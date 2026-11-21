namespace Dracocephalum.Fortrania.Syntax;

/// <summary>
/// A file meant to be drawn into program units by <c>INCLUDE</c>: statements
/// that belong to no program unit of their own.
/// </summary>
/// <param name="Body">The statements, in source order, with every block closed within the file.</param>
/// <param name="TrailingComments">The comment and blank lines after the last statement, as written.</param>
public sealed record IncludeFile(IReadOnlyList<Construct> Body, IReadOnlyList<string> TrailingComments);
