namespace Dracocephalum.Fortrania.Analysis;

/// <summary>
/// What a procedure does with one of its dummy arguments, anywhere in its
/// text. Nothing is said of the order: an argument both read and written
/// may be written first, so that the caller's value never matters.
/// </summary>
[Flags]
public enum ArgumentUses
{
    /// <summary>The argument is never touched.</summary>
    None = 0,

    /// <summary>Its value, or that of one of its elements, is read.</summary>
    Read = 1,

    /// <summary>It, or one of its elements, is given a value.</summary>
    Written = 2,
}
