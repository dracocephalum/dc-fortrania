using Dracocephalum.Fortrania.Syntax;

namespace Dracocephalum.Fortrania.Analysis;

/// <summary>What the three statements that open a way into a procedure have in common.</summary>
internal static class EntryStatementExtensions
{
    /// <summary>
    /// Gives the name a <c>SUBROUTINE</c>, <c>FUNCTION</c> or <c>ENTRY</c>
    /// statement is called by and its dummy arguments, in order.
    /// </summary>
    public static (string Name, IReadOnlyList<string> Parameters) ProcedureHeading(this Statement entry) => entry switch
    {
        Statement.Subroutine subroutine => (subroutine.Name, subroutine.Parameters),
        Statement.Function function => (function.Name, function.Parameters),
        Statement.Entry other => (other.Name, other.Parameters),
        _ => throw new ArgumentOutOfRangeException(nameof(entry)),
    };
}
