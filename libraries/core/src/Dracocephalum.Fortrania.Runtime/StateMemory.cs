// SPDX-License-Identifier: Apache-2.0
// Part of the Fortrania runtime, which code translated from FORTRAN 77 calls.

#nullable enable

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Dracocephalum.Fortrania.Runtime;

/// <summary>
/// The memory a context's translated libraries keep between calls: what
/// the FORTRAN declares <c>SAVE</c> or initializes with <c>DATA</c>, and
/// local arrays too large for the stack. A context has one, and each
/// program unit's instance in the context takes a block of it when it is
/// made. A block never moves, so an address into it stays good until the
/// context is disposed, and it is zero when handed out.
/// </summary>
/// <remarks>
/// It is not freed by the garbage collector. One that is dropped without
/// being disposed keeps its memory until the process ends.
/// </remarks>
#if FORTRANIA_RUNTIME_INTERNAL
internal
#else
public
#endif
sealed unsafe class StateMemory : IDisposable
{
    private readonly List<nint> _blocks = new();
    private bool _isDisposed;

    /// <summary>Gets the number of bytes handed out so far.</summary>
    public long Allocated { get; private set; }

    /// <summary>Hands out a block of zeroed memory.</summary>
    /// <param name="size">The number of bytes; at least one is given.</param>
    /// <returns>The address of the block, aligned for any numeric type.</returns>
    /// <exception cref="ObjectDisposedException">The memory has been disposed.</exception>
    public byte* Allocate(int size)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        ArgumentOutOfRangeException.ThrowIfNegative(size);

        var count = (nuint)Math.Max(size, 1);
        var block = NativeMemory.AllocZeroed(count);
        _blocks.Add((nint)block);
        Allocated += (long)count;
        return (byte*)block;
    }

    /// <summary>Frees every block handed out. No address into them may be used afterwards.</summary>
    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        foreach (var block in _blocks)
        {
            NativeMemory.Free((void*)block);
        }

        _blocks.Clear();
    }
}
