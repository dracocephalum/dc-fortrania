using Dracocephalum.Fortrania.Syntax;

namespace Dracocephalum.Fortrania.Analysis;

/// <summary>
/// One dummy argument of a procedure, as a caller has to know it. What is
/// done with it is told for the statements its entry point reaches: those
/// from the entry on, through any later <c>ENTRY</c>, to the first
/// <c>RETURN</c> or <c>STOP</c> that stands in no block.
/// </summary>
/// <param name="Name">Its name inside the procedure, in upper case.</param>
/// <param name="Kind">What the procedure takes it to be.</param>
/// <param name="Type">Its type; <see langword="null"/> for a subroutine and for a procedure that is only passed on.</param>
/// <param name="Rank">The number of its dimensions; zero when it is not an array.</param>
/// <param name="Uses">
/// What those statements themselves do with it. Passing it to a procedure
/// that is itself a dummy argument counts as both reading and writing, since
/// nothing more can be known; passing it to any other procedure counts as
/// nothing here and is listed in <paramref name="PassedTo"/>.
/// </param>
/// <param name="PassedTo">
/// Where those statements pass it, or an element or substring of it, on to
/// a procedure of another program unit, each place once.
/// <see cref="ArgumentEffects"/> follows these.
/// </param>
public sealed record DummyArgument(
    string Name,
    DummyArgumentKind Kind,
    DataType? Type,
    int Rank,
    ArgumentUses Uses,
    IReadOnlyList<ArgumentPass> PassedTo)
{
    /// <summary>
    /// Gets what the unit's own calls show of the argument when it is a
    /// procedure: <see langword="null"/> for a variable or an array, for
    /// a procedure the unit only passes on, and for one that two of its
    /// calls take differently.
    /// </summary>
    public ProcedureShape? Shape { get; init; }
}
