namespace Dracocephalum.Fortrania.Emit;

/// <summary>
/// What a library says of its project's working buffers when it is made
/// over a context: the region the context is to lay out for it.
/// </summary>
/// <param name="Project">The project's name, which is what tells projects apart to a context.</param>
/// <param name="OwnBytes">The size of the project's own region.</param>
/// <param name="ReachBytes">What a call into the project can need: its own region and the deepest chain of those it refers to.</param>
/// <param name="RefersTo">The names of the projects it calls into.</param>
public sealed record LibraryRegion(string Project, long OwnBytes, long ReachBytes, IReadOnlyList<string> RefersTo);
