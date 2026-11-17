namespace Dracocephalum.Fortrania.Syntax;

/// <summary>A program unit: everything from its first statement to its <c>END</c>.</summary>
/// <param name="Header">
/// The statement that names it: <see cref="Statement.Program"/>,
/// <see cref="Statement.Subroutine"/>, <see cref="Statement.Function"/> or
/// <see cref="Statement.BlockData"/>. It is <see langword="null"/> for a main
/// program written without a <c>PROGRAM</c> statement.
/// </param>
/// <param name="Body">
/// Everything between the two, in source order. Declarations are not set
/// apart from what executes: which of the two a statement is can depend on
/// what has been declared.
/// </param>
/// <param name="End">The <c>END</c>.</param>
public sealed record ProgramUnit(ParsedStatement? Header, IReadOnlyList<Construct> Body, ParsedStatement End);
