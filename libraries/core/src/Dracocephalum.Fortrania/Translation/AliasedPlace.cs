namespace Dracocephalum.Fortrania.Translation;

/// <summary>
/// A call of a translation that passes one variable, or two parts of one
/// array, as two arguments of a procedure that gives one of them a value.
/// FORTRAN 77 forbids it, and a method that keeps its scalar arguments in
/// locals relies on that; the procedure's two arguments are therefore left
/// behind their addresses, and the call is listed here.
/// </summary>
/// <param name="Project">The project the calling unit is translated in.</param>
/// <param name="Source">The source of that project it is read from.</param>
/// <param name="Unit">The calling program unit, in upper case.</param>
/// <param name="File">The file the unit is read from, by full path.</param>
/// <param name="Variable">The variable or array passed twice, in upper case.</param>
/// <param name="Procedure">The procedure called, in upper case.</param>
/// <param name="FirstArgument">The one-based position of the first of the two arguments.</param>
/// <param name="SecondArgument">The one-based position of the second.</param>
/// <param name="Line">The one-based number of the line the call starts on.</param>
public sealed record AliasedPlace(
    string Project,
    string Source,
    string Unit,
    string File,
    string Variable,
    string Procedure,
    int FirstArgument,
    int SecondArgument,
    int Line);
