using System.Text.Json;

namespace Dracocephalum.Fortrania.Translation;

/// <summary>
/// Everything one translation produces: the projects, and the FORTRAN
/// sources each is made from. It is what the settings file holds, and what
/// the command line and a build step read into.
/// </summary>
public sealed class TranslationSettings
{
    /// <summary>
    /// Gets or sets the folder the projects are written under, each in a
    /// folder of its own name unless it names another. Like every path in
    /// the settings it is taken from where the settings file lies.
    /// </summary>
    public string Output { get; set; } = ".";

    /// <summary>Gets or sets the target framework of a project file that is written; .NET 10 unless set.</summary>
    public string Framework { get; set; } = "net10.0";

    /// <summary>Gets or sets how a project file that is written refers to the runtime.</summary>
    public RuntimeSettings Runtime { get; set; } = new();

    /// <summary>Gets or sets the projects.</summary>
    public IList<ProjectSettings> Projects { get; set; } = [];

    /// <summary>Reads settings from a file.</summary>
    /// <param name="path">The settings file.</param>
    /// <returns>The settings.</returns>
    /// <exception cref="JsonException">The file is not settings: it is not JSON, or has a property the settings do not.</exception>
    public static TranslationSettings Load(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        return Parse(File.ReadAllText(path));
    }

    /// <summary>Reads settings from their text.</summary>
    /// <param name="json">The text of a settings file.</param>
    /// <returns>The settings.</returns>
    /// <exception cref="JsonException">The text is not settings: it is not JSON, or has a property the settings do not.</exception>
    public static TranslationSettings Parse(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        return JsonSerializer.Deserialize<TranslationSettings>(json, TranslationJson.Default)
            ?? throw new JsonException("The settings are empty.");
    }
}
