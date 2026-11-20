using System.Globalization;
using Dracocephalum.Fortrania.Translation;

namespace Dracocephalum.Spice.Translation;

/// <summary>
/// Writes what a translation found of the machine its code takes: the stack
/// each procedure can take, and the variables placed off their alignment.
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
        lines.Add(Format($"Written: {StackFile} and {UnalignedFile}, beside the projects."));
        return lines;
    }

    private static string Join(params object[] columns) => string.Join('\t', columns.Select(column => Convert.ToString(column, CultureInfo.InvariantCulture)));

    private static string Format(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);
}
