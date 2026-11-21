using Dracocephalum.Fortrania.Syntax;

namespace Dracocephalum.Fortrania.Analysis;

/// <summary>
/// One place a program unit calls a subroutine or references a function that
/// another program unit defines.
/// </summary>
/// <param name="Name">The procedure, in upper case.</param>
/// <param name="ResultType">The type the calling unit gives the function, or <see langword="null"/> for a <c>CALL</c>.</param>
/// <param name="Arguments">The actual arguments, in order.</param>
/// <param name="Line">The one-based number of the line the statement starts on.</param>
public sealed record CallSite(string Name, DataType? ResultType, IReadOnlyList<ActualArgument> Arguments, int Line)
{
    /// <summary>Gets a value indicating whether this references a function, and is not a <c>CALL</c>.</summary>
    public bool IsFunctionReference => ResultType is not null;
}
