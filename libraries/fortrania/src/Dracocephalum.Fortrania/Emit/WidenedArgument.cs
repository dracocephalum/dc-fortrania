namespace Dracocephalum.Fortrania.Emit;

/// <summary>
/// A variable of a 4-byte type passed where the procedure called takes a
/// <c>DOUBLE PRECISION</c> argument, which the procedure then reads at an
/// address that is a multiple of 4 and may be no multiple of 8.
/// </summary>
/// <param name="Variable">The variable passed, or the array an element of which is, in upper case.</param>
/// <param name="Procedure">The procedure called, in upper case.</param>
/// <param name="Argument">The one-based position of the argument.</param>
/// <param name="Line">The one-based number of the line the call is on.</param>
public sealed record WidenedArgument(string Variable, string Procedure, int Argument, int Line);
