namespace Dracocephalum.Fortrania.Translation;

/// <summary>One FORTRAN library of a project: its files, and the class they become.</summary>
public sealed class SourceSettings
{
    /// <summary>Gets or sets the source's name, which names the folder its translation is written to.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the class the source becomes; the source's name unless
    /// set. It may not be the name of a part of its namespace.
    /// </summary>
    public string? Class { get; set; }

    /// <summary>
    /// Gets or sets the files to translate, each entry their patterns and
    /// the platform they are for: the source's own where an entry gives
    /// none, and those that stand in for some of them on one platform
    /// where it gives one.
    /// </summary>
    public IList<FileSettings> Files { get; set; } = [];

    /// <summary>
    /// Gets or sets the folders an <c>INCLUDE</c> file is looked for in,
    /// after the folder of the file that includes it.
    /// </summary>
    public IList<string> Includes { get; set; } = [];
}
