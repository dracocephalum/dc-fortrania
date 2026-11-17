using System.Text;

namespace Dracocephalum.Fortrania.Syntax;

/// <summary>
/// Turns the physical lines of fixed-form FORTRAN 77 source into whole
/// statements, joining continuation lines and keeping every comment with the
/// statement it belongs to, so that a translation can carry the comments
/// across. It reads columns and nothing else; what a statement means is the
/// lexer's and the parser's business.
/// </summary>
public static class FixedFormReader
{
    // Zero-based index of column 7, where the statement field starts.
    private const int StatementFieldIndex = 6;

    // Columns 1 to 5 hold the label.
    private const int LabelFieldLength = 5;

    private const char Apostrophe = '\'';

    /// <summary>Reads every line from <paramref name="reader"/> and groups the lines into statements.</summary>
    /// <param name="reader">The source text.</param>
    /// <returns>The statements and comments of the source.</returns>
    /// <exception cref="FixedFormSyntaxException">The source breaks a rule of the fixed source form.</exception>
    public static FixedFormSource Read(TextReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);

        var statements = new List<FixedFormStatement>();
        var pendingComments = new List<string>();
        Builder? current = null;
        var lineNumber = 0;

        while (reader.ReadLine() is { } line)
        {
            lineNumber++;

            switch (FixedFormLine.Classify(line))
            {
                case FixedFormLineKind.Comment:
                    pendingComments.Add(line.TrimEnd());
                    break;

                case FixedFormLineKind.Initial:
                    if (current is not null)
                    {
                        statements.Add(current.Build());
                    }

                    current = new Builder(ParseLabel(line, lineNumber), lineNumber, pendingComments);
                    pendingComments = [];
                    current.Append(line);
                    break;

                case FixedFormLineKind.Continuation:
                    if (current is null)
                    {
                        throw new FixedFormSyntaxException(lineNumber, "A continuation line has no statement to continue.");
                    }

                    // Comments seen since the previous line of this statement
                    // sit inside it, and stay with it.
                    current.InteriorComments.AddRange(pendingComments);
                    pendingComments.Clear();
                    current.Append(line);
                    break;
            }
        }

        if (current is not null)
        {
            statements.Add(current.Build());
        }

        return new FixedFormSource(statements, pendingComments);
    }

    private static int? ParseLabel(string line, int lineNumber)
    {
        var field = line.AsSpan(0, Math.Min(line.Length, LabelFieldLength));
        int? label = null;

        foreach (var character in field)
        {
            if (character == ' ')
            {
                continue;
            }

            if (!char.IsAsciiDigit(character))
            {
                throw new FixedFormSyntaxException(lineNumber, $"The label field holds '{character}', which is not a digit.");
            }

            label = ((label ?? 0) * 10) + (character - '0');
        }

        return label;
    }

    /// <summary>The statement being assembled from an initial line and its continuation lines.</summary>
    private sealed class Builder(int? label, int line, List<string> leadingComments)
    {
        private readonly StringBuilder _text = new();

        // True while the text so far ends inside a character constant.
        private bool _inCharacterConstant;

        public List<string> InteriorComments { get; } = [];

        public void Append(string line)
        {
            var significant = line.AsSpan(0, Math.Min(line.Length, FixedFormLine.LastSignificantColumn));
            if (significant.Length <= StatementFieldIndex)
            {
                return;
            }

            var field = significant[StatementFieldIndex..];
            _text.Append(field);

            // A doubled apostrophe inside a constant toggles twice, which
            // leaves the state where it was, so no special case is needed.
            foreach (var character in field)
            {
                if (character == Apostrophe)
                {
                    _inCharacterConstant = !_inCharacterConstant;
                }
            }

            // A line is 72 columns wide however little of it was typed. When
            // a character constant runs on to the next line, the blanks up to
            // column 72 are part of the constant, as compilers treat them.
            if (_inCharacterConstant && significant.Length < FixedFormLine.LastSignificantColumn)
            {
                _text.Append(' ', FixedFormLine.LastSignificantColumn - significant.Length);
            }
        }

        public FixedFormStatement Build()
        {
            var text = _text.ToString();
            text = _inCharacterConstant ? text.TrimStart() : text.Trim();
            return new FixedFormStatement(label, text, line, leadingComments, InteriorComments);
        }
    }
}
