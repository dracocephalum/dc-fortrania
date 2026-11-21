using System.Diagnostics.CodeAnalysis;

namespace Dracocephalum.Fortrania.Syntax;

/// <summary>
/// One element of a program unit's body: a statement on its own, or a block
/// with everything inside it. The kinds are nested here and nothing else can
/// derive from this type.
/// </summary>
[SuppressMessage(
    "Naming",
    "CA1716:Identifiers should not match keywords",
    Justification = "Each node is named for the construct it stands for, and an IF and a loop are among them.")]
public abstract record Construct
{
    private Construct()
    {
    }

    /// <summary>A statement that opens and closes nothing.</summary>
    /// <param name="Statement">The statement.</param>
    public sealed record Simple(ParsedStatement Statement) : Construct;

    /// <summary>A block <c>IF</c> from its first line to its <c>END IF</c>.</summary>
    /// <param name="Branches">The arms, in order; there is at least one.</param>
    /// <param name="End">The <c>END IF</c>.</param>
    public sealed record If(IReadOnlyList<IfBranch> Branches, ParsedStatement End) : Construct;

    /// <summary>A loop from its <c>DO</c> to its <c>END DO</c>.</summary>
    /// <param name="Header">The opening statement: <see cref="Statement.Do"/> or <see cref="Statement.DoWhile"/>.</param>
    /// <param name="Body">What the loop repeats, which may be nothing.</param>
    /// <param name="End">The <c>END DO</c>.</param>
    public sealed record Loop(ParsedStatement Header, IReadOnlyList<Construct> Body, ParsedStatement End) : Construct;
}
