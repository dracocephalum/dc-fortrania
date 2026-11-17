using Dracocephalum.Fortrania.Syntax;

namespace Dracocephalum.Fortrania.Analysis;

/// <summary>
/// Works out the type of an expression of one program unit, checking on the
/// way that every operator and function is given operands it can take.
/// </summary>
public sealed class ExpressionTyper
{
    private readonly SymbolTable _table;
    private readonly Dictionary<string, int> _statementFunctionArguments;

    /// <summary>Initializes a new instance of the <see cref="ExpressionTyper"/> class.</summary>
    /// <param name="table">The names of the program unit the expressions belong to.</param>
    public ExpressionTyper(SymbolTable table)
    {
        ArgumentNullException.ThrowIfNull(table);

        _table = table;
        _statementFunctionArguments = table.StatementFunctions
            .Select(function => (Expression.Reference)((Statement.Assignment)function.Statement).Target)
            .ToDictionary(target => target.Identifier, target => target.Arguments.Count, StringComparer.Ordinal);
    }

    /// <summary>Works out the type of <paramref name="expression"/>.</summary>
    /// <param name="expression">An expression of the program unit.</param>
    /// <param name="line">The line its statement starts on, for an error message.</param>
    /// <param name="includeFile">The include file that line is in, or <see langword="null"/>.</param>
    /// <returns>The type of the expression's value.</returns>
    /// <exception cref="FortranAnalysisException">
    /// An operand has a type its operator or function cannot take, a name is
    /// not a value, or an array or function is given the wrong number of
    /// subscripts or arguments.
    /// </exception>
    public DataType TypeOf(Expression expression, int line, string? includeFile = null)
    {
        ArgumentNullException.ThrowIfNull(expression);
        return TypeOf(expression, new SourceLocation(line, includeFile));
    }

    internal DataType TypeOf(Expression expression, SourceLocation at) => expression switch
    {
        Expression.Literal literal => literal.Type,
        Expression.Name name => TypeOfName(name, at),
        Expression.Reference reference => TypeOfReference(reference, at),
        Expression.Substring substring => TypeOfSubstring(substring, at),
        Expression.Unary unary => TypeOfUnary(unary, at),
        Expression.Binary binary => TypeOfBinary(binary, at),
        Expression.Parenthesized parenthesized => TypeOf(parenthesized.Inner, at),
        _ => throw new ArgumentOutOfRangeException(nameof(expression)),
    };

    /// <summary>
    /// Types an actual argument, which unlike any other expression may be the
    /// bare name of a procedure; that has no type and gives <see langword="null"/>.
    /// </summary>
    internal DataType? TypeOfArgument(Expression argument, SourceLocation at)
    {
        if (argument is Expression.Name name
            && Find(name.Identifier, at).Kind is not (SymbolKind.Variable or SymbolKind.Constant or SymbolKind.StatementFunction))
        {
            return null;
        }

        return TypeOf(argument, at);
    }

    internal static bool IsNumeric(DataType type) =>
        type is DataType.Integer or DataType.Real or DataType.DoublePrecision or DataType.Complex;

    private static string Describe(DataType type) => type switch
    {
        DataType.Integer => "an integer",
        DataType.Real => "a real",
        DataType.DoublePrecision => "a double precision value",
        DataType.Complex => "a complex value",
        DataType.Logical => "a logical value",
        _ => "a character value",
    };

    private Symbol Find(string name, SourceLocation at) =>
        _table.Find(name) ?? throw at.Error($"{name} is not a name of this program unit.");

    private DataType TypeOfName(Expression.Name name, SourceLocation at)
    {
        var symbol = Find(name.Identifier, at);
        if (symbol.Kind is not (SymbolKind.Variable or SymbolKind.Constant))
        {
            throw at.Error($"{symbol.Name} is used as a value, and it is not a variable or a constant.");
        }

        return symbol.Type!.Type;
    }

    private DataType TypeOfReference(Expression.Reference reference, SourceLocation at)
    {
        var symbol = Find(reference.Identifier, at);
        var count = reference.Arguments.Count;

        switch (symbol.Kind)
        {
            case SymbolKind.Variable when symbol.IsArray:
                if (count != symbol.Dimensions.Count)
                {
                    throw at.Error($"{symbol.Name} has {symbol.Dimensions.Count} dimensions and is given {count} subscripts.");
                }

                foreach (var subscript in reference.Arguments)
                {
                    Expect(subscript, DataType.Integer, $"A subscript of {symbol.Name}", at);
                }

                return symbol.Type!.Type;

            case SymbolKind.StatementFunction:
                if (count != _statementFunctionArguments[symbol.Name])
                {
                    throw at.Error($"{symbol.Name} takes {_statementFunctionArguments[symbol.Name]} arguments and is given {count}.");
                }

                foreach (var argument in reference.Arguments)
                {
                    TypeOf(argument, at);
                }

                return symbol.Type!.Type;

            case SymbolKind.ExternalFunction:
                foreach (var argument in reference.Arguments)
                {
                    TypeOfArgument(argument, at);
                }

                return symbol.Type!.Type;

            case SymbolKind.IntrinsicFunction:
                return TypeOfIntrinsic(reference, at);

            default:
                throw at.Error($"{symbol.Name} is given arguments or subscripts, and it is neither an array nor a function.");
        }
    }

    private DataType TypeOfIntrinsic(Expression.Reference reference, SourceLocation at)
    {
        var intrinsic = Intrinsics.Find(reference.Identifier)!;
        var count = reference.Arguments.Count;
        if (count < intrinsic.MinimumArguments || count > intrinsic.MaximumArguments)
        {
            throw at.Error($"{intrinsic.Name} is given {count} arguments, which it cannot take.");
        }

        var first = TypeOf(reference.Arguments[0], at);
        if (Array.IndexOf(intrinsic.Accepts, first) < 0)
        {
            throw at.Error($"{intrinsic.Name} is given {Describe(first)}, which it cannot take.");
        }

        for (var index = 1; index < count; index++)
        {
            var next = TypeOf(reference.Arguments[index], at);
            if (next != first)
            {
                throw at.Error($"The arguments of {intrinsic.Name} differ in type: {Describe(first)} and {Describe(next)}.");
            }
        }

        if (intrinsic.Result is { } result)
        {
            return result;
        }

        // The one generic function whose result is not its argument's type.
        return intrinsic.Name is "ABS" && first == DataType.Complex ? DataType.Real : first;
    }

    private DataType TypeOfSubstring(Expression.Substring substring, SourceLocation at)
    {
        Expect(substring.Target, DataType.Character, "What a substring is taken of", at);

        if (substring.First is not null)
        {
            Expect(substring.First, DataType.Integer, "The start of a substring", at);
        }

        if (substring.Last is not null)
        {
            Expect(substring.Last, DataType.Integer, "The end of a substring", at);
        }

        return DataType.Character;
    }

    private DataType TypeOfUnary(Expression.Unary unary, SourceLocation at)
    {
        var operand = TypeOf(unary.Operand, at);

        if (unary.Operator == UnaryOperator.Not)
        {
            return operand == DataType.Logical
                ? DataType.Logical
                : throw at.Error($".NOT. is applied to {Describe(operand)}.");
        }

        return IsNumeric(operand)
            ? operand
            : throw at.Error($"A sign is applied to {Describe(operand)}.");
    }

    private DataType TypeOfBinary(Expression.Binary binary, SourceLocation at)
    {
        var left = TypeOf(binary.Left, at);
        var right = TypeOf(binary.Right, at);

        FortranAnalysisException Mismatch() =>
            at.Error($"{binary.Operator} cannot combine {Describe(left)} with {Describe(right)}.");

        switch (binary.Operator)
        {
            case BinaryOperator.Add or BinaryOperator.Subtract or BinaryOperator.Multiply or BinaryOperator.Divide or BinaryOperator.Power:
                return Promote(left, right) ?? throw Mismatch();

            case BinaryOperator.Concatenate:
                return left == DataType.Character && right == DataType.Character ? DataType.Character : throw Mismatch();

            case BinaryOperator.Equal or BinaryOperator.NotEqual:
                return (left == DataType.Character && right == DataType.Character) || Promote(left, right) is not null
                    ? DataType.Logical
                    : throw Mismatch();

            case BinaryOperator.LessThan or BinaryOperator.LessOrEqual or BinaryOperator.GreaterThan or BinaryOperator.GreaterOrEqual:
                var ordered = (left == DataType.Character && right == DataType.Character)
                    || (Promote(left, right) is { } common && common != DataType.Complex);
                return ordered ? DataType.Logical : throw Mismatch();

            default:
                return left == DataType.Logical && right == DataType.Logical ? DataType.Logical : throw Mismatch();
        }
    }

    // The type two numeric operands are brought to, or null when they cannot
    // be combined. FORTRAN 77 has no double precision complex, so it does
    // not let the two meet.
    private static DataType? Promote(DataType left, DataType right)
    {
        if (!IsNumeric(left) || !IsNumeric(right))
        {
            return null;
        }

        var wider = left > right ? left : right;
        var narrower = left > right ? right : left;
        return wider == DataType.Complex && narrower == DataType.DoublePrecision ? null : wider;
    }

    private void Expect(Expression expression, DataType expected, string what, SourceLocation at)
    {
        var actual = TypeOf(expression, at);
        if (actual != expected)
        {
            throw at.Error($"{what} is {Describe(actual)}, and {Describe(expected)} is needed.");
        }
    }
}
