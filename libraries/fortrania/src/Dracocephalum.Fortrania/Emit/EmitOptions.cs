namespace Dracocephalum.Fortrania.Emit;

/// <summary>What the emitted code is to be called.</summary>
/// <param name="Namespace">The namespace of the emitted class.</param>
/// <param name="ClassName">
/// The class every program unit becomes methods of. It is emitted as a
/// partial class, one file per program unit.
/// </param>
public sealed record EmitOptions(string Namespace, string ClassName);
