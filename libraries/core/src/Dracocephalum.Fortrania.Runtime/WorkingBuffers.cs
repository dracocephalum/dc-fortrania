// SPDX-License-Identifier: Apache-2.0
// Part of the Fortrania runtime, which code translated from FORTRAN 77 calls.

#nullable enable

namespace Dracocephalum.Fortrania.Runtime;

/// <summary>
/// Where a translated procedure keeps what it needs only while it runs:
/// its local arrays and character variables, and the places it makes
/// values in. Its scalars are on the stack whichever is chosen.
/// </summary>
#if FORTRANIA_RUNTIME_INTERNAL
internal
#else
public
#endif
enum WorkingBuffers
{
    /// <summary>
    /// Whatever a library was translated for, and the stack for one that
    /// serves both. What a context has unless it is told otherwise.
    /// </summary>
    Default,

    /// <summary>On the stack of the thread that calls, which is fastest and needs the stack to be deep enough.</summary>
    Stack,

    /// <summary>In a block of memory the context holds, which leaves the thread's stack the scalars alone.</summary>
    Block,
}
