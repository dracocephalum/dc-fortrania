namespace Dracocephalum.Fortrania.Syntax;

/// <summary>The type at the start of a type statement or a function statement.</summary>
/// <param name="Type">The type named.</param>
/// <param name="Length">The length given with it, or <see langword="null"/> when none is.</param>
public sealed record TypeSpecification(DataType Type, LengthSpecification? Length);
