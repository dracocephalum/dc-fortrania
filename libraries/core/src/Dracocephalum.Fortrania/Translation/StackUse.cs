namespace Dracocephalum.Fortrania.Translation;

/// <summary>
/// The most a call of one procedure of a translation can take from the
/// stack, over every chain of calls it can start, as far as the
/// translation's own code is concerned.
/// </summary>
/// <remarks>
/// The figures are the translation's own: what its methods take with
/// <c>stackalloc</c>. The just-in-time compiler's frame for each method
/// comes on top, and so do the runtime's own methods. A procedure passed
/// as an argument is counted as though called beneath the deepest call of
/// the procedure that passes it, which makes the figure an upper bound.
/// </remarks>
/// <param name="Project">The project the procedure is translated in.</param>
/// <param name="Source">The source of that project it is read from.</param>
/// <param name="Procedure">The procedure, in upper case.</param>
/// <param name="OwnBytes">What the procedure's own method takes.</param>
/// <param name="DeepestBytes">What the procedure and the deepest chain of calls beneath it take together.</param>
/// <param name="DeepestChain">The procedures of that chain, from this one down, a passed procedure where it is counted.</param>
/// <param name="TakesProcedures">
/// Whether the procedure takes procedures as arguments. A caller from
/// outside the translation adds what those take; a caller inside it is
/// counted with them already.
/// </param>
/// <param name="IsRecursive">
/// Whether a chain beneath it comes back to a procedure already on it,
/// which FORTRAN 77 does not allow; such a chain is counted to where it
/// turns back.
/// </param>
public sealed record StackUse(
    string Project,
    string Source,
    string Procedure,
    long OwnBytes,
    long DeepestBytes,
    IReadOnlyList<string> DeepestChain,
    bool TakesProcedures,
    bool IsRecursive)
{
    /// <summary>
    /// Gets where the procedure's working buffers would start in a region
    /// of its project's own, in place of the stack: after the buffers of
    /// every procedure of the project that calls it by name, directly or
    /// by way of others. Nothing for a procedure on a chain that comes
    /// back to itself, or beneath one, which can be given no such place.
    /// </summary>
    public long? Place { get; init; }
}
