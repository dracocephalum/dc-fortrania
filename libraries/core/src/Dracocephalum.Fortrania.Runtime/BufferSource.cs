// SPDX-License-Identifier: Apache-2.0
// Part of the Fortrania runtime, which code translated from FORTRAN 77 calls.

#nullable enable

namespace Dracocephalum.Fortrania.Runtime;

/// <summary>
/// What a translated procedure that has working buffers does on entry to
/// take them, as its project's region says at that moment.
/// </summary>
#if FORTRANIA_RUNTIME_INTERNAL
internal
#else
public
#endif
enum BufferSource
{
    /// <summary>
    /// Nothing translated is running, so no copy of the arena is open: a
    /// procedure called from outside calls its core through a method that
    /// opens a copy and closes it however the call ends.
    /// </summary>
    None,

    /// <summary>At its place in the copy of the arena now open, from <see cref="BufferRegion.Start"/>.</summary>
    Arena,

    /// <summary>From the stack.</summary>
    Stack,
}
