namespace Dracocephalum.Fortrania.Translation;

/// <summary>What a project of a translation is.</summary>
public enum ProjectKind
{
    /// <summary>An assembly that other code calls.</summary>
    Library,

    /// <summary>A program that starts with one of its main programs.</summary>
    Executable,
}
