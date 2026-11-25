// SPDX-License-Identifier: Apache-2.0
// Part of the Fortrania runtime, which code translated from FORTRAN 77 calls.

#nullable enable

namespace Dracocephalum.Fortrania.Runtime;

/// <summary>
/// Where the procedures of one translated project take their working
/// buffers in one context: from the stack, or from an address onwards in
/// the arena, each procedure at the place its translation gave it. A
/// translated library is handed one when it is made and reads it on entry
/// to a procedure that has buffers; nothing else has a use for it.
/// </summary>
/// <remarks>
/// What it says changes: when the arena lays its regions out again, and
/// whenever a copy of the arena is opened or closed. It is therefore read
/// on entry each time, and never kept.
/// </remarks>
#if FORTRANIA_RUNTIME_INTERNAL
internal
#else
public
#endif
sealed unsafe class BufferRegion
{
    internal BufferRegion(string project, long ownBytes, long reachBytes, string[] refersTo, bool isOnStack)
    {
        Project = project;
        OwnBytes = ownBytes;
        ReachBytes = reachBytes;
        RefersTo = refersTo;
        IsOnStack = isOnStack;
        CurrentSource = isOnStack ? BufferSource.Stack : BufferSource.None;
    }

    /// <summary>Gets what a procedure of the project does on entry to take its buffers.</summary>
    public BufferSource CurrentSource { get; internal set; }

    /// <summary>Gets where the project's buffers start in the copy of the arena now open, while <see cref="CurrentSource"/> is <see cref="BufferSource.Arena"/>.</summary>
    public byte* Start { get; internal set; }

    internal string Project { get; }

    // The most the libraries of the project made so far have asked for:
    // each asks for what its own procedures and those it calls into need.
    internal long OwnBytes { get; set; }

    internal long ReachBytes { get; set; }

    internal string[] RefersTo { get; }

    // Whether the project's buffers are on the stack for good, as the
    // context or its translation chose; it then takes no part in the arena.
    internal bool IsOnStack { get; }

    // Where the region starts in every copy of the arena; -1 until it is
    // first laid out.
    internal long Base { get; set; } = -1;
}
