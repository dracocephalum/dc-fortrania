namespace Dracocephalum.Fortrania.Translation;

/// <summary>
/// When a project is translated, after the values MSBuild has for copying a
/// file to the output.
/// </summary>
public enum TranslateMode
{
    /// <summary>
    /// Only when asked for by name or forced: what was translated before is
    /// left as it is, even where the sources are there, and the project is
    /// read from its summary.
    /// </summary>
    Never,

    /// <summary>
    /// When the settings, a source or the translator's version changed; a
    /// file whose translation did not change is not written again.
    /// </summary>
    IfChanged,

    /// <summary>Every time, every file written again whether it changed or not.</summary>
    Always,
}
