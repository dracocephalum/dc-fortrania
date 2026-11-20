namespace Dracocephalum.Fortrania.Emit;

/// <summary>A program unit as the emitter wrote it: the text, and what the text takes of the machine.</summary>
/// <param name="Text">The text of the C# source file.</param>
/// <param name="Methods">The methods written, one per entry point, in the order written.</param>
/// <param name="UnalignedVariables">The variables placed off their type's alignment, in the order the unit lays them out.</param>
public sealed record EmittedUnit(string Text, IReadOnlyList<EmittedMethod> Methods, IReadOnlyList<UnalignedVariable> UnalignedVariables);
