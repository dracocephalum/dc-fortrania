namespace Dracocephalum.Fortrania.Syntax;

/// <summary>
/// The role a single line plays in fixed-form FORTRAN 77 source, as decided by
/// its first six columns alone.
/// </summary>
public enum FixedFormLineKind
{
    /// <summary>A comment line, or a line that is blank in every significant column.</summary>
    Comment,

    /// <summary>The first line of a statement, with an optional label in columns 1 to 5.</summary>
    Initial,

    /// <summary>A line that continues the statement begun by the preceding initial line.</summary>
    Continuation,
}
