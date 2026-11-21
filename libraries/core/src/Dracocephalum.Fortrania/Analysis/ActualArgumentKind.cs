namespace Dracocephalum.Fortrania.Analysis;

/// <summary>What a call passes as one of its arguments.</summary>
public enum ActualArgumentKind
{
    /// <summary>
    /// A value with nowhere to store another: a constant, named or not, or
    /// any expression that is more than a variable.
    /// </summary>
    Value,

    /// <summary>A variable that is not an array, or a substring of one.</summary>
    Variable,

    /// <summary>An element of an array, or a substring of one.</summary>
    ArrayElement,

    /// <summary>A whole array.</summary>
    Array,

    /// <summary>The name of a procedure.</summary>
    Procedure,
}
