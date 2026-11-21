namespace Dracocephalum.Fortrania.Syntax;

/// <summary>
/// One whole statement of fixed-form source: an initial line joined with its
/// continuation lines, carrying the comments that came with it. It is what
/// the lexer reads; nothing after the reader needs to know about columns.
/// </summary>
/// <param name="Label">The statement label from columns 1 to 5, or <see langword="null"/> when there is none.</param>
/// <param name="Text">
/// The statement field, columns 7 to 72 of every line joined in order, with
/// blanks trimmed from both ends. Blanks inside it are kept, because they
/// are significant within a character constant and nowhere else.
/// </param>
/// <param name="Line">The one-based number of the initial line.</param>
/// <param name="LeadingComments">The comment and blank lines that precede the statement, as written.</param>
/// <param name="InteriorComments">The comment and blank lines that sit between its continuation lines, as written.</param>
public sealed record FixedFormStatement(
    int? Label,
    string Text,
    int Line,
    IReadOnlyList<string> LeadingComments,
    IReadOnlyList<string> InteriorComments);
