using System.Text.Json;
using System.Text.Json.Serialization;

namespace Dracocephalum.Fortrania.Translation;

/// <summary>
/// One translation, as a settings file describes it: FORTRAN sources, how
/// they are translated, and the folder the translation is written to. It is
/// what the settings file holds, and what the command line and a build step
/// read into. What the translation is compiled as, its assembly and project
/// file, is not the settings' to say.
/// </summary>
/// <remarks>
/// A run translates one translation or several, each from settings of its
/// own; a translation calls into the others of the run, and into those the
/// assemblies it is translated against carry. Several may be compiled into
/// one assembly.
/// </remarks>
public sealed class TranslationSettings
{
    /// <summary>
    /// Gets or sets the folder the settings file lies in, which every path
    /// of the settings is taken from. <see cref="Load"/> sets it; settings
    /// made otherwise are to be given one before they are translated.
    /// </summary>
    [JsonIgnore]
    public string? Folder { get; set; }

    /// <summary>
    /// Gets or sets the translation's name, which every translation has:
    /// what it goes by to the translations that call into it, the name of
    /// its summary, the default of its namespace, and the name of the
    /// project file a scaffold writes for it.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>Gets or sets the namespace of the translation's classes; its name unless set.</summary>
    public string? Namespace { get; set; }

    /// <summary>
    /// Gets or sets the folder the translation is written to,
    /// taken like every path of the settings from <see cref="Folder"/>. It
    /// has no default, so that nothing is written to a folder nobody named:
    /// <c>"."</c> is the folder of the settings file itself.
    /// </summary>
    public string? Output { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the translation is made
    /// again where what it is made from changed; true unless set. False
    /// leaves what was translated before as it is, committed say, and reads
    /// the translation from its summary, unless the run is forced.
    /// </summary>
    public bool Translate { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether each character constant is
    /// written where it is used, as a literal of its own. Unless set, a
    /// library keeps its constants in one block and a method reaches each
    /// by its place there, with the constant beside it as a note. That is
    /// what keeps a method with hundreds of constants fast and its frame
    /// small; written where they are used, the constants read as the
    /// FORTRAN does, which suits a small program.
    /// </summary>
    public bool InlineTextConstants { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether a <c>DO</c> loop that only
    /// copies one array to another, or gives every element of one the same
    /// value, is written as the loop alone. Unless set, such a loop has the
    /// runtime's copy or fill beside it, taken where the run is long: that
    /// is several times as fast for a long array and makes the code some
    /// lines longer at each such loop.
    /// </summary>
    public bool PlainArrayLoops { get; set; }

    /// <summary>
    /// Gets or sets the variables a path may name as <c>$(Name)</c>, names
    /// compared without regard to case. Their values are the last of three
    /// places a value is looked for: the local settings file beside this
    /// one comes first, then the environment. A variable may also say where
    /// to download the folder it names, when there is none.
    /// </summary>
    public IDictionary<string, VariableSettings> Variables { get; set; } = new Dictionary<string, VariableSettings>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets or sets the values the local settings file gave the variables,
    /// which come before any other. That file is read by <see cref="Load"/>
    /// and holds what belongs to one machine, where the sources are on it
    /// above all, so it is not to be shared.
    /// </summary>
    [JsonIgnore]
    public IDictionary<string, string> LocalVariables { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Gets or sets the FORTRAN libraries the translation is made from, each a class.</summary>
    public IList<SourceSettings> Sources { get; set; } = [];

    /// <summary>
    /// Gets or sets the commands run once the whole run has been written,
    /// in order, each when the one before has ended well. A command runs
    /// in the folder of the settings file, and finds in its environment
    /// <c>FORTRANIA_SETTINGS</c>, that file's folder,
    /// <c>FORTRANIA_OUTPUT</c>, the folder the translation is written to, and
    /// <c>FORTRANIA_WRITTEN</c>, a file that lists every file of the
    /// translation that was written, a full path a line. A command that
    /// changes a translated file keeps its first line, which says it was
    /// generated.
    /// </summary>
    public IList<CommandSettings> OnSuccess { get; set; } = [];

    /// <summary>Reads settings from a file, and the local settings file beside it where there is one.</summary>
    /// <param name="path">The settings file.</param>
    /// <returns>The settings, with <see cref="Folder"/> the file's folder.</returns>
    /// <exception cref="JsonException">The file is not settings: it is not JSON, or has a property the settings do not.</exception>
    public static TranslationSettings Load(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        var settings = Parse(File.ReadAllText(path));
        settings.Folder = Path.GetDirectoryName(Path.GetFullPath(path));

        var local = LocalFileOf(path);
        if (File.Exists(local))
        {
            var values = JsonSerializer.Deserialize<LocalSettings>(File.ReadAllText(local), TranslationJson.Default)
                ?? throw new JsonException("The local settings are empty.");
            settings.LocalVariables = new Dictionary<string, string>(values.Variables, StringComparer.OrdinalIgnoreCase);
        }

        return settings;
    }

    /// <summary>Gives the local settings file that belongs to a settings file: the same name with <c>.local</c> before its extension.</summary>
    /// <param name="path">The settings file.</param>
    /// <returns>The local settings file, which need not exist.</returns>
    public static string LocalFileOf(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        return Path.Combine(Path.GetDirectoryName(path) ?? string.Empty, Path.GetFileNameWithoutExtension(path) + ".local" + Path.GetExtension(path));
    }

    /// <summary>Reads settings from their text.</summary>
    /// <param name="json">The text of a settings file.</param>
    /// <returns>The settings, with no <see cref="Folder"/>.</returns>
    /// <exception cref="JsonException">The text is not settings: it is not JSON, or has a property the settings do not.</exception>
    public static TranslationSettings Parse(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        var settings = JsonSerializer.Deserialize<TranslationSettings>(json, TranslationJson.Default)
            ?? throw new JsonException("The settings are empty.");

        // What is read is an ordinary dictionary; names are compared
        // without regard to case.
        settings.Variables = new Dictionary<string, VariableSettings>(settings.Variables, StringComparer.OrdinalIgnoreCase);
        return settings;
    }
}
