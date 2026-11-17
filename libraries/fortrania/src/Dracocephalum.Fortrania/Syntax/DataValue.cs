namespace Dracocephalum.Fortrania.Syntax;

/// <summary>One item in the list of values of a <c>DATA</c> statement.</summary>
/// <param name="Repeat">How many successive targets take the value, as in <c>3*0.0D0</c>; <see langword="null"/> means one.</param>
/// <param name="Value">The value: a constant, a named constant, or either with a sign.</param>
public sealed record DataValue(Expression? Repeat, Expression Value);
