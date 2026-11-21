namespace Dracocephalum.Fortrania.Syntax;

/// <summary>One definition in a <c>PARAMETER</c> statement.</summary>
/// <param name="Name">The constant's name, in upper case.</param>
/// <param name="Value">The constant expression that gives its value.</param>
public sealed record NamedConstant(string Name, Expression Value);
