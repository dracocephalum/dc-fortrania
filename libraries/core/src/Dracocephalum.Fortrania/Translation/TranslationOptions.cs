using Dracocephalum.Fortrania.Plugins;

namespace Dracocephalum.Fortrania.Translation;

/// <summary>
/// How one run of a translation is to go, as against what it translates,
/// which the settings say: among it what the translations are compiled as,
/// where the translator is to write the files around them.
/// </summary>
public sealed class TranslationOptions
{
    /// <summary>How long a download of sources may take, whole, before it is given up; it waits on a server that may never answer.</summary>
    public static readonly TimeSpan DefaultDownloadTimeout = TimeSpan.FromMinutes(30);

    /// <summary>The target framework of a project file the translator writes, unless the options give another.</summary>
    public const string DefaultFramework = "net10.0";

    /// <summary>
    /// Gets or sets a value indicating whether every translated file is
    /// written again and listed as written for the commands after the
    /// translation, whether or not its translation changed.
    /// </summary>
    public bool Force { get; set; }

    /// <summary>
    /// Gets or sets when the sources the settings say where to download
    /// from are downloaded: where they are not there, the default, never, or
    /// on every run.
    /// </summary>
    public FetchPolicy Fetch { get; set; }

    /// <summary>
    /// Gets or sets what a translation may call into, by paths: an
    /// assembly compiled from a translation, carrying its summary, as a
    /// build's project and package references are, or a summary file,
    /// one ending in <c>.json</c>, for a project not built yet. An assembly
    /// that carries none is passed over. A path that is not full is taken
    /// from the current directory.
    /// </summary>
    public IReadOnlyCollection<string>? References { get; set; }

    /// <summary>
    /// Gets or sets plugins that take part in the run beside those of the
    /// assemblies the options and the settings name; none unless set. The
    /// plugins of a run are called in the order each gives, and where two
    /// give the same, these first and then those of each assembly in the
    /// order named.
    /// </summary>
    public IReadOnlyList<ITranslationPlugin> Plugins { get; set; } = [];

    /// <summary>
    /// Gets or sets the assemblies whose plugins take part in the run, by
    /// paths, before those the settings name. A path that is not full is
    /// taken from the current directory.
    /// </summary>
    public IReadOnlyCollection<string>? PluginAssemblies { get; set; }

    /// <summary>
    /// Gets or sets the namespace of the classes of every project of the
    /// run, over what their settings say. Unless set, each source has the
    /// one its settings give, or else its project's name.
    /// </summary>
    public string? Namespace { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the files around a
    /// project's translation are written where they are not there yet: a
    /// project file of its name in its folder, the folder of its summary,
    /// referring to the projects it calls into, and a <c>Program.cs</c>
    /// where it has a main program, which makes it an executable. Once
    /// written they are left to whoever has them. None is written unless
    /// set, the translation then being somebody's project's to compile.
    /// </summary>
    public bool Scaffold { get; set; }

    /// <summary>
    /// Gets or sets the main program the <c>Program.cs</c> of a scaffold
    /// starts with, by its name. It has to be set where a project
    /// scaffolded has more than one.
    /// </summary>
    public string? Entry { get; set; }

    /// <summary>Gets or sets the target framework of a project file a scaffold writes; <see cref="DefaultFramework"/> unless set.</summary>
    public string Framework { get; set; } = DefaultFramework;

    /// <summary>Gets or sets how a project file a scaffold writes refers to the runtime.</summary>
    public RuntimeSettings Runtime { get; set; } = new();

    /// <summary>Gets or sets how long a download of sources may take; <see cref="DefaultDownloadTimeout"/> unless set.</summary>
    public TimeSpan DownloadTimeout { get; set; } = DefaultDownloadTimeout;

    /// <summary>
    /// Gets or sets what sends the requests that download sources, for a
    /// proxy say; the system's own unless set. It is not disposed by the
    /// translation.
    /// </summary>
    public HttpMessageHandler? HttpHandler { get; set; }
}
