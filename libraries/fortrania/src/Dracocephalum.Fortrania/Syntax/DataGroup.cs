namespace Dracocephalum.Fortrania.Syntax;

/// <summary>
/// One <c>names / values /</c> pair of a <c>DATA</c> statement. The values are
/// given to the targets in order, an array or an implied loop taking as many
/// as it has elements.
/// </summary>
/// <param name="Targets">What is initialized.</param>
/// <param name="Values">What it is initialized with.</param>
public sealed record DataGroup(IReadOnlyList<DataTarget> Targets, IReadOnlyList<DataValue> Values);
