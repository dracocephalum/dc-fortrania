namespace Dracocephalum.Fortrania.Syntax;

/// <summary>A parsed statement together with where it came from.</summary>
/// <param name="Statement">The statement's parts.</param>
/// <param name="Source">The statement as the reader produced it, for its label, line and comments.</param>
public sealed record ParsedStatement(Statement Statement, FixedFormStatement Source);
