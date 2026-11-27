namespace Dracocephalum.Fortrania.Analysis;

/// <summary>
/// What the rest of a program has to know about one program unit, and what
/// that unit asks of the rest. It holds nothing of the unit's text, so the
/// summaries of a whole program can be kept while its units are read one
/// at a time.
/// </summary>
/// <param name="Kind">What the unit is.</param>
/// <param name="Name">Its name, in upper case, or <see langword="null"/> for a main program or a block data subprogram without one.</param>
/// <param name="EntryPoints">
/// The ways into a subroutine or function: its own first statement, then
/// each <c>ENTRY</c> in order. Empty for a main program or block data.
/// </param>
/// <param name="Calls">Every call and function reference to another program unit, in the order written.</param>
public sealed record UnitSummary(
    UnitKind Kind,
    string? Name,
    IReadOnlyList<ProcedureInterface> EntryPoints,
    IReadOnlyList<CallSite> Calls)
{
    /// <summary>
    /// Gets every call and function reference to a procedure the unit was
    /// passed as an argument, in the order written. What each reaches is
    /// known only where the unit itself is called.
    /// </summary>
    public IReadOnlyList<CallSite> CallsThroughArguments { get; init; } = [];
}
