namespace Dracocephalum.Fortrania.Translation;

/// <summary>
/// A variable of a translation that <c>EQUIVALENCE</c> places at a byte its
/// type is not aligned on; <c>docs/rules/portability.md</c>, <em>Alignment</em>,
/// says why that matters on some processors.
/// </summary>
/// <param name="Project">The project the unit is translated in.</param>
/// <param name="Source">The source of that project it is read from.</param>
/// <param name="Unit">The program unit, in upper case.</param>
/// <param name="File">The file the unit is read from, by full path.</param>
/// <param name="Variable">The variable, in upper case.</param>
/// <param name="Offset">Where it begins, in bytes from the start of the storage it shares.</param>
/// <param name="Alignment">The alignment its type has, in bytes.</param>
/// <param name="Line">The one-based number of the line the variable is declared on.</param>
public sealed record UnalignedPlace(
    string Project,
    string Source,
    string Unit,
    string File,
    string Variable,
    long Offset,
    int Alignment,
    int Line);
