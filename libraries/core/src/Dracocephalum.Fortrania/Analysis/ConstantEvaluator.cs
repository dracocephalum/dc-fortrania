using System.Globalization;
using Dracocephalum.Fortrania.Syntax;

namespace Dracocephalum.Fortrania.Analysis;

/// <summary>
/// Evaluates the constant expressions of one program unit: the values of
/// its named constants, and whatever is built from constants alone, such as
/// an array bound or a character length.
/// </summary>
/// <remarks>
/// It computes as the language does: a real is single precision until
/// something widens it, so <c>PARAMETER ( PI = 3.14159265358979 )</c> gives a
/// double precision <c>PI</c> only seven good digits, and an integer is 32
/// bits, with anything that does not fit an error and never a wrap.
/// </remarks>
public sealed class ConstantEvaluator
{
    private readonly SymbolTable _table;
    private readonly Dictionary<string, Constant> _values = new(StringComparer.Ordinal);
    private readonly HashSet<string> _inProgress = new(StringComparer.Ordinal);

    /// <summary>Initializes a new instance of the <see cref="ConstantEvaluator"/> class.</summary>
    /// <param name="table">The names of the program unit the expressions belong to.</param>
    public ConstantEvaluator(SymbolTable table)
    {
        ArgumentNullException.ThrowIfNull(table);
        _table = table;
    }

    /// <summary>Evaluates <paramref name="expression"/> if it is a constant expression.</summary>
    /// <param name="expression">An expression of the program unit.</param>
    /// <param name="line">The line its statement starts on, for an error message.</param>
    /// <param name="includeFile">The include file that line is in, or <see langword="null"/>.</param>
    /// <returns>
    /// The value, or <see langword="null"/> when the expression is not
    /// constant: it reads a variable or an array element, or references a
    /// function.
    /// </returns>
    /// <exception cref="FortranAnalysisException">
    /// The expression is constant and has no value: it overflows, divides by
    /// zero, combines types that do not combine, or depends on itself.
    /// </exception>
    public Constant? TryEvaluate(Expression expression, int line, string? includeFile = null)
    {
        ArgumentNullException.ThrowIfNull(expression);
        return Evaluate(expression, new SourceLocation(line, includeFile));
    }

    /// <summary>Gives the value of a named constant, converted to the constant's own type and length.</summary>
    /// <param name="constant">A symbol of the program unit whose kind is <see cref="SymbolKind.Constant"/>.</param>
    /// <returns>The value.</returns>
    /// <exception cref="FortranAnalysisException">
    /// The expression that defines it is not constant, has no value, or has
    /// a type the constant cannot take.
    /// </exception>
    public Constant ValueOf(Symbol constant)
    {
        ArgumentNullException.ThrowIfNull(constant);

        if (constant.ConstantValue is null || !ReferenceEquals(_table.Find(constant.Name), constant))
        {
            throw new ArgumentException("The symbol is not a named constant of this program unit.", nameof(constant));
        }

        if (_values.TryGetValue(constant.Name, out var known))
        {
            return known;
        }

        var at = new SourceLocation(constant.Line, constant.IncludeFile);
        if (!_inProgress.Add(constant.Name))
        {
            throw at.Error($"The value of {constant.Name} depends on itself.");
        }

        var value = Evaluate(constant.ConstantValue, at)
            ?? throw at.Error($"The value of {constant.Name} is not a constant expression.");

        var converted = Convert(value, constant, at);
        _inProgress.Remove(constant.Name);
        _values.Add(constant.Name, converted);
        return converted;
    }

    private static Constant ParseLiteral(Expression.Literal literal, SourceLocation at)
    {
        var text = literal.Text;
        switch (literal.Type)
        {
            case DataType.Integer:
                return new Constant.Integer(ToInteger(ParseInteger(text, at), at));

            case DataType.Real:
                var single = float.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture);
                return float.IsFinite(single) ? new Constant.Real(single) : throw at.Error($"{text} is too large for a real.");

            case DataType.DoublePrecision:
                var wide = double.Parse(text.Replace('D', 'E'), NumberStyles.Float, CultureInfo.InvariantCulture);
                return double.IsFinite(wide) ? new Constant.DoublePrecision(wide) : throw at.Error($"{text} is too large for a double precision value.");

            case DataType.Logical:
                return new Constant.Logical(text is ".TRUE.");

            case DataType.Character:
                // The delimiter is doubled to stand for itself inside.
                var delimiter = text[..1];
                return new Constant.Character(text[1..^1].Replace(delimiter + delimiter, delimiter, StringComparison.Ordinal));

            default:
                throw at.Error($"A constant of type {literal.Type} is not supported yet.");
        }
    }

    private static long ParseInteger(string text, SourceLocation at) =>
        long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var value)
            ? value
            : throw at.Error($"{text} is too large for an integer.");

    private static int ToInteger(long value, SourceLocation at) =>
        value is >= int.MinValue and <= int.MaxValue
            ? (int)value
            : throw at.Error($"{value.ToString(CultureInfo.InvariantCulture)} does not fit in an integer.");

    // Conversion to integer drops the fraction, toward zero.
    private static int ToInteger(double value, SourceLocation at)
    {
        var truncated = Math.Truncate(value);
        return truncated is >= int.MinValue and <= int.MaxValue
            ? (int)truncated
            : throw at.Error($"{value.ToString(CultureInfo.InvariantCulture)} does not fit in an integer.");
    }

    private static double ToDouble(Constant value) => value switch
    {
        Constant.Integer integer => integer.Value,
        Constant.Real real => real.Value,
        Constant.DoublePrecision wide => wide.Value,
        _ => throw new ArgumentOutOfRangeException(nameof(value)),
    };

    private static bool IsNumeric(Constant value) => value is Constant.Integer or Constant.Real or Constant.DoublePrecision;

    private static Constant Negate(Constant value, SourceLocation at) => value switch
    {
        Constant.Integer integer => new Constant.Integer(ToInteger(-(long)integer.Value, at)),
        Constant.Real real => new Constant.Real(-real.Value),
        Constant.DoublePrecision wide => new Constant.DoublePrecision(-wide.Value),
        _ => throw at.Error($"A sign is applied to a value of type {value.Type}."),
    };

    private static Constant Arithmetic(BinaryOperator op, Constant left, Constant right, SourceLocation at)
    {
        if (left is Constant.Integer a && right is Constant.Integer b)
        {
            return new Constant.Integer(ToInteger(IntegerArithmetic(op, a.Value, b.Value, at), at));
        }

        if (left is Constant.DoublePrecision || right is Constant.DoublePrecision)
        {
            var x = ToDouble(left);
            var y = ToDouble(right);
            var result = op switch
            {
                BinaryOperator.Add => x + y,
                BinaryOperator.Subtract => x - y,
                BinaryOperator.Multiply => x * y,
                BinaryOperator.Divide => y != 0 ? x / y : throw at.Error("A constant expression divides by zero."),
                _ => Math.Pow(x, y),
            };
            return double.IsFinite(result)
                ? new Constant.DoublePrecision(result)
                : throw at.Error("A constant expression has no double precision value: it overflows or is undefined.");
        }
        else
        {
            // Single precision, computed in single precision. A power is the
            // exception: it is worked out wider and rounded once.
            var x = (float)ToDouble(left);
            var y = (float)ToDouble(right);
            var result = op switch
            {
                BinaryOperator.Add => x + y,
                BinaryOperator.Subtract => x - y,
                BinaryOperator.Multiply => x * y,
                BinaryOperator.Divide => y != 0 ? x / y : throw at.Error("A constant expression divides by zero."),
                _ => (float)Math.Pow(x, y),
            };
            return float.IsFinite(result)
                ? new Constant.Real(result)
                : throw at.Error("A constant expression has no real value: it overflows or is undefined.");
        }
    }

    private static long IntegerArithmetic(BinaryOperator op, int left, int right, SourceLocation at)
    {
        switch (op)
        {
            case BinaryOperator.Add:
                return (long)left + right;
            case BinaryOperator.Subtract:
                return (long)left - right;
            case BinaryOperator.Multiply:
                return (long)left * right;
            case BinaryOperator.Divide:
                // Integer division drops the remainder, toward zero.
                return right != 0 ? (long)left / right : throw at.Error("A constant expression divides by zero.");
        }

        // A negative power of an integer is the reciprocal with its fraction
        // dropped: zero, unless the base is one or minus one.
        if (right < 0)
        {
            return left switch
            {
                0 => throw at.Error("A constant expression raises zero to a negative power."),
                1 => 1,
                -1 => right % 2 == 0 ? 1 : -1,
                _ => 0,
            };
        }

        long power = 1;
        for (var count = 0; count < right; count++)
        {
            power *= left;
            if (power is < int.MinValue or > int.MaxValue)
            {
                throw at.Error($"{left.ToString(CultureInfo.InvariantCulture)} ** {right.ToString(CultureInfo.InvariantCulture)} does not fit in an integer.");
            }
        }

        return power;
    }

    private static bool Compare(BinaryOperator op, Constant left, Constant right, SourceLocation at)
    {
        int order;
        if (left is Constant.Character a && right is Constant.Character b)
        {
            // The shorter one counts as padded with blanks.
            var length = Math.Max(a.Value.Length, b.Value.Length);
            order = string.CompareOrdinal(a.Value.PadRight(length), b.Value.PadRight(length));
        }
        else if (IsNumeric(left) && IsNumeric(right))
        {
            order = left is Constant.Integer x && right is Constant.Integer y
                ? x.Value.CompareTo(y.Value)
                : left is Constant.DoublePrecision || right is Constant.DoublePrecision
                    ? ToDouble(left).CompareTo(ToDouble(right))
                    : ((float)ToDouble(left)).CompareTo((float)ToDouble(right));
        }
        else
        {
            throw Mismatch(op, left, right, at);
        }

        return op switch
        {
            BinaryOperator.Equal => order == 0,
            BinaryOperator.NotEqual => order != 0,
            BinaryOperator.LessThan => order < 0,
            BinaryOperator.LessOrEqual => order <= 0,
            BinaryOperator.GreaterThan => order > 0,
            _ => order >= 0,
        };
    }

    private static FortranAnalysisException Mismatch(BinaryOperator op, Constant left, Constant right, SourceLocation at) =>
        at.Error($"{op} cannot combine a value of type {left.Type} with one of type {right.Type}.");

    private Constant? Evaluate(Expression expression, SourceLocation at)
    {
        switch (expression)
        {
            case Expression.Literal literal:
                return ParseLiteral(literal, at);

            case Expression.Name name:
                return _table.Find(name.Identifier) is { Kind: SymbolKind.Constant } symbol ? ValueOf(symbol) : null;

            case Expression.Parenthesized parenthesized:
                return Evaluate(parenthesized.Inner, at);

            // The most negative integer has no positive counterpart, so its
            // digits are read together with the sign.
            case Expression.Unary { Operator: UnaryOperator.Minus, Operand: Expression.Literal { Type: DataType.Integer } digits }:
                return new Constant.Integer(ToInteger(-ParseInteger(digits.Text, at), at));

            case Expression.Unary unary:
                return Evaluate(unary.Operand, at) switch
                {
                    null => null,
                    var operand when unary.Operator == UnaryOperator.Minus => Negate(operand, at),
                    var operand when unary.Operator == UnaryOperator.Plus && IsNumeric(operand) => operand,
                    Constant.Logical logical when unary.Operator == UnaryOperator.Not => new Constant.Logical(!logical.Value),
                    var operand => throw at.Error($"{unary.Operator} is applied to a value of type {operand.Type}."),
                };

            case Expression.Binary binary:
                var left = Evaluate(binary.Left, at);
                var right = Evaluate(binary.Right, at);
                return left is null || right is null ? null : Combine(binary.Operator, left, right, at);

            default:
                // An array element, a substring or a function reference.
                return null;
        }
    }

    private static Constant Combine(BinaryOperator op, Constant left, Constant right, SourceLocation at)
    {
        switch (op)
        {
            case BinaryOperator.Add or BinaryOperator.Subtract or BinaryOperator.Multiply or BinaryOperator.Divide or BinaryOperator.Power:
                return IsNumeric(left) && IsNumeric(right) ? Arithmetic(op, left, right, at) : throw Mismatch(op, left, right, at);

            case BinaryOperator.Concatenate:
                return left is Constant.Character a && right is Constant.Character b
                    ? new Constant.Character(a.Value + b.Value)
                    : throw Mismatch(op, left, right, at);

            case BinaryOperator.And or BinaryOperator.Or or BinaryOperator.Equivalent or BinaryOperator.NotEquivalent:
                if (left is not Constant.Logical p || right is not Constant.Logical q)
                {
                    throw Mismatch(op, left, right, at);
                }

                return new Constant.Logical(op switch
                {
                    BinaryOperator.And => p.Value && q.Value,
                    BinaryOperator.Or => p.Value || q.Value,
                    BinaryOperator.Equivalent => p.Value == q.Value,
                    _ => p.Value != q.Value,
                });

            default:
                return new Constant.Logical(Compare(op, left, right, at));
        }
    }

    // What assignment does: a number takes the constant's numeric type, and
    // a character value is cut or padded with blanks to the constant's length.
    private Constant Convert(Constant value, Symbol constant, SourceLocation at)
    {
        var type = constant.Type!;
        switch (type.Type)
        {
            case DataType.Integer when IsNumeric(value):
                return value as Constant.Integer ?? new Constant.Integer(ToInteger(ToDouble(value), at));

            case DataType.Real when IsNumeric(value):
                var single = (float)ToDouble(value);
                return float.IsFinite(single) ? new Constant.Real(single) : throw at.Error($"The value of {constant.Name} is too large for a real.");

            case DataType.DoublePrecision when IsNumeric(value):
                return new Constant.DoublePrecision(ToDouble(value));

            case DataType.Logical when value is Constant.Logical:
                return value;

            case DataType.Character when value is Constant.Character text:
                var length = LengthOf(constant, text, at);
                return text.Value.Length == length
                    ? text
                    : new Constant.Character(text.Value.Length > length ? text.Value[..length] : text.Value.PadRight(length));

            default:
                throw at.Error($"{constant.Name} is of type {type.Type}, and its value is of type {value.Type}.");
        }
    }

    private int LengthOf(Symbol constant, Constant.Character value, SourceLocation at)
    {
        switch (constant.Type!.Length)
        {
            // No length given means one character.
            case null:
                return 1;

            // An asterisk takes the length of the value.
            case { IsAssumed: true }:
                return value.Value.Length;

            case var given:
                var length = Evaluate(given.Value!, at) as Constant.Integer
                    ?? throw at.Error($"The length of {constant.Name} is not a constant integer.");
                return length.Value > 0
                    ? length.Value
                    : throw at.Error($"The length of {constant.Name} is not positive.");
        }
    }
}
