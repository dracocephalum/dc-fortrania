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
    /// Gets or sets the projects to translate, by name; every project of the
    /// settings unless set. A project left out is not read: what the
    /// projects translated need of it is read from the summary its last
    /// translation wrote beside its translated sources, and it is not
    /// written. One left out that has no summary takes no part.
    /// </summary>
    public IReadOnlyCollection<string>? Projects { get; set; }

    /// <summary>Gets or sets how long a download of sources may take; <see cref="DefaultDownloadTimeout"/> unless set.</summary>
    public TimeSpan DownloadTimeout { get; set; } = DefaultDownloadTimeout;

    /// <summary>
    /// Gets or sets what sends the requests that download sources, for a
    /// proxy say; the system's own unless set. It is not disposed by the
    /// translation.
    /// </summary>
    public HttpMessageHandler? HttpHandler { get; set; }
}
