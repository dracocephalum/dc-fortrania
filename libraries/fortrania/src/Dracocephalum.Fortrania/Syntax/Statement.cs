namespace Dracocephalum.Fortrania.Syntax;

/// <summary>
/// A statement as written in the source, parsed into its parts. The node
/// types are nested here and nothing else can derive from this type, so a
/// <see langword="switch"/> over them is complete.
/// </summary>
public abstract record Statement
{
    private Statement()
    {
    }

    /// <summary>A type statement.</summary>
    /// <param name="Type">The type it gives its names.</param>
    /// <param name="Entities">The names declared.</param>
    public sealed record TypeDeclaration(TypeSpecification Type, IReadOnlyList<DeclaredEntity> Entities) : Statement;

    /// <summary><c>DIMENSION</c>.</summary>
    /// <param name="Entities">The arrays declared, each with its bounds.</param>
    public sealed record Dimension(IReadOnlyList<DeclaredEntity> Entities) : Statement;

    /// <summary><c>PARAMETER</c>.</summary>
    /// <param name="Constants">The constants defined, in order; a later one may use an earlier one.</param>
    public sealed record Parameter(IReadOnlyList<NamedConstant> Constants) : Statement;

    /// <summary><c>IMPLICIT NONE</c>.</summary>
    public sealed record ImplicitNone : Statement;

    /// <summary><c>SAVE</c>.</summary>
    /// <param name="Names">The variables saved; empty means everything in the program unit.</param>
    public sealed record Save(IReadOnlyList<string> Names) : Statement;

    /// <summary><c>EXTERNAL</c>.</summary>
    /// <param name="Names">The procedures named.</param>
    public sealed record External(IReadOnlyList<string> Names) : Statement;

    /// <summary><c>INTRINSIC</c>.</summary>
    /// <param name="Names">The intrinsic functions named.</param>
    public sealed record Intrinsic(IReadOnlyList<string> Names) : Statement;

    /// <summary><c>INCLUDE</c>.</summary>
    /// <param name="FileName">The file named, as written between the quotes.</param>
    public sealed record Include(string FileName) : Statement;
}
