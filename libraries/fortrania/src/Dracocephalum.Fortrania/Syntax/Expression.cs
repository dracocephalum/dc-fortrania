namespace Dracocephalum.Fortrania.Syntax;

/// <summary>
/// An expression as written in the source. The node types are nested here and
/// nothing else can derive from this type, so a <see langword="switch"/> over
/// them is complete.
/// </summary>
/// <remarks>
/// A node records syntax only. <see cref="Reference"/> in particular is a name
/// followed by a parenthesized list, which FORTRAN writes the same way for a
/// function call and for an array element.
/// </remarks>
public abstract record Expression
{
    private Expression()
    {
    }

    /// <summary>A constant.</summary>
    /// <param name="Type">Its type.</param>
    /// <param name="Text">Its text as normalized by the lexer; a character constant keeps its quotes.</param>
    public sealed record Literal(DataType Type, string Text) : Expression;

    /// <summary>A name standing alone: a variable, a named constant, or an array or function passed whole.</summary>
    /// <param name="Identifier">The name, in upper case.</param>
    public sealed record Name(string Identifier) : Expression;

    /// <summary>A name followed by a parenthesized list: a function call or an array element.</summary>
    /// <param name="Identifier">The name, in upper case.</param>
    /// <param name="Arguments">The expressions in the list, which may be empty.</param>
    public sealed record Reference(string Identifier, IReadOnlyList<Expression> Arguments) : Expression;

    /// <summary>A substring of a character variable or array element.</summary>
    /// <param name="Target">What the substring is taken from: a <see cref="Name"/> or a <see cref="Reference"/>.</param>
    /// <param name="First">The position of the first character, or <see langword="null"/> when omitted.</param>
    /// <param name="Last">The position of the last character, or <see langword="null"/> when omitted.</param>
    public sealed record Substring(Expression Target, Expression? First, Expression? Last) : Expression;

    /// <summary>An operator applied to one operand.</summary>
    /// <param name="Operator">The operator.</param>
    /// <param name="Operand">Its operand.</param>
    public sealed record Unary(UnaryOperator Operator, Expression Operand) : Expression;

    /// <summary>An operator applied to two operands.</summary>
    /// <param name="Operator">The operator.</param>
    /// <param name="Left">The left operand.</param>
    /// <param name="Right">The right operand.</param>
    public sealed record Binary(BinaryOperator Operator, Expression Left, Expression Right) : Expression;

    /// <summary>
    /// An expression in parentheses. It is kept as a node because FORTRAN
    /// requires a parenthesized expression to be evaluated as written, which
    /// matters for floating-point results.
    /// </summary>
    /// <param name="Inner">The expression inside.</param>
    public sealed record Parenthesized(Expression Inner) : Expression;
}
