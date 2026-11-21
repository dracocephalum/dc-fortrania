namespace Dracocephalum.Fortrania.Translation;

/// <summary>
/// A call of a translation that passes a variable of a 4-byte type where
/// the procedure takes a <c>DOUBLE PRECISION</c> argument, which may then
/// be read off a multiple of 8; <c>docs/rules/portability.md</c>,
/// <em>Alignment</em>, says why that matters.
/// </summary>
/// <param name="Project">The project the calling unit is translated in.</param>
/// <param name="Source">The source of that project it is read from.</param>
/// <param name="Unit">The calling program unit, in upper case.</param>
/// <param name="File">The file the unit is read from, by full path.</param>
/// <param name="Variable">The variable passed, or the array an element of which is, in upper case.</param>
/// <param name="Procedure">The procedure called, in upper case.</param>
/// <param name="Argument">The one-based position of the argument.</param>
/// <param name="Line">The one-based number of the line the call is on.</param>
public sealed record WidenedPlace(
    string Project,
    string Source,
    string Unit,
    string File,
    string Variable,
    string Procedure,
    int Argument,
    int Line);
