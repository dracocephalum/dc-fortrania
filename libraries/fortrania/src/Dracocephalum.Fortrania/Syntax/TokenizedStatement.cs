namespace Dracocephalum.Fortrania.Syntax;

/// <summary>
/// A statement after lexing: the tokens the parser reads, over the text they
/// were cut from.
/// </summary>
/// <param name="Source">The statement as the reader produced it, for its label, line and comments.</param>
/// <param name="Text">
/// The statement text with every blank removed and every letter upper-cased,
/// except inside character constants, which are kept exactly as written.
/// Tokens index into this text, not into the source's.
/// </param>
/// <param name="Tokens">The tokens, in order.</param>
public sealed record TokenizedStatement(
    FixedFormStatement Source,
    string Text,
    IReadOnlyList<Token> Tokens)
{
    /// <summary>Reads the characters a token spans.</summary>
    /// <param name="token">A token of this statement.</param>
    /// <returns>The token's text; for a character constant, with its quotes.</returns>
    public ReadOnlySpan<char> GetText(Token token) => Text.AsSpan(token.Start, token.Length);
}
