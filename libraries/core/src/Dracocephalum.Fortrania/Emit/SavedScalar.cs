namespace Dracocephalum.Fortrania.Emit;

/// <summary>
/// A single number or logical value that a program unit keeps between
/// calls and that no <c>EQUIVALENCE</c> lays another variable over: one
/// <see cref="ScalarLocalPass"/> may keep in a local while a method of the
/// unit runs.
/// </summary>
/// <param name="Name">The name of the pointer to it that a method declares.</param>
/// <param name="Type">The type the pointer points at, as the target language names it.</param>
internal sealed record SavedScalar(string Name, string Type);
