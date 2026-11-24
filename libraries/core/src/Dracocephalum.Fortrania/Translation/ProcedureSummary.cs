using Dracocephalum.Fortrania.Analysis;
using Dracocephalum.Fortrania.Syntax;

namespace Dracocephalum.Fortrania.Translation;

/// <summary>One procedure of a summarized project, as a caller has to know it.</summary>
/// <param name="Name">The name it is called by, in upper case.</param>
/// <param name="Result">The type of the value it returns; nothing for a subroutine.</param>
/// <param name="Line">The line its statement starts on, for a message that names it.</param>
/// <param name="Arguments">Its dummy arguments, in order.</param>
/// <param name="Deepest">How deep a call of it can go; nothing for a procedure that has no method of its own.</param>
/// <param name="Through">How deep beneath it a procedure it was passed can be called; nothing where it calls none.</param>
internal sealed record ProcedureSummary(
    string Name,
    DataType? Result,
    int Line,
    IReadOnlyList<ArgumentSummary> Arguments,
    StackDepth? Deepest,
    StackDepth? Through)
{
    /// <summary>Gives the procedure as a caller looks it up.</summary>
    /// <returns>Its interface.</returns>
    public ProcedureInterface ToInterface() => new(Name, Result, [.. Arguments.Select(argument => argument.ToDummyArgument())], Line);
}
