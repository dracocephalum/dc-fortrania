using Dracocephalum.Fortrania.Analysis;
using Dracocephalum.Fortrania.Syntax;

namespace Dracocephalum.Fortrania.Translation;

/// <summary>One dummy argument of a summarized procedure, as a caller has to know it.</summary>
/// <param name="Name">Its name inside the procedure, in upper case.</param>
/// <param name="Kind">What the procedure takes it to be.</param>
/// <param name="Type">Its type; nothing for a subroutine and for a procedure that is only passed on.</param>
/// <param name="Rank">The number of its dimensions; zero when it is not an array.</param>
/// <param name="Uses">
/// What the procedure does with it, counting what the procedures it passes
/// it on to do, however far down: the answer a caller in another project
/// cannot work out, since it reads none of this project's sources.
/// </param>
/// <param name="Shape">What the procedure's own calls show of it when it is a procedure; nothing otherwise.</param>
internal sealed record ArgumentSummary(
    string Name,
    DummyArgumentKind Kind,
    DataType? Type,
    int Rank,
    ArgumentUses Uses,
    ProcedureShape? Shape)
{
    /// <summary>Gives the argument as a caller looks it up, with where it is passed left out, since its uses already count that.</summary>
    /// <returns>The dummy argument.</returns>
    public DummyArgument ToDummyArgument() => new(Name, Kind, Type, Rank, Uses, []) { Shape = Shape };
}
