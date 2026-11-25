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
    /// Gets or sets a value indicating whether every translation is made,
    /// those whose settings say not to translate among them, and every
    /// translated file written again and listed as written for the commands
    /// after the translation, whether or not its translation changed.
    /// </summary>
    public bool Force { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the run reaches no server:
    /// nothing is downloaded, and a translation whose sources are not on
    /// the machine is read from the summary it wrote last, as one that says
    /// not to translate is. One that has no summary then fails. A build
    /// runs so, and fetches in its restore.
    /// </summary>
    public bool Offline { get; set; }

    /// <summary>
    /// Gets or sets assemblies a translation may call into, by their paths:
    /// each one with translations compiled into it, carrying their
    /// summaries, as a build's project and package references are. An
    /// assembly that carries none is passed over. A path that is not full
    /// is taken from the current directory.
    /// </summary>
    public IReadOnlyCollection<string>? References { get; set; }

    /// <summary>
    /// Gets or sets the namespace of the classes of every translation of
    /// the run, over what their settings say. Unless set, each has the one
    /// its settings give, or else its name.
    /// </summary>
    public string? Namespace { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the files around a
    /// translation are written where they are not there yet: a project
    /// file of the translation's name in its folder, referring to the
    /// projects of the translations it calls into, and a
    /// <c>Program.cs</c> where the translation has a main program, which
    /// makes it an executable. Once written they are left to whoever has
    /// them. None is written unless set, the translation then being
    /// somebody's project's to compile.
    /// </summary>
    public bool Scaffold { get; set; }

    /// <summary>
    /// Gets or sets the main program the <c>Program.cs</c> of a scaffold
    /// starts with, by its name. It has to be set where a translation
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
