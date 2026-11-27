using System.Globalization;
using Dracocephalum.Fortrania.Analysis;
using Dracocephalum.Fortrania.Syntax;

namespace Dracocephalum.Fortrania.Emit;

/// <content>
/// Turns expressions, and the variables and array elements they are made
/// of, into the program tree.
/// </content>
internal sealed partial class UnitEmitter
{
    private static readonly TreeExpression Zero = new TreeExpression.Integer(0);
    private static readonly TreeExpression One = new TreeExpression.Integer(1);

    private static DataType Wider(DataType left, DataType right) => left > right ? left : right;

    // A length or a position is put in parentheses before it joins an
    // expression, unless it is a single name or number.

    private static TreeExpression Group(TreeExpression node) => node is TreeExpression.Local or TreeExpression.Integer { Value: >= 0 }
        ? node
        : new TreeExpression.Grouped(node);

    private static TreeExpression AtLeast(TreeExpression node, Level needed) =>
        CSharpExpressionWriter.LevelOf(node) >= needed ? node : new TreeExpression.Grouped(node);

    // The number a length is, where it is known when translating.
    private static int? KnownNumber(TreeExpression? node) =>
        node is TreeExpression.Integer { Value: >= 0 and <= int.MaxValue } integer ? (int)integer.Value : null;

    private static TreeExpression.Sum Add(params TreeExpression.Term[] terms) => new TreeExpression.Sum(terms);

    private static TreeExpression.Term Plus(TreeExpression value) => new(value);

    private static TreeExpression.Term Minus(TreeExpression value) => new(value, IsSubtracted: true);

    // The value of an expression. A logical value is a C# bool here; it
    // becomes an integer only where it is stored. A character value is
    // the address of its first byte, with its length beside it.
    private Code Value(Expression expression)
    {
        switch (expression)
        {
            case Expression.Literal literal:
                return LiteralValue(literal);

            case Expression.Name name:
                var symbol = _table.Find(name.Identifier)!;
                if (symbol.Kind == SymbolKind.Constant)
                {
                    return Named(symbol);
                }

                // A logical argument of a statement function is a bool
                // already: it was passed as a value, not stored.
                return IsOwnArgument(symbol) ? Variable(symbol) : Loaded(Variable(symbol));

            case Expression.Reference reference:
                var referenced = _table.Find(reference.Identifier)!;
                return referenced.Kind switch
                {
                    SymbolKind.Variable => Loaded(Element(referenced, reference.Arguments)),
                    SymbolKind.IntrinsicFunction => Intrinsic(reference),
                    SymbolKind.ExternalFunction => Call(referenced, reference.Arguments),
                    _ => StatementFunctionCall(referenced, reference.Arguments),
                };

            case Expression.Substring substring:
                return Substring(substring);

            case Expression.Parenthesized parenthesized:
                var inner = Value(parenthesized.Inner);
                return new Code(new TreeExpression.Grouped(inner.Node), inner.Type, inner.LengthNode);

            case Expression.Unary unary:
                return Unary(unary);

            case Expression.Binary binary:
                return Binary(binary);

            default:
                throw new ArgumentOutOfRangeException(nameof(expression));
        }
    }

    private Code LiteralValue(Expression.Literal literal)
    {
        if (literal.Type == DataType.Complex)
        {
            throw Unsupported("a complex constant");
        }

        return _evaluator.TryEvaluate(literal, _line)! switch
        {
            Constant.Character text => Text(text.Value),
            Constant.Integer integer => new Code(new TreeExpression.Integer(integer.Value), literal.Type),
            var value => new Code(new TreeExpression.Number(value), literal.Type),
        };
    }

    private Code Named(Symbol constant)
    {
        if (constant.Type!.Type == DataType.Complex)
        {
            throw Unsupported("a complex constant");
        }

        // A character constant is written out where it is used: its bytes
        // lie in the assembly once, however often they are named.
        if (_evaluator.ValueOf(constant) is Constant.Character text)
        {
            return Text(text.Value);
        }

        if (!_constantsUsed.Contains(constant))
        {
            _constantsUsed.Add(constant);
        }

        return new Code(new TreeExpression.Local(constant.Name.ToCSharpLocal()), constant.Type.Type);
    }

    // A character constant: the address of a literal the compiler keeps in
    // the assembly, and the number of its bytes.
    private Code Text(string value)
    {
        if (value.Any(character => character > '\u007f'))
        {
            throw Unsupported("a character constant with a character outside ASCII");
        }

        return new Code(
            new TreeExpression.Call(Runtime("AddrOf"), [new TreeExpression.Utf8(value)]),
            DataType.Character,
            new TreeExpression.Integer(value.Length));
    }

    // Where a variable that is not an array is kept: a local of the method,
    // or behind the pointer the caller passed. A character variable is the
    // pointer itself, wherever it is kept.
    private Code Variable(Symbol symbol)
    {
        if (symbol.Kind != SymbolKind.Variable || symbol.IsArray)
        {
            throw Unsupported("a whole array or a procedure used as a value");
        }

        if (IsOwnArgument(symbol))
        {
            var length = symbol.Type!.Type == DataType.Character ? LengthNodeOf(symbol) : null;
            return new Code(new TreeExpression.Local(symbol.Name.ToCSharpLocal()), symbol.Type.Type, length);
        }

        var name = new TreeExpression.Local(Local(symbol));
        if (symbol.Type!.Type == DataType.Character)
        {
            return new Code(name, DataType.Character, LengthNodeOf(symbol));
        }

        return symbol.IsDummyArgument || IsPlaced(symbol)
            ? new Code(new TreeExpression.Dereference(name), symbol.Type.Type)
            : new Code(name, symbol.Type.Type);
    }

    private Code Element(Symbol array, IReadOnlyList<Expression> subscripts)
    {
        var name = new TreeExpression.Local(Local(array));
        var offset = Offset(array, subscripts);

        if (array.Type!.Type != DataType.Character)
        {
            return new Code(new TreeExpression.Indexed(name, offset), array.Type.Type);
        }

        // Elements of a character array lie end to end, each of the
        // array's length.
        var length = LengthNodeOf(array);
        if (offset is TreeExpression.Integer { Value: 0 })
        {
            return new Code(name, DataType.Character, length);
        }

        var distance = length is TreeExpression.Integer { Value: 1 }
            ? Group(offset)
            : new TreeExpression.Product([Group(offset), Group(length)]);
        return new Code(Add(Plus(name), Plus(distance)), DataType.Character, length);
    }

    // The length of a character variable or array element: the one its
    // declaration gives, or for a dummy argument of assumed length, the one
    // the caller passed.
    private long ConstantLengthOf(Symbol symbol) => LengthNodeOf(symbol) is TreeExpression.Integer known
        ? known.Value
        : throw Unsupported("a CHARACTER length that is not constant");

    private TreeExpression LengthNodeOf(Symbol symbol)
    {
        var length = symbol.Type!.Length;
        if (length is null)
        {
            return One;
        }

        if (length.IsAssumed)
        {
            // Of assumed length are a dummy argument and, inside a character
            // function, its result; the caller passes the length of both.
            return symbol.IsDummyArgument || symbol.IsEntryPoint
                ? new TreeExpression.Local(LengthName(symbol))
                : throw Unsupported("a CHARACTER length that is not constant");
        }

        return _evaluator.TryEvaluate(length.Value!, _line) is Constant.Integer constant
            ? new TreeExpression.Integer(constant.Value)
            : throw Unsupported("a CHARACTER length that is not constant");
    }

    // Names the argument that carries the length of a character dummy
    // argument. The underscore inside it is in no FORTRAN 77 name; where a
    // compiler's extension has let the unit have such a name all the same,
    // one more underscore in front keeps the two apart.
    private string LengthName(Symbol dummy)
    {
        dummy = Use(dummy);
        var name = dummy.Name.ToLowerInvariant() + "_len";
        return _table.Find(dummy.Name + "_LEN") is null ? name : "_" + name;
    }

    // Part of a character value, from one position to another, both counted
    // from one and both included.
    private Code Substring(Expression.Substring substring)
    {
        var whole = Value(substring.Target);
        var first = substring.First is null ? One : Value(substring.First).Node;
        var last = substring.Last is null ? Group(whole.LengthNode!) : Value(substring.Last).Node;

        // Where the positions are known when translating, the address and
        // the length come out as plain numbers.
        var firstKnown = substring.First is null ? 1 : Known(substring.First);
        var lastKnown = substring.Last is not null ? Known(substring.Last) : KnownNumber(whole.LengthNode);

        var address = firstKnown switch
        {
            1 => whole.Node,
            { } position => Add(Plus(whole.Node), Plus(new TreeExpression.Integer(position - 1))),
            null => Add(Plus(whole.Node), Plus(first), Minus(One)),
        };

        TreeExpression length;
        if (firstKnown is { } start && lastKnown is { } end)
        {
            length = new TreeExpression.Integer(end - start + 1);
        }
        else if (firstKnown == 1)
        {
            length = last;
        }
        else if (firstKnown is { } known)
        {
            length = Add(Plus(last), Minus(new TreeExpression.Integer(known - 1)));
        }
        else if (substring.Last is not null && ExpressionKey.Of(substring.First!) == ExpressionKey.Of(substring.Last))
        {
            // One position written twice, TEXT(I:I), is one character,
            // wherever it is: which lets what is done with it be done
            // with a byte.
            length = One;
        }
        else
        {
            length = Add(Plus(last), Minus(first), Plus(One));
        }

        return new Code(address, DataType.Character, length);
    }

    private int? Known(Expression expression) => (_evaluator.TryEvaluate(expression, _line) as Constant.Integer)?.Value;

    // What a stored value is in an expression: itself, or for a logical
    // one, whether it is not zero.
    private static Code Loaded(Code storage) => storage.Type == DataType.Logical
        ? new Code(new TreeExpression.Binary(TreeExpression.Operation.NotEqual, storage.Node, Zero), DataType.Logical)
        : storage;

    // The variable, array element or substring an assignment stores into.
    private Code Target(Expression target) => target switch
    {
        Expression.Name name => Variable(_table.Find(name.Identifier)!),
        Expression.Reference reference => Element(_table.Find(reference.Identifier)!, reference.Arguments),
        Expression.Substring substring => Substring(substring),
        _ => throw new ArgumentOutOfRangeException(nameof(target)),
    };

    // A value as it is stored in a place of the given type. Numbers convert
    // as assignment converts them, a real value to an integer by dropping
    // its fraction, which is what the cast does.

    private static TreeExpression StoredNode(Code value, DataType type)
    {
        if (type == DataType.Logical)
        {
            return value.Node is TreeExpression.Number { Value: Constant.Logical known }
                ? (known.Value ? One : Zero)
                : new TreeExpression.Conditional(value.Node, One, Zero);
        }

        return value.Type <= type ? value.Node : new TreeExpression.Converted(type.ToCSharpStorageType(), value.Node);
    }

    // The position of an element counted from the array's first, with the
    // first subscript running fastest, as the language lays arrays out.
    private TreeExpression Offset(Symbol array, IReadOnlyList<Expression> subscripts)
    {
        long constant = 0;
        var terms = new List<TreeExpression.Term>();
        long stride = 1;
        List<TreeExpression>? strideFactors = null;

        for (var dimension = 0; dimension < subscripts.Count; dimension++)
        {
            var bound = array.Dimensions[dimension];
            var lower = ConstantBound(bound.Lower, 1);
            var subscript = _evaluator.TryEvaluate(subscripts[dimension], _line) as Constant.Integer;

            if (strideFactors is null && lower is { } low && subscript is not null)
            {
                constant += (subscript.Value - low) * stride;
            }
            else
            {
                var position = Value(subscripts[dimension]).Node;

                if (lower is null)
                {
                    position = Add(Plus(position), Minus(Value(bound.Lower!).Node));
                }
                else if (strideFactors is null)
                {
                    // A constant lower bound joins the one constant term.
                    constant -= (long)lower.Value * stride;
                }
                else if (lower.Value != 0)
                {
                    var shift = new TreeExpression.Integer(Math.Abs((long)lower.Value));
                    position = Add(Plus(position), lower.Value > 0 ? Minus(shift) : Plus(shift));
                }

                if (strideFactors is not null)
                {
                    terms.Add(Plus(new TreeExpression.Product([position, .. strideFactors])));
                }
                else if (stride != 1)
                {
                    terms.Add(Plus(new TreeExpression.Product([position, new TreeExpression.Integer(stride)])));
                }
                else
                {
                    terms.Add(Plus(position));
                }
            }

            // The stride of the next dimension; it is not needed after the
            // last, whose extent an assumed size leaves unknown.
            if (dimension == subscripts.Count - 1)
            {
                break;
            }

            if (strideFactors is null && ConstantExtent(bound) is { } extent)
            {
                stride *= extent;
            }
            else
            {
                var size = Extent(bound);
                strideFactors = strideFactors is null
                    ? (stride == 1 ? [size] : [new TreeExpression.Integer(stride), size])
                    : [.. strideFactors, size];
            }
        }

        if (terms.Count == 0)
        {
            return new TreeExpression.Integer(constant);
        }

        switch (constant)
        {
            case 0:
                break;
            case > 0:
                terms.Add(Plus(new TreeExpression.Integer(constant)));
                break;
            default:
                terms.Add(Minus(new TreeExpression.Integer(-constant)));
                break;
        }

        return terms.Count == 1 ? AtLeast(terms[0].Value, Level.Additive) : new TreeExpression.Sum(terms);
    }

    // The number of elements along a dimension whose bounds are not both
    // constant, which only a dummy argument's array can have.
    private TreeExpression Extent(DimensionBound bound)
    {
        var upper = Value(bound.Upper ?? throw Unsupported("an assumed size in a dimension that is not the last")).Node;
        return ConstantBound(bound.Lower, 1) switch
        {
            1 => AtLeast(upper, Level.Primary),
            { } lower => new TreeExpression.Grouped(Add(Plus(upper), Minus(new TreeExpression.Integer(lower)), Plus(One))),
            null => new TreeExpression.Grouped(Add(Plus(upper), Minus(Value(bound.Lower!).Node), Plus(One))),
        };
    }

    private Code Unary(Expression.Unary unary)
    {
        var operand = Value(unary.Operand);
        return unary.Operator switch
        {
            UnaryOperator.Plus => operand,
            UnaryOperator.Not => new Code(new TreeExpression.Unary(TreeExpression.Operation.Not, operand.Node), DataType.Logical),
            _ => new Code(new TreeExpression.Unary(TreeExpression.Operation.Negate, operand.Node), operand.Type),
        };
    }

    private Code Binary(Expression.Binary binary)
    {
        if (binary.Operator == BinaryOperator.Power)
        {
            return Power(binary);
        }

        if (binary.Operator == BinaryOperator.Concatenate)
        {
            return Joined(binary);
        }

        var left = Value(binary.Left);
        var right = Value(binary.Right);

        var (@operator, isLogical) = binary.Operator switch
        {
            BinaryOperator.Add => (TreeExpression.Operation.Add, false),
            BinaryOperator.Subtract => (TreeExpression.Operation.Subtract, false),
            BinaryOperator.Multiply => (TreeExpression.Operation.Multiply, false),
            BinaryOperator.Divide => (TreeExpression.Operation.Divide, false),
            BinaryOperator.Equal => (TreeExpression.Operation.Equal, true),
            BinaryOperator.NotEqual => (TreeExpression.Operation.NotEqual, true),
            BinaryOperator.LessThan => (TreeExpression.Operation.Less, true),
            BinaryOperator.LessOrEqual => (TreeExpression.Operation.LessOrEqual, true),
            BinaryOperator.GreaterThan => (TreeExpression.Operation.Greater, true),
            BinaryOperator.GreaterOrEqual => (TreeExpression.Operation.GreaterOrEqual, true),
            BinaryOperator.And => (TreeExpression.Operation.And, true),
            BinaryOperator.Or => (TreeExpression.Operation.Or, true),
            BinaryOperator.Equivalent => (TreeExpression.Operation.Equal, true),
            _ => (TreeExpression.Operation.NotEqual, true),
        };

        // Character values are set in order by the runtime, and the
        // operator then asks how that came out.
        if (left.Type == DataType.Character)
        {
            return new Code(new TreeExpression.Binary(@operator, ComparedNode(left, right), Zero), DataType.Logical);
        }

        return new Code(
            new TreeExpression.Binary(@operator, left.Node, right.Node),
            isLogical ? DataType.Logical : Wider(left.Type, right.Type));
    }

    private TreeExpression.Call ComparedNode(Code left, Code right) =>
        new TreeExpression.Call(Runtime("Compare"), [left.Node, left.LengthNode!, right.Node, right.LengthNode!]);

    // An integer exponent multiplies; any other is a call to the library.
    private Code Power(Expression.Binary power)
    {
        var value = Value(power.Left);
        var exponent = Value(power.Right);

        if (exponent.Type != DataType.Integer)
        {
            var type = Wider(value.Type, exponent.Type);
            var library = type == DataType.Real ? "MathF" : "Math";
            return new Code(new TreeExpression.Call($"{library}.Pow", [value.Node, exponent.Node]), type);
        }

        var isSquare = _evaluator.TryEvaluate(power.Right, _line) is Constant.Integer { Value: 2 };
        var isSimple = power.Left is Expression.Literal
            || (power.Left is Expression.Name name && _table.Find(name.Identifier) is { IsArray: false });
        if (isSquare && isSimple)
        {
            return new Code(new TreeExpression.Binary(TreeExpression.Operation.Multiply, value.Node, value.Node), value.Type);
        }

        return new Code(new TreeExpression.Call(Runtime("Pow"), [value.Node, exponent.Node]), value.Type);
    }
}
