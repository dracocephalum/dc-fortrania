using System.Globalization;
using Dracocephalum.Fortrania.Analysis;
using Dracocephalum.Fortrania.Syntax;

namespace Dracocephalum.Fortrania.Emit;

/// <content>Writes expressions, and the variables and array elements they are made of.</content>
internal sealed partial class UnitEmitter
{
    private static DataType Wider(DataType left, DataType right) => left > right ? left : right;

    // The value of an expression. A logical value is a C# bool here; it
    // becomes an integer only where it is stored.
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

            case Expression.Parenthesized parenthesized:
                var inner = Value(parenthesized.Inner);
                return new Code($"({inner.Text})", inner.Type, Level.Primary);

            case Expression.Unary unary:
                return Unary(unary);

            case Expression.Binary binary:
                return Binary(binary);

            default:
                throw Unsupported("a substring");
        }
    }

    private Code Constant(Expression.Literal literal)
    {
        if (literal.Type is DataType.Character or DataType.Complex)
        {
            throw Unsupported(literal.Type == DataType.Character ? "a character constant" : "a complex constant");
        }

        return new Code(CSharpText.Literal(_evaluator.TryEvaluate(literal, _line)!), literal.Type, Level.Primary);
    }

    private Code Named(Symbol constant)
    {
        if (constant.Type!.Type is DataType.Character or DataType.Complex)
        {
            throw Unsupported(constant.Type.Type == DataType.Character ? "a character constant" : "a complex constant");
        }

        if (!_constantsUsed.Contains(constant))
        {
            _constantsUsed.Add(constant);
        }

        return new Code(CSharpText.Local(constant.Name), constant.Type.Type, Level.Primary);
    }

    // Where a variable that is not an array is kept: a local of the method,
    // or behind the pointer the caller passed.
    private Code Variable(Symbol symbol)
    {
        if (symbol.Kind != SymbolKind.Variable || symbol.IsArray)
        {
            throw Unsupported("a whole array or a procedure used as a value");
        }

        var name = CSharpText.Local(symbol.Name);
        return symbol.IsDummyArgument
            ? new Code($"*{name}", symbol.Type!.Type, Level.Unary)
            : new Code(name, symbol.Type!.Type, Level.Primary);
    }

    private Code Element(Symbol array, IReadOnlyList<Expression> subscripts) =>
        new($"{CSharpText.Local(array.Name)}[{Offset(array, subscripts)}]", array.Type!.Type, Level.Primary);

    // What a stored value is in an expression: itself, or for a logical
    // one, whether it is not zero.
    private static Code Loaded(Code storage) => storage.Type == DataType.Logical
        ? new Code($"{storage.At(Level.Relational)} != 0", DataType.Logical, Level.Equality)
        : storage;

    // The variable or array element an assignment stores into.
    private Code Target(Expression target) => target switch
    {
        Expression.Name name => Variable(_table.Find(name.Identifier)!),
        Expression.Reference reference => Element(_table.Find(reference.Identifier)!, reference.Arguments),
        _ => throw Unsupported("a substring"),
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

        return value.Type <= type ? value.Text : $"({CSharpText.Storage(type)}){value.At(Level.Unary)}";
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
            throw Unsupported("a character expression");
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

        // The right side is held one level tighter, so that what the source
        // grouped to the right stays grouped: a - (b - c).
        var text = $"{left.At(level)} {symbol} {right.At(level + 1)}";
        return new Code(text, isLogical ? DataType.Logical : Wider(left.Type, right.Type), level);
    }

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
