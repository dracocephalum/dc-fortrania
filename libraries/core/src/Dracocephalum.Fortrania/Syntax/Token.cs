namespace Dracocephalum.Fortrania.Syntax;

/// <summary>
/// One token of a statement, as a position in the statement's normalized
/// text. It holds no text of its own; <see cref="TokenizedStatement.GetText"/>
/// reads it back.
/// </summary>
/// <param name="Kind">What the token is.</param>
/// <param name="Start">The zero-based index of its first character in the normalized text.</param>
/// <param name="Length">How many characters it spans.</param>
public readonly record struct Token(TokenKind Kind, int Start, int Length);
