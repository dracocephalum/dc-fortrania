namespace Dracocephalum.Fortrania.Emit;

/// <summary>A procedure of the program a method calls, with the procedures it is passed there.</summary>
/// <param name="Name">The procedure called, in upper case.</param>
/// <param name="Passes">The procedures of the program passed to it as arguments, over all the method's calls of it, each once, in upper case and in ordinal order.</param>
/// <param name="PassesOnArgument">Whether a procedure the method was passed itself is passed on to it, at any of the calls.</param>
public sealed record EmittedCall(string Name, IReadOnlyList<string> Passes, bool PassesOnArgument);
