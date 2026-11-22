using Dracocephalum.Fortrania.Syntax;

namespace Dracocephalum.Fortrania.Emit;

/// <content>
/// Carries the source's comments into the translation, each where it stands
/// in the source as nearly as C# allows. A comment line becomes a <c>// </c>
/// line at the indentation of the code it comes before, with the text after
/// the comment character less the indentation its block shares, so that what
/// is lined up in the block stays lined up; a blank line stays a blank line,
/// and several in a row are one.
/// </content>
/// <remarks>
/// Declarations are not written where they stand: a unit's variables are
/// declared at the top of each method that names them. So the comments up
/// to a unit's first executable statement, its header among them, go at the
/// top of the method, before what it declares, and the comments before the
/// statement that names the procedure go before the method. Comments of an
/// include file are the include file's, and are not carried into the units
/// that include it.
/// </remarks>
internal sealed partial class UnitEmitter
{
    // The comments of what comes before the first executable statement of
    // the method being written, and whether that statement has been met.
    private CodeWriter _preamble = new();
    private bool _isInPreamble;

    // Writes comment lines as they were read, blank lines among them. A
    // blank line that would open what the writer holds, or follow an opening
    // brace or another blank line, is left out; a block of comments that
    // follows code has one before it.
    private static void WriteComments(ILineWriter writer, IEnumerable<string> lines)
    {
        // The text after the comment character, or null for a blank line.
        var texts = lines.Select(line => IsBlankComment(line) ? null : line[1..].TrimEnd()).ToList();
        var isBlankNeedless = writer.IsEmpty || writer.LastLine is "" or "{";
        var start = 0;
        while (start < texts.Count)
        {
            if (texts[start] is null)
            {
                if (!isBlankNeedless)
                {
                    writer.Blank();
                    isBlankNeedless = true;
                }

                start++;
                continue;
            }

            if (!isBlankNeedless && !writer.LastLine.StartsWith("//", StringComparison.Ordinal))
            {
                writer.Blank();
            }

            var end = start;
            while (end < texts.Count && texts[end] is not null)
            {
                end++;
            }

            WriteBlock(writer, texts[start..end].ConvertAll(text => text!));
            isBlankNeedless = false;
            start = end;
        }
    }

    // Writes a block of comment lines with the indentation they share taken
    // off, which in the source stood for the code's own; what is lined up
    // within the block stays lined up.
    private static void WriteBlock(ILineWriter writer, List<string> texts)
    {
        var shared = texts.Where(text => text.Length > 0)
            .Select(text => text.Length - text.TrimStart(' ').Length)
            .DefaultIfEmpty(0)
            .Min();
        foreach (var text in texts)
        {
            writer.Line(text.Length == 0 ? "//" : "// " + text[shared..]);
        }
    }

    // The lines of a block of comments that comes before a closing brace,
    // with the blank lines that would end it left out.
    private static IEnumerable<string> WithoutTrailingBlanks(IEnumerable<string> lines) =>
        lines.Reverse().SkipWhile(IsBlankComment).Reverse();

    // A line whose first column is blank is a blank line, whatever lies
    // beyond the columns that count.
    private static bool IsBlankComment(string line) => line.Length == 0 || line[0] is ' ' or '\t';

    private static IEnumerable<string> CommentsOf(ParsedStatement statement) =>
        statement.Source.LeadingComments.Concat(statement.Source.InteriorComments);

    // A statement's comments, where the statement's code goes: at the top of
    // the method while nothing executable has been met, before the code
    // from then on.
    private void WriteCommentsOf(ParsedStatement statement, bool isExecutable)
    {
        if (_isInPreamble && !isExecutable)
        {
            WriteComments(_preamble, CommentsOf(statement));
            return;
        }

        _isInPreamble = false;
        WriteComments(_body, CommentsOf(statement));
    }

    // Whether a statement does something when run, as against declaring
    // something. ENTRY is neither, and starts another method.
    private bool IsExecutable(Statement statement) => statement switch
    {
        Statement.Assignment assignment => !IsStatementFunctionDefinition(assignment),
        Statement.Call or Statement.LogicalIf or Statement.Return or Statement.Stop or Statement.Continue
            or Statement.Read or Statement.Write or Statement.Print or Statement.Open or Statement.Close
            or Statement.Inquire or Statement.Rewind or Statement.Backspace or Statement.EndFile => true,
        _ => false,
    };
}
