namespace Dracocephalum.Fortrania.Syntax;

/// <summary>A source file as the program units it holds.</summary>
/// <param name="Units">The program units, in source order.</param>
/// <param name="TrailingComments">The comment and blank lines after the last <c>END</c>, as written.</param>
public sealed record SourceFile(IReadOnlyList<ProgramUnit> Units, IReadOnlyList<string> TrailingComments);
