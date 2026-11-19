using System.Text.RegularExpressions;

namespace Dracocephalum.Spice.Translation;

/// <summary>
/// Adds to a translated file what NAIF's rules ask of code made from the
/// toolkit: that it is a machine translation, of which routine and version,
/// that it is not a product of NASA, Caltech/JPL or NAIF and NAIF does not
/// support it, and which terms apply. The lines go inside the file's auto-generated block, after
/// what the translator wrote there, so that the block still opens the file
/// and says that the file is generated.
/// </summary>
internal static partial class ToolkitHeader
{
    /// <summary>The version of the toolkit the translation is made from.</summary>
    public const string ToolkitVersion = "N0067";

    private const string Marker = "// Machine translation";
    private const string End = "// </auto-generated>";

    /// <summary>
    /// Gives a translated file's text with the header added, or the text as
    /// it is where the header is there already, so that running twice
    /// changes nothing.
    /// </summary>
    /// <param name="text">The text of a file the translator wrote.</param>
    /// <param name="changes">
    /// What a later step changed in the file, in words, for the reader; the
    /// toolkit's terms ask for a changed routine to say so. Null where
    /// nothing was changed.
    /// </param>
    /// <returns>The text with the header.</returns>
    /// <exception cref="RewriteException">The text has no auto-generated block to put the header in.</exception>
    public static string AddTo(string text, string? changes = null)
    {
        if (text.Contains(Marker, StringComparison.Ordinal))
        {
            return text;
        }

        var end = text.IndexOf(End, StringComparison.Ordinal);
        if (end < 0)
        {
            throw new RewriteException("The file has no auto-generated block to put the toolkit's header in.");
        }

        var unit = TranslatedUnitRegex().Match(text);
        var lines = new List<string>
        {
            unit.Success
                ? $"// Machine translation of {unit.Groups[1].Value}, from {unit.Groups[2].Value}, of NASA's SPICE Toolkit {ToolkitVersion}."
                : $"// Machine translation of part of NASA's SPICE Toolkit {ToolkitVersion}.",
            "// It is not a product of NASA, of Caltech/JPL or of NAIF, the Navigation and",
            "// Ancillary Information Facility that maintains the toolkit, and NAIF does",
            "// not support it. The toolkit's rules apply to it, and not the licence of",
            "// the repository it is kept in; see LICENSE.md beside the project.",
        };

        if (changes is not null)
        {
            lines.Add("// Changed after translation by Dracocephalum.Spice.Translation: " + changes);
        }

        return text.Insert(end, string.Concat(lines.Select(line => line + "\n")));
    }

    [GeneratedRegex(@"^// Translated from FORTRAN 77 by Fortrania: (\w+), from ([\w.]+)\.$", RegexOptions.Multiline)]
    private static partial Regex TranslatedUnitRegex();
}
