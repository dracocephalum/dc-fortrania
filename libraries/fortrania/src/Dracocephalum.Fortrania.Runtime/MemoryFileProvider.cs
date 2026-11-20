// SPDX-License-Identifier: Apache-2.0
// Part of the Fortrania runtime, which code translated from FORTRAN 77 calls.

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;

namespace Dracocephalum.Fortrania.Runtime;

/// <summary>
/// Takes a name for a file kept in memory, and never touches a disk. A
/// program that writes files and reads them back finds each as it left it,
/// for as long as the provider lives; nothing is there before the program
/// or its host puts it there, and nothing is left when the provider is
/// dropped. It is for running a program without leaving files behind, many
/// instances at once without their files meeting, and faster than a disk
/// allows.
/// </summary>
/// <remarks>
/// A name is the file's whole identity, as it is spelled: case counts, and
/// a path is not resolved. A file is one array of bytes, so it holds less
/// than two gigabytes. Like the instance it belongs to, a provider serves
/// one caller at a time.
/// </remarks>
#if FORTRANIA_RUNTIME_INTERNAL
internal
#else
public
#endif
sealed class MemoryFileProvider : IFileProvider
{
    private readonly Dictionary<string, Contents> _files = new(StringComparer.Ordinal);

    /// <summary>Gets the number of files there are.</summary>
    public int Count => _files.Count;

    /// <summary>Puts a file there, in place of any of the same name.</summary>
    /// <param name="name">The name, as the program will give it.</param>
    /// <param name="bytes">The file's contents, which are copied.</param>
    public void Add(string name, ReadOnlySpan<byte> bytes)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        var contents = new Contents();
        contents.Write(0, bytes);
        _files[name] = contents;
    }

    /// <summary>Gives what a file holds now.</summary>
    /// <param name="name">The file's name.</param>
    /// <param name="bytes">The contents, not copied: they are good until the file is next written.</param>
    /// <returns>Whether there is such a file.</returns>
    public bool TryGetContents(string name, out ReadOnlyMemory<byte> bytes)
    {
        if (_files.TryGetValue(name, out var contents))
        {
            bytes = contents.ToMemory();
            return true;
        }

        bytes = default;
        return false;
    }

    /// <inheritdoc/>
    public bool Exists(string name) => _files.ContainsKey(name);

    /// <inheritdoc/>
    public string Identify(string name) => name;

    /// <inheritdoc/>
    public FortranFile Open(string name, FileMode mode)
    {
        var exists = _files.TryGetValue(name, out var contents);
        switch (mode)
        {
            case FileMode.Open or FileMode.Truncate when !exists:
                throw new FileNotFoundException($"There is no file {name} in memory.", name);
            case FileMode.CreateNew when exists:
                throw new IOException($"The file {name} is in memory already.");
        }

        if (contents is null)
        {
            contents = new Contents();
            _files[name] = contents;
        }
        else if (mode is FileMode.Create or FileMode.Truncate)
        {
            // The same file made empty, as on a disk, so that a unit still
            // connected to it sees what is written from here on.
            contents.Truncate(0);
        }

        return new OpenFile(contents);
    }

    /// <inheritdoc/>
    public FortranFile OpenScratch() => new OpenFile(new Contents());

    /// <inheritdoc/>
    public void Delete(string name) => _files.Remove(name);

    // What a file holds. It outlives every unit connected to it, and goes
    // when its name is deleted and the last of them is closed.
    private sealed class Contents
    {
        private byte[] _bytes = [];

        public long Length { get; private set; }

        public ReadOnlyMemory<byte> ToMemory() => _bytes.AsMemory(0, (int)Length);

        public int Read(long offset, Span<byte> buffer)
        {
            if (offset >= Length)
            {
                return 0;
            }

            var count = (int)Math.Min(buffer.Length, Length - offset);
            _bytes.AsSpan((int)offset, count).CopyTo(buffer);
            return count;
        }

        public void Write(long offset, ReadOnlySpan<byte> buffer)
        {
            var end = offset + buffer.Length;
            Reserve(end);

            // What lies between the old end and a write past it is zeros:
            // the array is kept zero beyond the file's length.
            buffer.CopyTo(_bytes.AsSpan((int)offset));
            Length = Math.Max(Length, end);
        }

        public void Truncate(long length)
        {
            if (length < Length)
            {
                _bytes.AsSpan((int)length, (int)(Length - length)).Clear();
            }
            else
            {
                Reserve(length);
            }

            Length = length;
        }

        // Grows by doubling, so that a file written a record at a time is
        // copied a number of times that grows with the logarithm of its size.
        private void Reserve(long length)
        {
            if (length <= _bytes.Length)
            {
                return;
            }

            if (length > Array.MaxLength)
            {
                throw new IOException("A file in memory holds less than two gigabytes.");
            }

            Array.Resize(ref _bytes, (int)Math.Min(Math.Max(length, Math.Max(_bytes.Length * 2L, 256)), Array.MaxLength));
        }
    }

    // One connection to a file. Closing it leaves the contents where they are.
    private sealed class OpenFile(Contents contents) : FortranFile
    {
        public override long Length => contents.Length;

        public override bool CanWrite => true;

        public override int Read(long offset, Span<byte> buffer) => contents.Read(offset, buffer);

        public override void Write(long offset, ReadOnlySpan<byte> buffer) => contents.Write(offset, buffer);

        public override void Truncate(long length) => contents.Truncate(length);

        protected override void Dispose(bool disposing)
        {
            // The contents belong to the provider.
        }
    }
}
