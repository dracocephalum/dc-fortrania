using System.Runtime.InteropServices;

namespace Dracocephalum.Fortrania.Translation;

/// <summary>
/// The files of a source that take the place of its own on one platform:
/// a copy of a routine as a package for that platform has it. Each program
/// unit of them is a variant of the unit of the same name among the
/// source's files, which is the base and runs everywhere else; the library
/// chooses between them when it is made, from its context.
/// </summary>
/// <remarks>
/// A variant gives an operating system, an architecture or both. Where
/// several of a unit's variants suit one platform, the one that gives both
/// is chosen before one that gives the system, and that before one that
/// gives the architecture; among equals, the one given first.
/// </remarks>
public sealed class VariantSettings
{
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

    /// <summary>
    /// Gets or sets the files, each a path as a source's own are given,
    /// whose last part may have <c>*</c> and <c>?</c>.
    /// </summary>
    public IList<string> Files { get; set; } = [];
}
