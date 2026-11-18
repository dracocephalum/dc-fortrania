using System.Globalization;
using Dracocephalum.Fortrania.Analysis;
using Dracocephalum.Fortrania.Syntax;

namespace Dracocephalum.Fortrania.Emit;

/// <content>Writes expressions, and the variables and array elements they are made of.</content>
internal sealed partial class UnitEmitter
{
    private static DataType Wider(DataType left, DataType right) => left > right ? left : right;

    // A length or a position is put in parentheses before it joins an
    // expression, unless it is a single name or number.
    private static string Grouped(string text) => text.All(character => char.IsLetterOrDigit(character) || character == '_')
        ? text
        : $"({text})";

    // The value of an expression. A logical value is a C# bool here; it
    // becomes an integer only where it is stored. A character value is
    // the address of its first byte, with its length beside it.
    private Code Value(Expression expression)
    {
        switch (expression)
        {
            case Expression.Literal literal:
                return Constant(literal);

            case Expression.Name name:
                var symbol = _table.Find(name.Identifier)!;
                if (symbol.Kind == SymbolKind.Constant)
                {
                    return Named(symbol);
                }

                return Loaded(Variable(symbol));

            case Expression.Reference reference:
                var referenced = _table.Find(reference.Identifier)!;
                return referenced.Kind switch
                {
                    SymbolKind.Variable => Loaded(Element(referenced, reference.Arguments)),
                    SymbolKind.IntrinsicFunction => Intrinsic(reference),
                    SymbolKind.ExternalFunction => Call(referenced.Name, reference.Arguments),
                    _ => throw Unsupported("a statement function"),
                };

            case Expression.Substring substring:
                return Substring(substring);

            case Expression.Parenthesized parenthesized:
                var inner = Value(parenthesized.Inner);
                return new Code($"({inner.Text})", inner.Type, Level.Primary, inner.Length);

            case Expression.Unary unary:
                return Unary(unary);

            case Expression.Binary binary:
                return Binary(binary);

            default:
                throw new ArgumentOutOfRangeException(nameof(expression));
        }
    }

    private Code Constant(Expression.Literal literal)
    {
        if (literal.Type == DataType.Complex)
        {
            throw Unsupported("a complex constant");
        }

        var value = _evaluator.TryEvaluate(literal, _line)!;
        return value is Constant.Character text
            ? Text(text.Value)
            : new Code(value.ToCSharpLiteral(), literal.Type, Level.Primary);
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

        return new Code(constant.Name.ToCSharpLocal(), constant.Type.Type, Level.Primary);
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
            $"Fortran.Text({value.ToUtf8Literal()})",
            DataType.Character,
            Level.Primary,
            value.Length.ToString(CultureInfo.InvariantCulture));
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

        var name = symbol.Name.ToCSharpLocal();
        if (symbol.Type!.Type == DataType.Character)
        {
            return new Code(name, DataType.Character, Level.Primary, LengthOf(symbol));
        }

        return symbol.IsDummyArgument
            ? new Code($"*{name}", symbol.Type.Type, Level.Unary)
            : new Code(name, symbol.Type.Type, Level.Primary);
    }

    private Code Element(Symbol array, IReadOnlyList<Expression> subscripts)
    {
        var name = array.Name.ToCSharpLocal();
        var offset = Offset(array, subscripts);

        if (array.Type!.Type != DataType.Character)
        {
            return new Code($"{name}[{offset}]", array.Type.Type, Level.Primary);
        }

        // Elements of a character array lie end to end, each of the
        // array's length.
        var length = LengthOf(array);
        if (offset == "0")
        {
            return new Code(name, DataType.Character, Level.Primary, length);
        }

        var distance = length == "1" ? Grouped(offset) : $"{Grouped(offset)} * {Grouped(length)}";
        return new Code($"{name} + {distance}", DataType.Character, Level.Additive, length);
    }

    // The length of a character variable or array element: the one its
    // declaration gives, or for a dummy argument of assumed length, the one
    // the caller passed.
    private string LengthOf(Symbol symbol)
    {
        var length = symbol.Type!.Length;
        if (length is null)
        {
            return "1";
        }

        if (length.IsAssumed)
        {
            return symbol.IsDummyArgument ? LengthName(symbol) : throw Unsupported("a CHARACTER function");
        }

        return _evaluator.TryEvaluate(length.Value!, _line) is Constant.Integer constant
            ? constant.Value.ToString(CultureInfo.InvariantCulture)
            : throw Unsupported("a CHARACTER length that is not constant");
    }

    // Names the argument that carries the length of a character dummy
    // argument. The underscore inside it is in no FORTRAN 77 name; where a
    // compiler's extension has let the unit have such a name all the same,
    // one more underscore in front keeps the two apart.
    private string LengthName(Symbol dummy)
    {
        var name = dummy.Name.ToLowerInvariant() + "_len";
        return _table.Find(dummy.Name + "_LEN") is null ? name : "_" + name;
    }

    // Part of a character value, from one position to another, both counted
    // from one and both included.
    private Code Substring(Expression.Substring substring)
    {
        var whole = Value(substring.Target);
        var first = substring.First is null ? new Code("1", DataType.Integer, Level.Primary) : Value(substring.First);
        var last = substring.Last is null ? new Code(Grouped(whole.Length!), DataType.Integer, Level.Primary) : Value(substring.Last);

        // Where the positions are known when translating, the address and
        // the length come out as plain numbers.
        var firstKnown = substring.First is null ? 1 : Known(substring.First);
        var lastKnown = substring.Last is not null
            ? Known(substring.Last)
            : int.TryParse(whole.Length, NumberStyles.None, CultureInfo.InvariantCulture, out var wholeLength) ? wholeLength : null;

        var address = firstKnown switch
        {
            1 => whole,
            { } position => new Code(
                string.Create(CultureInfo.InvariantCulture, $"{whole.At(Level.Additive)} + {position - 1}"),
                DataType.Character,
                Level.Additive),
            null => new Code($"{whole.At(Level.Additive)} + {first.At(Level.Additive)} - 1", DataType.Character, Level.Additive),
        };

        string length;
        if (firstKnown is { } start && lastKnown is { } end)
        {
            length = (end - start + 1).ToString(CultureInfo.InvariantCulture);
        }
        else if (firstKnown == 1)
        {
            length = last.Text;
        }
        else if (firstKnown is { } known)
        {
            length = string.Create(CultureInfo.InvariantCulture, $"{last.At(Level.Additive)} - {known - 1}");
        }
        else
        {
            length = $"{last.At(Level.Additive)} - {first.At(Level.Multiplicative)} + 1";
        }

        return new Code(address.Text, DataType.Character, address.Level, length);
    }

    private int? Known(Expression expression) => (_evaluator.TryEvaluate(expression, _line) as Constant.Integer)?.Value;

    // What a stored value is in an expression: itself, or for a logical
    // one, whether it is not zero.
    private static Code Loaded(Code storage) => storage.Type == DataType.Logical
        ? new Code($"{storage.At(Level.Relational)} != 0", DataType.Logical, Level.Equality)
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
    private static string Stored(Code value, DataType type)
    {
        if (type == DataType.Logical)
        {
            return value.Text switch
            {
                "true" => "1",
                "false" => "0",
                _ => $"{value.At(Level.Or)} ? 1 : 0",
            };
        }

        return value.Type <= type ? value.Text : $"({type.ToCSharpStorageType()}){value.At(Level.Unary)}";
    }

    // The position of an element counted from the array's first, with the
    // first subscript running fastest, as the language lays arrays out.
    private string Offset(Symbol array, IReadOnlyList<Expression> subscripts)
    {
        long constant = 0;
        var terms = new List<string>();
        long stride = 1;
        string? strideText = null;

        for (var dimension = 0; dimension < subscripts.Count; dimension++)
        {
            var bound = array.Dimensions[dimension];
            var lower = ConstantBound(bound.Lower, 1);
            var subscript = _evaluator.TryEvaluate(subscripts[dimension], _line) as Constant.Integer;

            if (strideText is null && lower is { } low && subscript is not null)
            {
                constant += (subscript.Value - low) * stride;
            }
            else
            {
                var position = Value(subscripts[dimension]);
                var text = position.Text;
                var level = position.Level;

                if (lower is null)
                {
                    text = $"{position.At(Level.Additive)} - {Value(bound.Lower!).At(Level.Multiplicative)}";
                    level = Level.Additive;
                }
                else if (strideText is null)
                {
                    // A constant lower bound joins the one constant term.
                    constant -= (long)lower.Value * stride;
                }
                else if (lower.Value != 0)
                {
                    var shift = Math.Abs((long)lower.Value).ToString(CultureInfo.InvariantCulture);
                    text = $"{position.At(Level.Additive)} {(lower.Value > 0 ? "-" : "+")} {shift}";
                    level = Level.Additive;
                }

                var term = new Code(text, DataType.Integer, level);
                if (strideText is not null)
                {
                    terms.Add($"{term.At(Level.Multiplicative)} * {strideText}");
                }
                else if (stride != 1)
                {
                    terms.Add(string.Create(CultureInfo.InvariantCulture, $"{term.At(Level.Multiplicative)} * {stride}"));
                }
                else
                {
                    terms.Add(term.At(Level.Additive));
                }
            }

            // The stride of the next dimension; it is not needed after the
            // last, whose extent an assumed size leaves unknown.
            if (dimension == subscripts.Count - 1)
            {
                break;
            }

            if (strideText is null && ConstantExtent(bound) is { } extent)
            {
                stride *= extent;
            }
            else
            {
                var size = Extent(bound);
                strideText = strideText is null
                    ? (stride == 1 ? size : string.Create(CultureInfo.InvariantCulture, $"{stride} * {size}"))
                    : $"{strideText} * {size}";
            }
        }

        var sum = string.Join(" + ", terms);
        if (terms.Count == 0)
        {
            return constant.ToString(CultureInfo.InvariantCulture);
        }

        return constant switch
        {
            0 => sum,
            > 0 => string.Create(CultureInfo.InvariantCulture, $"{sum} + {constant}"),
            _ => string.Create(CultureInfo.InvariantCulture, $"{sum} - {-constant}"),
        };
    }

    // The number of elements along a dimension whose bounds are not both
    // constant, which only a dummy argument's array can have.
    private string Extent(DimensionBound bound)
    {
        var upper = Value(bound.Upper ?? throw Unsupported("an assumed size in a dimension that is not the last"));
        return ConstantBound(bound.Lower, 1) switch
        {
            1 => upper.At(Level.Primary),
            { } lower => string.Create(CultureInfo.InvariantCulture, $"({upper.At(Level.Additive)} - {lower} + 1)"),
            null => $"({upper.At(Level.Additive)} - {Value(bound.Lower!).At(Level.Multiplicative)} + 1)",
        };
    }

    private Code Unary(Expression.Unary unary)
    {
        var operand = Value(unary.Operand);
        switch (unary.Operator)
        {
            case UnaryOperator.Plus:
                return operand;
            case UnaryOperator.Not:
                return new Code($"!{operand.At(Level.Unary)}", DataType.Logical, Level.Unary);
            default:
                // Two minus signs side by side would read as a decrement.
                var text = operand.At(Level.Unary);
                return new Code(text.StartsWith('-') ? $"-({text})" : $"-{text}", operand.Type, Level.Unary);
        }
    }

    private Code Binary(Expression.Binary binary)
    {
        if (binary.Operator == BinaryOperator.Power)
        {
            return Power(binary);
        }

        if (binary.Operator == BinaryOperator.Concatenate)
        {
            throw Unsupported("a character concatenation");
        }

        var left = Value(binary.Left);
        var right = Value(binary.Right);

        var (symbol, level, isLogical) = binary.Operator switch
        {
            BinaryOperator.Add => ("+", Level.Additive, false),
            BinaryOperator.Subtract => ("-", Level.Additive, false),
            BinaryOperator.Multiply => ("*", Level.Multiplicative, false),
            BinaryOperator.Divide => ("/", Level.Multiplicative, false),
            BinaryOperator.Equal => ("==", Level.Equality, true),
            BinaryOperator.NotEqual => ("!=", Level.Equality, true),
            BinaryOperator.LessThan => ("<", Level.Relational, true),
            BinaryOperator.LessOrEqual => ("<=", Level.Relational, true),
            BinaryOperator.GreaterThan => (">", Level.Relational, true),
            BinaryOperator.GreaterOrEqual => (">=", Level.Relational, true),
            BinaryOperator.And => ("&&", Level.And, true),
            BinaryOperator.Or => ("||", Level.Or, true),
            BinaryOperator.Equivalent => ("==", Level.Equality, true),
            _ => ("!=", Level.Equality, true),
        };

        // Character values are set in order by the runtime, and the
        // operator then asks how that came out.
        if (left.Type == DataType.Character)
        {
            return new Code($"{Compared(left, right)} {symbol} 0", DataType.Logical, level);
        }

        // The right side is held one level tighter, so that what the source
        // grouped to the right stays grouped: a - (b - c).
        var text = $"{left.At(level)} {symbol} {right.At(level + 1)}";
        return new Code(text, isLogical ? DataType.Logical : Wider(left.Type, right.Type), level);
    }

    private static string Compared(Code left, Code right) =>
        $"Fortran.Compare({left.Text}, {left.Length}, {right.Text}, {right.Length})";

    // An integer exponent multiplies; any other is a call to the library.
    private Code Power(Expression.Binary power)
    {
        var value = Value(power.Left);
        var exponent = Value(power.Right);

        if (exponent.Type != DataType.Integer)
        {
            var type = Wider(value.Type, exponent.Type);
            var library = type == DataType.Real ? "MathF" : "Math";
            return new Code($"{library}.Pow({value.Text}, {exponent.Text})", type, Level.Primary);
        }

        var isSquare = _evaluator.TryEvaluate(power.Right, _line) is Constant.Integer { Value: 2 };
        var isSimple = power.Left is Expression.Literal
            || (power.Left is Expression.Name name && _table.Find(name.Identifier) is { IsArray: false });
        if (isSquare && isSimple)
        {
            return new Code($"{value.At(Level.Unary)} * {value.At(Level.Unary)}", value.Type, Level.Multiplicative);
        }

        return new Code($"Fortran.Pow({value.Text}, {exponent.Text})", value.Type, Level.Primary);
    }
}
