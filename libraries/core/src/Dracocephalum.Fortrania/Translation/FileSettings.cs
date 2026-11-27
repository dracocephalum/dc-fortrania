using System.Runtime.InteropServices;

namespace Dracocephalum.Fortrania.Translation;

/// <summary>
/// Files of a source, by their paths, and the platform they are for. Files for no platform in particular are the source's own,
/// the base. Files for one platform stand in for the base on it: each
/// program unit of them is a variant of the unit of the same name in the
/// base, which runs everywhere else, and the library chooses between them
/// when it is made, from its context.
/// </summary>
/// <remarks>
/// Files may give an operating system, an architecture, both or neither.
/// Where several of a unit's variants suit one platform, the one that gives
/// both is chosen before one that gives the system, and that before one
/// that gives the architecture; among equals, the one given first.
/// </remarks>
public sealed class FileSettings
{
    /// <summary>
    /// Gets or sets the paths of the files, each of which may name
    /// variables as <c>$(Name)</c>. Any part of one may have <c>*</c> for
    /// any run of characters and <c>?</c> for any one, and a part that is
    /// <c>**</c> stands for any number of folders, none among them; as the
    /// last part, for every file at any depth. The folder before the first
    /// part with a wildcard is the path's root.
    /// </summary>
    public IList<string> Paths { get; set; } = [];

    /// <summary>
    /// Gets or sets the files among those <see cref="Paths"/> finds that are
    /// not to be read: each a glob, as a path is, or a regular expression
    /// after <c>regex:</c>, matched against a file's path from the root of
    /// the path that found it, parts joined by <c>/</c>, without regard to
    /// case. A glob matches that whole path, and a regular expression
    /// anywhere in it. None unless set.
    /// </summary>
    public IList<string> Ignores { get; set; } = [];

    /// <summary>
    /// Gets or sets the operating system the files are for, by the name
    /// .NET gives it, <c>windows</c>, <c>linux</c> or <c>macos</c> say,
    /// compared without regard to case; any system when left
    /// <see langword="null"/>. A name .NET does not know is not refused: the
    /// variant then runs where a context is told it is on that system.
    /// </summary>
    public string? OSPlatform { get; set; }

    /// <summary>
    /// Gets or sets the processor architecture the files are for, that of
    /// the process; any when left <see langword="null"/>.
    /// </summary>
    public Architecture? ProcessArchitecture { get; set; }

    /// <summary>Gets a value indicating whether the files are the source's own, for no platform in particular.</summary>
    internal bool IsBase => OSPlatform is null && ProcessArchitecture is null;
}
