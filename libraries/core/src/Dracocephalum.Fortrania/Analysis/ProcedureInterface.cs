using Dracocephalum.Fortrania.Syntax;

namespace Dracocephalum.Fortrania.Analysis;

/// <summary>
/// One way into a subroutine or function, as a caller has to know it: the
/// procedure's own first statement, or one of its <c>ENTRY</c> statements.
/// </summary>
/// <param name="Name">The name it is called by, in upper case.</param>
/// <param name="ResultType">The type of the value it returns, or <see langword="null"/> when it is a subroutine.</param>
/// <param name="Arguments">Its dummy arguments, in order.</param>
/// <param name="Line">The one-based number of the line its statement starts on.</param>
public sealed record ProcedureInterface(string Name, DataType? ResultType, IReadOnlyList<DummyArgument> Arguments, int Line)
{
    /// <summary>Gets a value indicating whether this is a function, referenced for its value, and not a subroutine, called.</summary>
    public bool IsFunction => ResultType is not null;
}
