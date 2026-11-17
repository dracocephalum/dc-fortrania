namespace Dracocephalum.Fortrania.Syntax;

/// <summary>One name in the list of a type or <c>DIMENSION</c> statement.</summary>
/// <param name="Name">The name declared, in upper case.</param>
/// <param name="Dimensions">Its array bounds, one for each dimension; empty when it is not declared as an array here.</param>
/// <param name="Length">A length of its own, which overrides the statement's, or <see langword="null"/>.</param>
public sealed record DeclaredEntity(
    string Name,
    IReadOnlyList<DimensionBound> Dimensions,
    LengthSpecification? Length);
