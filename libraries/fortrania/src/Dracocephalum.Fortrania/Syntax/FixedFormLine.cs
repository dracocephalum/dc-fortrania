namespace Dracocephalum.Fortrania.Syntax;

/// <summary>
/// Classifies physical lines of fixed-form FORTRAN 77 source, for
/// <see cref="FixedFormReader"/> to group into statements.
/// </summary>
/// <remarks>
/// Only columns 1 to 72 are significant; anything beyond is the historical
/// sequence field and is ignored. Tab-form source is not recognized.
/// </remarks>
public static class FixedFormLine
{
    /// <summary>The last column the standard treats as part of a statement.</summary>
    public const int LastSignificantColumn = 72;

    // Zero-based index of column 6, the continuation column.
    private const int ContinuationIndex = 5;

    /// <summary>Decides what kind of line <paramref name="line"/> is.</summary>
    /// <param name="line">One physical source line, without its line terminator.</param>
    /// <returns>The kind of the line.</returns>
    public static FixedFormLineKind Classify(ReadOnlySpan<char> line)
    {
        if (line.Length > LastSignificantColumn)
        {
            line = line[..LastSignificantColumn];
        }

        if (line.IsEmpty)
        {
            return FixedFormLineKind.Comment;
        }

        // 'C' and '*' are the standard's comment markers. Lower-case 'c' and
        // '!' are extensions every compiler of the sources we translate accepts.
        if (line[0] is 'C' or 'c' or '*' or '!')
        {
            return FixedFormLineKind.Comment;
        }

        if (line.IndexOfAnyExcept(' ') < 0)
        {
            return FixedFormLineKind.Comment;
        }

        // A continuation line has blanks in columns 1 to 5 and any character
        // other than blank or zero in column 6. A zero there marks an initial
        // line, which is why it is excluded.
        if (line.Length > ContinuationIndex
            && line[ContinuationIndex] is not (' ' or '0')
            && line[..ContinuationIndex].IndexOfAnyExcept(' ') < 0)
        {
            return FixedFormLineKind.Continuation;
        }

        return FixedFormLineKind.Initial;
    }
}
