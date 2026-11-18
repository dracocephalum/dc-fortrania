// SPDX-License-Identifier: Apache-2.0
// Part of the Fortrania runtime, which code translated from FORTRAN 77 calls.

#nullable enable

using System;
using System.IO;

namespace Dracocephalum.Fortrania.Runtime;

/// <summary>
/// A stream that can seek, taken for a file. The stream has one position,
/// which every transfer moves, so nothing else may use the stream while a
/// unit is connected to it.
/// </summary>
#if FORTRANIA_RUNTIME_INTERNAL
internal
#else
public
#endif
sealed class StreamFile : FortranFile
{
    private readonly Stream _stream;
    private readonly bool _leavesOpen;

    /// <summary>Initializes a new instance of the <see cref="StreamFile"/> class.</summary>
    /// <param name="stream">A stream that can seek and be read.</param>
    /// <param name="leaveOpen">Whether the stream stays open when the file is disposed.</param>
    /// <exception cref="ArgumentException">The stream cannot seek or cannot be read.</exception>
    public StreamFile(Stream stream, bool leaveOpen = false)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (!stream.CanSeek || !stream.CanRead)
        {
            throw new ArgumentException("A stream taken for a file has to seek and be readable.", nameof(stream));
        }

        _stream = stream;
        _leavesOpen = leaveOpen;
    }

    /// <inheritdoc/>
    public override long Length => _stream.Length;

    /// <inheritdoc/>
    public override bool CanWrite => _stream.CanWrite;

    /// <inheritdoc/>
    public override int Read(long offset, Span<byte> buffer)
    {
        if (offset >= _stream.Length)
        {
            return 0;
        }

        _stream.Position = offset;
        return _stream.ReadAtLeast(buffer, buffer.Length, throwOnEndOfStream: false);
    }

    /// <inheritdoc/>
    public override void Write(long offset, ReadOnlySpan<byte> buffer)
    {
        // Setting a position past the end and writing there fills what
        // lies between with zeros, as a file on disk does.
        _stream.Position = offset;
        _stream.Write(buffer);
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing && !_leavesOpen)
        {
            _stream.Dispose();
        }
    }
}
