using System.Diagnostics.CodeAnalysis;

namespace Dracocephalum.Fortrania.Syntax;

/// <summary>The type a constant in the source has.</summary>
[SuppressMessage(
    "Naming",
    "CA1720:Identifier contains type name",
    Justification = "The members are named for the FORTRAN types they stand for, and INTEGER is one of them.")]
public enum LiteralKind
{
    /// <summary>An integer constant.</summary>
    Integer,

    /// <summary>A real constant.</summary>
    Real,

    /// <summary>A double precision constant.</summary>
    DoublePrecision,

    /// <summary>A character constant.</summary>
    Character,

    /// <summary>A logical constant.</summary>
    Logical,
}
