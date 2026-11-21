namespace Dracocephalum.Fortrania.Translation;

/// <summary>
/// How a project file that is written refers to the runtime translated
/// code calls. With nothing set it refers to the runtime's package, in the
/// version of the translator that wrote the file.
/// </summary>
public sealed class RuntimeSettings
{
    /// <summary>Gets or sets the path of the runtime's project, to refer to it as a project.</summary>
    public string? Project { get; set; }

    /// <summary>Gets or sets the version of the runtime's package to refer to.</summary>
    public string? Package { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the reference to the runtime's
    /// package is written with no version, for a repository that manages its
    /// package versions centrally.
    /// </summary>
    /// <remarks>
    /// The version is then the one the repository's <c>Directory.Packages.props</c>
    /// gives the package, and it is the repository's to keep it the
    /// translator's own. Where versions are not managed centrally a
    /// reference with no version does not fail: the restore warns and takes
    /// the lowest version there is, so this is never set there.
    /// </remarks>
    public bool CentrallyVersioned { get; set; }
}
