namespace Dracocephalum.Fortrania.Emit;

/// <summary>
/// What a library says of its project's working buffers when it is made
/// over a context: the region the context is to lay out for it, which
/// grows to the most any library of the project made so far asks for.
/// </summary>
/// <param name="Project">The project's name, which is what tells projects apart to a context.</param>
/// <param name="OwnBytes">What the library needs of its project's region: the buffers of its procedures and of those of the project they call.</param>
/// <param name="ReachBytes">What a call into the library can need: what it needs of its region and the deepest chain of the projects it calls into.</param>
/// <param name="RefersTo">The names of the projects it calls into.</param>
public sealed record LibraryRegion(string Project, long OwnBytes, long ReachBytes, IReadOnlyList<string> RefersTo);
