namespace Dracocephalum.Fortrania.Translation;

/// <summary>A depth on the stack, and the chain of calls that takes it.</summary>
/// <param name="Bytes">The bytes the chain takes.</param>
/// <param name="Chain">The procedures of the chain, from the top, parted by a space.</param>
/// <param name="IsRecursive">Whether a procedure beneath is reached again beneath itself.</param>
internal sealed record StackDepth(long Bytes, string Chain, bool IsRecursive = false);
