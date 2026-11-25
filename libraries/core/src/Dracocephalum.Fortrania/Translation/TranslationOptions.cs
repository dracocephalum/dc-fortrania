namespace Dracocephalum.Fortrania.Translation;

/// <summary>How one run of a translation is to go, as against what it translates, which the settings say.</summary>
public sealed class TranslationOptions
{
    /// <summary>How long a download of sources may take, whole, before it is given up; it waits on a server that may never answer.</summary>
    public static readonly TimeSpan DefaultDownloadTimeout = TimeSpan.FromMinutes(30);

    /// <summary>
    /// Gets or sets a value indicating whether every translated file is
    /// written again, and listed as written for the commands after the
    /// translation, whether or not its translation changed.
    /// </summary>
    public bool Force { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the run reaches no server:
    /// nothing is downloaded, and a project to translate whose sources are
    /// not on the machine is read from the summary its last translation
    /// wrote, as one that says never to translate is. One that has no
    /// summary then fails. A build runs so, and fetches in its restore.
    /// </summary>
    public bool Offline { get; set; }

    /// <summary>
    /// Gets or sets assemblies a translation may call into, by their paths:
    /// each one a translation of another project compiled, carrying that
    /// project's summary, as a build's project and package references do.
    /// An assembly that carries none is passed over. A path that is not
    /// full is taken from the current directory.
    /// </summary>
    public IReadOnlyCollection<string>? References { get; set; }

    /// <summary>Gets or sets how long a download of sources may take; <see cref="DefaultDownloadTimeout"/> unless set.</summary>
    public TimeSpan DownloadTimeout { get; set; } = DefaultDownloadTimeout;

    /// <summary>
    /// Gets or sets what sends the requests that download sources, for a
    /// proxy say; the system's own unless set. It is not disposed by the
    /// translation.
    /// </summary>
    public HttpMessageHandler? HttpHandler { get; set; }
}
