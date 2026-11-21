namespace Dracocephalum.Fortrania.Syntax;

/// <summary>
/// A fixed-form source file split into statements. Every physical line of
/// the file is accounted for: it is part of a statement's text, one of a
/// statement's comments, or one of the trailing comments.
/// </summary>
/// <param name="Statements">The statements, in source order.</param>
/// <param name="TrailingComments">The comment and blank lines after the last statement, as written.</param>
public sealed record FixedFormSource(
    IReadOnlyList<FixedFormStatement> Statements,
    IReadOnlyList<string> TrailingComments);
