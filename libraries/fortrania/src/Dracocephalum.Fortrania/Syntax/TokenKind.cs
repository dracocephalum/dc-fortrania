namespace Dracocephalum.Fortrania.Syntax;

/// <summary>
/// What a token is. There is no kind for a keyword: <c>IF</c>, <c>DO</c> and
/// <c>CALL</c> are a <see cref="Name"/> until the parser says otherwise.
/// </summary>
public enum TokenKind
{
    /// <summary>A run of letters, digits, underscores and dollar signs that starts with a letter.</summary>
    Name,

    /// <summary>An unsigned integer constant.</summary>
    IntegerLiteral,

    /// <summary>A real constant: it has a decimal point, an <c>E</c> exponent, or both.</summary>
    RealLiteral,

    /// <summary>A double precision constant, marked by a <c>D</c> exponent.</summary>
    DoubleLiteral,

    /// <summary>A character constant, quotes included.</summary>
    CharacterLiteral,

    /// <summary><c>.TRUE.</c> or <c>.FALSE.</c>.</summary>
    LogicalLiteral,

    /// <summary><c>+</c>.</summary>
    Plus,

    /// <summary><c>-</c>.</summary>
    Minus,

    /// <summary><c>*</c>.</summary>
    Star,

    /// <summary><c>/</c>.</summary>
    Slash,

    /// <summary><c>**</c>.</summary>
    Power,

    /// <summary><c>//</c>.</summary>
    Concatenate,

    /// <summary><c>(</c>.</summary>
    LeftParenthesis,

    /// <summary><c>)</c>.</summary>
    RightParenthesis,

    /// <summary><c>,</c>.</summary>
    Comma,

    /// <summary><c>=</c>.</summary>
    Assign,

    /// <summary><c>:</c>.</summary>
    Colon,

    /// <summary><c>.EQ.</c>, or <c>==</c> as an extension.</summary>
    Equal,

    /// <summary><c>.NE.</c>, or <c>/=</c> as an extension.</summary>
    NotEqual,

    /// <summary><c>.LT.</c>, or <c>&lt;</c> as an extension.</summary>
    LessThan,

    /// <summary><c>.LE.</c>, or <c>&lt;=</c> as an extension.</summary>
    LessOrEqual,

    /// <summary><c>.GT.</c>, or <c>&gt;</c> as an extension.</summary>
    GreaterThan,

    /// <summary><c>.GE.</c>, or <c>&gt;=</c> as an extension.</summary>
    GreaterOrEqual,

    /// <summary><c>.AND.</c>.</summary>
    And,

    /// <summary><c>.OR.</c>.</summary>
    Or,

    /// <summary><c>.NOT.</c>.</summary>
    Not,

    /// <summary><c>.EQV.</c>.</summary>
    Equivalent,

    /// <summary><c>.NEQV.</c>.</summary>
    NotEquivalent,
}
