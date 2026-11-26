namespace Dracocephalum.Fortrania.Translation;

/// <summary>
/// When a run downloads the sources a variable of the settings says where to
/// download from: a folder the settings give, and not one the local settings
/// file or the environment gives, which is somebody's own.
/// </summary>
public enum FetchPolicy
{
    /// <summary>Where the folder is not there, and never again once it is.</summary>
    Once,

    /// <summary>
    /// Never: nothing is downloaded, and a project whose sources are not on
    /// the machine is compiled as it was translated last, where what that
    /// wrote is all there. One that was never translated then fails.
    /// </summary>
    Never,

    /// <summary>
    /// On every run, over a folder that is there: what a fetch made is
    /// replaced, once the new download is checked and expanded. A folder no
    /// fetch made is refused and not touched.
    /// </summary>
    Always,
}
