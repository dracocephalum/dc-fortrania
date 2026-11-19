namespace Dracocephalum.Fortrania.Translation;

/// <summary>
/// How a project file that is written refers to the runtime translated
/// code calls. With neither set it refers to the runtime's package, in the
/// version of the translator that wrote the file.
/// </summary>
public sealed class RuntimeSettings
{
    /// <summary>Gets or sets the path of the runtime's project, to refer to it as a project.</summary>
    public string? Project { get; set; }

    /// <summary>Gets or sets the version of the runtime's package to refer to.</summary>
    public string? Package { get; set; }
}
