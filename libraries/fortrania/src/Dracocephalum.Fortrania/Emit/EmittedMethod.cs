namespace Dracocephalum.Fortrania.Emit;

/// <summary>One method the emitter wrote for a program unit: an entry point, or the unit itself.</summary>
/// <param name="Name">The procedure the method is, in upper case, as the program calls it.</param>
/// <param name="StackBytes">
/// The bytes the method takes from the stack for its arrays, character
/// values and shared storage, those of its statement functions included.
/// The just-in-time compiler's own frame comes on top and is not counted.
/// </param>
/// <param name="Calls">
/// The procedures of the program the method calls, its statement
/// functions' calls included, each once, in ordinal order of their names.
/// The compiler's library is the runtime's, and not among them.
/// </param>
/// <param name="CallsThroughArgument">Whether the method calls a procedure it was passed, which is whatever its caller gave it.</param>
public sealed record EmittedMethod(string Name, long StackBytes, IReadOnlyList<EmittedCall> Calls, bool CallsThroughArgument);
