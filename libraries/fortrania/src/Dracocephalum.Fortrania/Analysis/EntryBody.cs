using Dracocephalum.Fortrania.Syntax;

namespace Dracocephalum.Fortrania.Analysis;

/// <summary>One way into a subroutine or function, with the statements control passes through from it.</summary>
/// <param name="Entry">The <c>SUBROUTINE</c>, <c>FUNCTION</c> or <c>ENTRY</c> statement.</param>
/// <param name="Statements">
/// The statements of the program unit, blocks and loops whole, from the one
/// after <paramref name="Entry"/> to the first <c>RETURN</c> or <c>STOP</c>
/// that stands in no block, that one included, or to the unit's end. An
/// <c>ENTRY</c> statement passed on the way is left out. For the unit's own
/// first statement they begin with the unit's declarations.
/// </param>
/// <param name="StrayArguments">
/// The dummy arguments those statements use that are another entry point's
/// and not this one's, each once, in the order met. The language forbids
/// such a use, since nothing was passed for the argument; programs have
/// them all the same, in branches never taken.
/// </param>
public sealed record EntryBody(ParsedStatement Entry, IReadOnlyList<Construct> Statements, IReadOnlyList<string> StrayArguments)
{
    /// <summary>Gets the name the entry point is called by, in upper case.</summary>
    public string Name => EntryLayout.Heading(Entry.Statement).Name;

    /// <summary>Gets the entry point's dummy arguments, in order.</summary>
    public IReadOnlyList<string> Parameters => EntryLayout.Heading(Entry.Statement).Parameters;
}
