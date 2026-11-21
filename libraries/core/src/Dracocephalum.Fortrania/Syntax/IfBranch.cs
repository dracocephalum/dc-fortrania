namespace Dracocephalum.Fortrania.Syntax;

/// <summary>One arm of a block <c>IF</c>: the statement that opens it and what it guards.</summary>
/// <param name="Header">
/// The opening statement: <see cref="Statement.BlockIf"/> for the first arm,
/// then <see cref="Statement.ElseIf"/>, and <see cref="Statement.Else"/> for
/// at most the last.
/// </param>
/// <param name="Body">What the arm holds, which may be nothing.</param>
public sealed record IfBranch(ParsedStatement Header, IReadOnlyList<Construct> Body);
