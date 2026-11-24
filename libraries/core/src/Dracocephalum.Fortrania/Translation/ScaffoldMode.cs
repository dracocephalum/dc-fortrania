namespace Dracocephalum.Fortrania.Translation;

/// <summary>
/// Whether the translator writes the files around a translation: a
/// project's file, and <c>Program.cs</c> for an executable; and, at the
/// root, the solution the settings name.
/// </summary>
public enum ScaffoldMode
{
    /// <summary>The files are the translator's, and written again on every translation.</summary>
    Always,

    /// <summary>A file is written where it is missing, and is left to whoever has it after that.</summary>
    Once,

    /// <summary>
    /// No file is written; they are somebody else's. The translator only
    /// checks that a project file refers to the projects its project calls
    /// into, and that the solution holds every project.
    /// </summary>
    Never,
}
