using Dracocephalum.Fortrania.Syntax;

namespace Dracocephalum.Fortrania.Analysis;

/// <summary>
/// Writes an expression as one line of text that is the same for two
/// expressions written alike, whatever the spacing of the source: enough to
/// tell that two subscripts name the same element, and nothing more. Two
/// that are written differently may still have one value.
/// </summary>
internal static class ExpressionKey
{
    /// <summary>Gives the text of an expression.</summary>
    /// <param name="expression">The expression.</param>
    public static string Of(Expression expression) => expression switch
    {
        Expression.Literal literal => literal.Text,
        Expression.Name name => name.Identifier,
        Expression.Reference reference => $"{reference.Identifier}({Of(reference.Arguments)})",
        Expression.Substring substring => $"{Of(substring.Target)}({(substring.First is null ? string.Empty : Of(substring.First))}:{(substring.Last is null ? string.Empty : Of(substring.Last))})",
        Expression.Unary unary => $"{unary.Operator} {Of(unary.Operand)}",
        Expression.Binary binary => $"{Of(binary.Left)} {binary.Operator} {Of(binary.Right)}",
        Expression.Parenthesized inner => $"({Of(inner.Inner)})",
        _ => throw new ArgumentOutOfRangeException(nameof(expression)),
    };

    /// <summary>Gives the text of several expressions, as the subscripts of an element are.</summary>
    /// <param name="expressions">The expressions, in order.</param>
    public static string Of(IReadOnlyList<Expression> expressions) => string.Join(",", expressions.Select(Of));
}
