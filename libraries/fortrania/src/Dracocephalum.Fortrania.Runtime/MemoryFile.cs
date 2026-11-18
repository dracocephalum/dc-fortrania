// SPDX-License-Identifier: Apache-2.0
// Part of the Fortrania runtime, which code translated from FORTRAN 77 calls.

#nullable enable

using System;

namespace Dracocephalum.Fortrania.Runtime;

/// <summary>
/// Bytes held in memory, taken for a file that can be read and not
/// written. It keeps no position and changes nothing, so the same bytes
/// may be behind units of any number of instances at once, on any threads.
/// </summary>
#if FORTRANIA_RUNTIME_INTERNAL
internal
#else
public
#endif
sealed class MemoryFile : FortranFile
{
    private readonly ReadOnlyMemory<byte> _bytes;

    /// <summary>Initializes a new instance of the <see cref="MemoryFile"/> class.</summary>
    /// <param name="bytes">The file's contents, which are not copied and must not change while a unit is connected.</param>
    public MemoryFile(ReadOnlyMemory<byte> bytes) => _bytes = bytes;

    /// <inheritdoc/>
    public override long Length => _bytes.Length;

    /// <inheritdoc/>
    public override bool CanWrite => false;

    /// <inheritdoc/>
    public override int Read(long offset, Span<byte> buffer)
    {
        if (offset >= _bytes.Length)
        {
            return 0;
        }

        var count = (int)Math.Min(buffer.Length, _bytes.Length - offset);
        _bytes.Span.Slice((int)offset, count).CopyTo(buffer);
        return count;
    }

    /// <inheritdoc/>
    public override void Write(long offset, ReadOnlySpan<byte> buffer) =>
        throw new NotSupportedException("A file held in memory for reading cannot be written.");

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        // The bytes are the caller's, and there is nothing else to release.
    }
}
