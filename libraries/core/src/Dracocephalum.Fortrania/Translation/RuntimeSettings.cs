namespace Dracocephalum.Fortrania.Translation;

/// <summary>
/// How a project file that is written refers to the runtime translated
/// code calls. With nothing set it refers to the runtime's package, in the
/// version of the translator that wrote the file.
/// </summary>
public sealed class RuntimeSettings
{
    /// <summary>
    /// Gets or sets the path of the runtime's project, to refer to it as a
    /// project; one that is not full is taken from the current directory.
    /// </summary>
    public string? Project { get; set; }

    /// <summary>Gets or sets the version of the runtime's package to refer to.</summary>
    public string? Package { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the reference to the runtime's
    /// package is written with no version.
    /// </summary>
    /// <remarks>
    /// The version then has to come from somewhere else, and it is the
    /// user's to keep it the translator's own. Central package management
    /// is the usual case: it refuses a reference that carries a version,
    /// and names the version in <c>Directory.Packages.props</c>. Where
    /// nothing gives one, the restore does not fail: it warns and takes the
    /// lowest version there is.
    /// </remarks>
    public bool OmitPackageVersion { get; set; }
}
