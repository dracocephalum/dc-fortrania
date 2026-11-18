namespace Dracocephalum.Fortrania.Analysis;

/// <summary>What a procedure takes a dummy argument to be.</summary>
public enum DummyArgumentKind
{
    /// <summary>A single value.</summary>
    Variable,

    /// <summary>An array.</summary>
    Array,

    /// <summary>A function the procedure references.</summary>
    Function,

    /// <summary>A subroutine the procedure calls.</summary>
    Subroutine,

    /// <summary>A procedure it only passes on, so that nothing in it says whether that is a function or a subroutine.</summary>
    Procedure,
}
