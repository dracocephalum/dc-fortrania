namespace Dracocephalum.Fortrania.Translation;

/// <summary>
/// One project of a run: the assembly a translation is compiled into, by
/// its name, and the settings files of what it is made of, all of them one
/// translation. It is what the command line and a build step say of the
/// project, as against the settings, which say what the FORTRAN is.
/// </summary>
/// <remarks>
/// The settings files of one project are translated as one: their sources
/// call each other freely and share one region of working buffers, and the
/// project has one summary. A run may hold several projects, which call
/// into each other as they would apart.
/// </remarks>
public sealed class TranslationProject
{
    /// <summary>
    /// Gets or sets the project's name, which every project has: its
    /// assembly's name, which a build gives it. It is what other
    /// translations know it by, the name of its summary and of the region
    /// of working buffers its code registers at run time, and its
    /// namespace where neither the options nor the settings give one.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>Gets or sets the settings of what the project is made of, each with the folder it lies in.</summary>
    public IList<TranslationSettings> Settings { get; set; } = [];

    /// <summary>
    /// Gets or sets the file the project's summary is kept in, beside its
    /// project file; one that is not full is taken from the current
    /// directory. Unless set it is <c>&lt;name&gt;.fortrania-summary.json</c>
    /// in the folder of the first settings file. Its folder is the
    /// project's, where a scaffold writes the project file.
    /// </summary>
    public string? Summary { get; set; }
}
