namespace Dracocephalum.Fortrania.Emit;

/// <summary>
/// A source file of the program tree: one program unit, as its part of the
/// class its library is.
/// </summary>
/// <param name="UnitName">The program unit translated.</param>
/// <param name="SourceFile">The file it was read from, by name alone, where that is known.</param>
/// <param name="Namespace">The namespace of the class.</param>
/// <param name="ClassName">The class the members are a part of.</param>
/// <param name="RuntimeClass">The class of the runtime whose methods translated code calls, named in full.</param>
/// <param name="RuntimeAlias">The name the file gives that class, where its methods are called by it; nothing where they are called by their own names.</param>
/// <param name="SuppressedWarnings">The warnings of the target language's compiler that translated code is known to draw and that mean nothing in it.</param>
/// <param name="Members">The fields and methods, in the order they are written.</param>
/// <param name="TrailingComments">What the source said after the unit's end, which follows the last member.</param>
internal sealed record TreeFile(
    string UnitName,
    string? SourceFile,
    string Namespace,
    string ClassName,
    string RuntimeClass,
    string? RuntimeAlias,
    IReadOnlyList<string> SuppressedWarnings,
    IReadOnlyList<TreeMember> Members,
    IReadOnlyList<TreeStatement> TrailingComments)
{
    /// <summary>Gets the platform the unit is written for, in words, where it is one of its variants; nothing otherwise.</summary>
    public string? Variant { get; init; }
}
