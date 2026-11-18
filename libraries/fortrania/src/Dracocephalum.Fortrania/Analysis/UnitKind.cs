namespace Dracocephalum.Fortrania.Analysis;

/// <summary>What a program unit is.</summary>
public enum UnitKind
{
    /// <summary>A main program, with or without a <c>PROGRAM</c> statement.</summary>
    MainProgram,

    /// <summary>A subroutine.</summary>
    Subroutine,

    /// <summary>A function.</summary>
    Function,

    /// <summary>A block data subprogram.</summary>
    BlockData,
}
