namespace Dracocephalum.Fortrania.Emit;

/// <summary>
/// A variable that <c>EQUIVALENCE</c> places at a byte its type is not
/// aligned on, which some processors cannot read as it stands.
/// </summary>
/// <param name="Name">The variable, in upper case.</param>
/// <param name="Offset">Where it begins, in bytes from the start of the storage it shares.</param>
/// <param name="Alignment">The alignment its type has, in bytes, which the offset is not a multiple of.</param>
/// <param name="Line">The one-based number of the line the variable is declared on.</param>
public sealed record UnalignedVariable(string Name, long Offset, int Alignment, int Line);
