namespace Dracocephalum.Fortrania.Translation;

/// <summary>One project of a translation: an assembly, made of one or more sources.</summary>
public sealed class ProjectSettings
{
    /// <summary>Gets or sets the project's name, which names its folder, its project file and its assembly.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the namespace of the project's classes; the project's name unless set.</summary>
    public string? Namespace { get; set; }

    /// <summary>Gets or sets the folder the project is written to; a folder of its name under the settings' output unless set.</summary>
    public string? Output { get; set; }

    /// <summary>
    /// Gets or sets what the project is. Unless set it follows from its
    /// sources: an executable when one of them has a main program, and a
    /// library otherwise.
    /// </summary>
    public ProjectKind? Kind { get; set; }

    /// <summary>
    /// Gets or sets the main program an executable starts with, by its
    /// name. It has to be set where the sources have more than one.
    /// </summary>
    public string? Entry { get; set; }

    /// <summary>
    /// Gets or sets the names of the projects this one calls into. Unless
    /// set they are worked out from the calls; set, they are checked
    /// against them.
    /// </summary>
    public IList<string>? References { get; set; }

    /// <summary>Gets or sets the FORTRAN libraries the project is made from, each a class.</summary>
    public IList<SourceSettings> Sources { get; set; } = [];
}
