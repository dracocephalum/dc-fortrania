// SPDX-License-Identifier: Apache-2.0
// Part of the Fortrania runtime, which code translated from FORTRAN 77 calls.

#nullable enable

using System;

namespace Dracocephalum.Fortrania.Runtime;

/// <summary>
/// What is behind a unit: bytes that can be read and written at any
/// position. It keeps no position of its own, so every transfer says where.
/// </summary>
#if FORTRANIA_RUNTIME_INTERNAL
internal
#else
public
#endif
abstract class FortranFile : IDisposable
{
    /// <summary>Gets the number of bytes in the file.</summary>
    public abstract long Length { get; }

    /// <summary>Gets a value indicating whether the file can be written.</summary>
    public abstract bool CanWrite { get; }

    /// <summary>Reads bytes from a position.</summary>
    /// <param name="offset">The position of the first byte, counted from zero.</param>
    /// <param name="buffer">Where the bytes go.</param>
    /// <returns>
    /// The number of bytes read: fewer than asked for only where the file
    /// ends first, and zero at its end or past it.
    /// </returns>
    public abstract int Read(long offset, Span<byte> buffer);

    /// <summary>Writes bytes at a position, making the file longer where it has to be.</summary>
    /// <param name="offset">The position of the first byte, counted from zero.</param>
    /// <param name="buffer">The bytes.</param>
    public abstract void Write(long offset, ReadOnlySpan<byte> buffer);

    /// <summary>Makes the file end at a position, dropping what lies after it.</summary>
    /// <param name="length">The number of bytes the file is to have.</param>
    public abstract void Truncate(long length);

    /// <summary>Releases what the file holds.</summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>Releases what the file holds.</summary>
    /// <param name="disposing">Whether it is called from <see cref="Dispose()"/>.</param>
    protected abstract void Dispose(bool disposing);
}
