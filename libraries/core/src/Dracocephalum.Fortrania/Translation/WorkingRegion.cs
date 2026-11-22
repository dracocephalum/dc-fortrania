namespace Dracocephalum.Fortrania.Translation;

/// <summary>
/// The memory the procedures of one project of a translation need for
/// their working buffers, were those to lie in a block and not on the
/// stack.
/// </summary>
/// <param name="Project">The project.</param>
/// <param name="OwnBytes">
/// The size of the project's own region: the buffers of its deepest chain
/// of calls by name, each procedure at its <see cref="StackUse.Place"/>.
/// </param>
/// <param name="ReachBytes">
/// What is needed when a procedure of the project is the first to be
/// called: its own region and, after it, those of the deepest chain of
/// projects it refers to.
/// </param>
public sealed record WorkingRegion(string Project, long OwnBytes, long ReachBytes);
