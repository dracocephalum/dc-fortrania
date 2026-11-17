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

    /// <summary>
    /// Splits a keyword off the front of the first token, which the lexer
    /// left joined to whatever followed it: <c>CALLFOO</c> becomes
    /// <c>CALL</c> and <c>FOO</c>, and <c>DO10I</c> becomes <c>DO</c>,
    /// <c>10</c> and <c>I</c>.
    /// </summary>
    /// <param name="length">The length of the keyword; at most the length of the first token, which is a name.</param>
    /// <returns>A statement over the same text, with the first token replaced by the pieces.</returns>
    public TokenizedStatement SplitKeyword(int length)
    {
        var first = Tokens[0];
        if (first.Kind != TokenKind.Name)
        {
            throw new InvalidOperationException("Only a name can have a keyword split off it.");
        }

        ArgumentOutOfRangeException.ThrowIfLessThan(length, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(length, first.Length);

        var tokens = new List<Token>(Tokens.Count + 2) { new(TokenKind.Name, first.Start, length) };
        var rest = first.Start + length;
        var end = first.Start + first.Length;

        // What followed the keyword may open with a statement label.
        var digits = rest;
        while (digits < end && char.IsAsciiDigit(Text[digits]))
        {
            digits++;
        }

        if (digits > rest)
        {
            tokens.Add(new Token(TokenKind.IntegerLiteral, rest, digits - rest));
        }

        if (end > digits)
        {
            tokens.Add(new Token(TokenKind.Name, digits, end - digits));
        }

        tokens.AddRange(Tokens.Skip(1));
        return this with { Tokens = tokens };
    }
}
