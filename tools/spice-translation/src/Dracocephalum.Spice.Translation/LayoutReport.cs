using System.Globalization;
using Dracocephalum.Fortrania.Translation;

namespace Dracocephalum.Spice.Translation;

/// <summary>
/// Writes what a translation found of the machine its code takes: the stack
/// each procedure can take, the variables placed off their alignment, and
/// the calls that pass a 4-byte number where an 8-byte one is taken.
/// Each goes in a file of tab-separated columns beside the translation, for
/// choosing the stack settings and for answering the 32-bit ARM question;
/// <c>PENDING.md</c> and <c>docs/rules/portability.md</c> say what for.
/// </summary>
internal static class LayoutReport
{
    /// <summary>The file the stack each procedure can take is written to.</summary>
    public const string StackFile = "stack-use.tsv";

    /// <summary>The file the variables off their alignment are written to.</summary>
    public const string UnalignedFile = "unaligned.tsv";

    /// <summary>The file the calls that pass a 4-byte number for an 8-byte one are written to.</summary>
    public const string WidenedFile = "widened.tsv";

    // How many of the deepest procedures of each project are shown.
    private const int Shown = 5;

    /// <summary>Writes both files into a folder, and says in a few lines what they hold.</summary>
    /// <param name="result">A translation that was done.</param>
    /// <param name="folder">The folder the translation's projects are written to.</param>
    /// <returns>The lines that sum the files up, for the console.</returns>
    public static IReadOnlyList<string> Write(TranslationResult result, string folder)
    {
        File.WriteAllLines(
            Path.Combine(folder, StackFile),
            result.StackUse
                .Select(use => Join(use.Project, use.Source, use.Procedure, use.OwnBytes, use.DeepestBytes, use.TakesProcedures, use.IsRecursive, string.Join(" > ", use.DeepestChain)))
                .Prepend("project\tsource\tprocedure\town bytes\tdeepest bytes\ttakes procedures\trecursive\tdeepest chain"));

        File.WriteAllLines(
            Path.Combine(folder, UnalignedFile),
            result.UnalignedPlaces
                .Select(place => Join(place.Project, place.Source, place.Unit, Path.GetFileName(place.File), place.Line, place.Variable, place.Offset, place.Alignment))
                .Prepend("project\tsource\tunit\tfile\tline\tvariable\toffset\talignment"));

        File.WriteAllLines(
            Path.Combine(folder, WidenedFile),
            result.WidenedPlaces
                .Select(place => Join(place.Project, place.Source, place.Unit, Path.GetFileName(place.File), place.Line, place.Variable, place.Procedure, place.Argument))
                .Prepend("project\tsource\tunit\tfile\tline\tvariable\tprocedure\targument"));

        var lines = new List<string>();
        foreach (var project in result.StackUse.GroupBy(use => use.Project))
        {
            lines.Add(Format($"{project.Key}: {project.Count()} procedures, the deepest:"));
            lines.AddRange(project
                .OrderByDescending(use => use.DeepestBytes)
                .ThenBy(use => use.Procedure, StringComparer.Ordinal)
                .Take(Shown)
                .Select(use => Format($"  {use.DeepestBytes,9:N0} bytes  {string.Join(" > ", use.DeepestChain)}{(use.IsRecursive ? " (recursive)" : string.Empty)}")));
        }

        // A 32-bit ARM processor asks a multiple of 4 of any type, so only
        // a variable off that faults there.
        var onArm32 = result.UnalignedPlaces.Count(place => place.Offset % 4 != 0);
        lines.Add(Format($"Off their alignment: {result.UnalignedPlaces.Count} variables, {onArm32} of them off a multiple of 4."));

        // A 4-byte number stays on a multiple of 4 wherever it is passed, so
        // these are slower at worst and never fault.
        lines.Add(Format($"Passed as 8 bytes from a 4-byte number: {result.WidenedPlaces.Count} arguments."));
        lines.Add(Format($"Written: {StackFile}, {UnalignedFile} and {WidenedFile}, beside the projects."));
        return lines;
    }

    private static string Join(params object[] columns) => string.Join('\t', columns.Select(column => Convert.ToString(column, CultureInfo.InvariantCulture)));

    private static string Format(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);
}
