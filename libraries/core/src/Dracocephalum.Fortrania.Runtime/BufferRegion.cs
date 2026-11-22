// SPDX-License-Identifier: Apache-2.0
// Part of the Fortrania runtime, which code translated from FORTRAN 77 calls.

#nullable enable

namespace Dracocephalum.Fortrania.Runtime;

/// <summary>
/// Where the procedures of one translated project keep their working
/// buffers in one context: on the stack, or from an address onwards, each
/// procedure at the place its translation gave it. A translated library
/// is handed one when it is made and reads it on entry to a procedure
/// that has buffers; nothing else has a use for it.
/// </summary>
/// <remarks>
/// The address changes: when the context lays its regions out again, and
/// for as long as a procedure called through an argument runs. It is
/// therefore read on entry each time, and never kept.
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
    }

    /// <summary>Gets a value indicating whether the procedures take their buffers from the stack.</summary>
    public bool IsOnStack { get; }

    /// <summary>Gets where the project's buffers start, when they are not on the stack.</summary>
    public byte* Start { get; internal set; }

    internal string Project { get; }

    internal long OwnBytes { get; }

    internal long ReachBytes { get; }

    internal string[] RefersTo { get; }

    // Where the region starts in the block of any level.
    internal long Base { get; set; }
}
