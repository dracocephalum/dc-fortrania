using System.Text;

namespace Dracocephalum.Fortrania.Syntax;

/// <summary>
/// Cuts a statement into tokens. It is the second stage of the front end,
/// after <see cref="FixedFormReader"/> and before the parser.
/// </summary>
/// <remarks>
/// It finds no keywords: <c>DO 10 I = 1, 5</c> gives the name <c>DO10I</c>,
/// for the parser to split (<c>DESIGN.md</c>, <i>Fortrania's lexer finds no
/// keywords</i>). Two things the standard allows are not recognized: a
/// Hollerith constant, and a name starting with <c>E</c> or <c>D</c> directly
/// after a length, as in <c>REAL*8 E1</c>, which reads as the number
/// <c>8E1</c>.
/// </remarks>
public static class Lexer
{
    private const char Apostrophe = '\'';
    private const char Quote = '"';

    /// <summary>Cuts <paramref name="statement"/> into tokens.</summary>
    /// <param name="statement">A whole statement from the reader.</param>
    /// <returns>The tokens, over the statement's normalized text.</returns>
    /// <exception cref="FortranSyntaxException">The statement holds something that is not a token.</exception>
    public static TokenizedStatement Tokenize(FixedFormStatement statement)
    {
        ArgumentNullException.ThrowIfNull(statement);

        var text = Normalize(statement);
        var tokens = new List<Token>();
        var position = 0;

        while (position < text.Length)
        {
            var token = ReadToken(text, position, statement.Line);
            tokens.Add(token);
            position += token.Length;
        }

        return new TokenizedStatement(statement, text, tokens);
    }

    // Removes blanks and upper-cases letters, leaving character constants as written.
    private static string Normalize(FixedFormStatement statement)
    {
        var source = statement.Text;
        var text = new StringBuilder(source.Length);
        var delimiter = '\0';

        for (var index = 0; index < source.Length; index++)
        {
            var character = source[index];

            if (delimiter != '\0')
            {
                text.Append(character);
                if (character != delimiter)
                {
                    continue;
                }

                // A doubled delimiter is one character of the constant, not its end.
                if (index + 1 < source.Length && source[index + 1] == delimiter)
                {
                    text.Append(delimiter);
                    index++;
                }
                else
                {
                    delimiter = '\0';
                }
            }
            else if (character is Apostrophe or Quote)
            {
                delimiter = character;
                text.Append(character);
            }
            else if (character is not (' ' or '\t'))
            {
                text.Append(char.ToUpperInvariant(character));
            }
        }

        if (delimiter != '\0')
        {
            throw new FortranSyntaxException(statement.Line, "A character constant is not closed.");
        }

        return text.ToString();
    }

    private static Token ReadToken(string text, int start, int line)
    {
        var character = text[start];
        var next = start + 1 < text.Length ? text[start + 1] : '\0';

        if (char.IsAsciiLetter(character))
        {
            return ReadName(text, start);
        }

        if (char.IsAsciiDigit(character) || (character == '.' && char.IsAsciiDigit(next)))
        {
            return ReadNumber(text, start);
        }

        return character switch
        {
            '.' => ReadDottedWord(text, start, line),
            Apostrophe or Quote => ReadCharacterConstant(text, start),
            '+' => new Token(TokenKind.Plus, start, 1),
            '-' => new Token(TokenKind.Minus, start, 1),
            '(' => new Token(TokenKind.LeftParenthesis, start, 1),
            ')' => new Token(TokenKind.RightParenthesis, start, 1),
            ',' => new Token(TokenKind.Comma, start, 1),
            ':' => new Token(TokenKind.Colon, start, 1),
            '*' when next == '*' => new Token(TokenKind.Power, start, 2),
            '*' => new Token(TokenKind.Star, start, 1),
            '/' when next == '/' => new Token(TokenKind.Concatenate, start, 2),
            '/' when next == '=' => new Token(TokenKind.NotEqual, start, 2),
            '/' => new Token(TokenKind.Slash, start, 1),
            '=' when next == '=' => new Token(TokenKind.Equal, start, 2),
            '=' => new Token(TokenKind.Assign, start, 1),
            '<' when next == '=' => new Token(TokenKind.LessOrEqual, start, 2),
            '<' => new Token(TokenKind.LessThan, start, 1),
            '>' when next == '=' => new Token(TokenKind.GreaterOrEqual, start, 2),
            '>' => new Token(TokenKind.GreaterThan, start, 1),
            _ => throw new FortranSyntaxException(line, $"'{character}' is not part of any token."),
        };
    }

    private static Token ReadName(string text, int start)
    {
        var end = start + 1;
        while (end < text.Length && (char.IsAsciiLetterOrDigit(text[end]) || text[end] is '_' or '$'))
        {
            end++;
        }

        return new Token(TokenKind.Name, start, end - start);
    }

    private static Token ReadNumber(string text, int start)
    {
        var kind = TokenKind.IntegerLiteral;
        var end = SkipDigits(text, start);

        // "1.EQ.2" is an integer and an operator; "1.E2" and "1." are reals.
        if (end < text.Length && text[end] == '.' && FindDottedWordEnd(text, end) < 0)
        {
            kind = TokenKind.RealLiteral;
            end = SkipDigits(text, end + 1);
        }

        // An exponent needs at least one digit, or the letter belongs to what follows.
        if (end < text.Length && text[end] is 'E' or 'D')
        {
            var digits = end + 1;
            if (digits < text.Length && text[digits] is '+' or '-')
            {
                digits++;
            }

            if (digits < text.Length && char.IsAsciiDigit(text[digits]))
            {
                kind = text[end] == 'D' ? TokenKind.DoubleLiteral : TokenKind.RealLiteral;
                end = SkipDigits(text, digits);
            }
        }

        return new Token(kind, start, end - start);
    }

    private static int SkipDigits(string text, int index)
    {
        while (index < text.Length && char.IsAsciiDigit(text[index]))
        {
            index++;
        }

        return index;
    }

    // Given the index of a period, returns the index just past ".WORD." when
    // the period starts a dotted operator or logical constant, and -1 when it
    // does not.
    private static int FindDottedWordEnd(string text, int period)
    {
        var end = period + 1;
        while (end < text.Length && char.IsAsciiLetter(text[end]))
        {
            end++;
        }

        if (end == period + 1 || end >= text.Length || text[end] != '.')
        {
            return -1;
        }

        return ClassifyDottedWord(text.AsSpan(period + 1, end - period - 1)) is null ? -1 : end + 1;
    }

    private static Token ReadDottedWord(string text, int start, int line)
    {
        var end = FindDottedWordEnd(text, start);
        if (end < 0)
        {
            throw new FortranSyntaxException(line, "A period starts neither a number nor a known operator.");
        }

        // FindDottedWordEnd only succeeds for a word ClassifyDottedWord knows.
        var kind = ClassifyDottedWord(text.AsSpan(start + 1, end - start - 2)) ?? TokenKind.Name;
        return new Token(kind, start, end - start);
    }

    private static TokenKind? ClassifyDottedWord(ReadOnlySpan<char> word) => word switch
    {
        "TRUE" or "FALSE" => TokenKind.LogicalLiteral,
        "EQ" => TokenKind.Equal,
        "NE" => TokenKind.NotEqual,
        "LT" => TokenKind.LessThan,
        "LE" => TokenKind.LessOrEqual,
        "GT" => TokenKind.GreaterThan,
        "GE" => TokenKind.GreaterOrEqual,
        "AND" => TokenKind.And,
        "OR" => TokenKind.Or,
        "NOT" => TokenKind.Not,
        "EQV" => TokenKind.Equivalent,
        "NEQV" => TokenKind.NotEquivalent,
        _ => null,
    };

    // Normalize has already checked that the constant is closed.
    private static Token ReadCharacterConstant(string text, int start)
    {
        var delimiter = text[start];
        var end = start + 1;

        while (true)
        {
            if (text[end] != delimiter)
            {
                end++;
            }
            else if (end + 1 < text.Length && text[end + 1] == delimiter)
            {
                end += 2;
            }
            else
            {
                return new Token(TokenKind.CharacterLiteral, start, end + 1 - start);
            }
        }
    }
}
