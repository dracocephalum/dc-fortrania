// SPDX-License-Identifier: Apache-2.0
// Part of the Fortrania runtime, which code translated from FORTRAN 77 calls.

#nullable enable

using System;
using System.IO;
using Microsoft.Win32.SafeHandles;

namespace Dracocephalum.Fortrania.Runtime;

/// <summary>
/// Takes a name for a path on disk. A file is read and written through the
/// operating system's own handle, a record at a time with no buffer in
/// between, and may be open in other units and other programs at once.
/// </summary>
#if FORTRANIA_RUNTIME_INTERNAL
internal
#else
public
#endif
sealed class DiskFileProvider : IFileProvider
{
    // Two spellings of a path that differ in case are one file where the
    // file system does not tell them apart, which is the rule on Windows
    // and macOS and the exception elsewhere.
    private static readonly bool IgnoresCase = OperatingSystem.IsWindows() || OperatingSystem.IsMacOS();

    /// <inheritdoc/>
    public bool Exists(string name) => File.Exists(name);

    /// <inheritdoc/>
    public string Identify(string name)
    {
        var path = Path.GetFullPath(name);
        return IgnoresCase ? path.ToUpperInvariant() : path;
    }

    /// <inheritdoc/>
    public FortranFile Open(string name, FileMode mode)
    {
        try
        {
            return new DiskFile(File.OpenHandle(name, mode, FileAccess.ReadWrite, FileShare.ReadWrite), canWrite: true);
        }
        catch (UnauthorizedAccessException) when (mode == FileMode.Open)
        {
            // A file that may only be read is still a file to read from.
            return new DiskFile(File.OpenHandle(name, mode, FileAccess.Read, FileShare.ReadWrite), canWrite: false);
        }
    }

    /// <inheritdoc/>
    public FortranFile OpenScratch()
    {
        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        return new DiskFile(
            File.OpenHandle(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, FileOptions.DeleteOnClose),
            canWrite: true);
    }

    /// <inheritdoc/>
    public void Delete(string name) => File.Delete(name);

    private sealed class DiskFile(SafeFileHandle handle, bool canWrite) : FortranFile
    {
        public override long Length => RandomAccess.GetLength(handle);

        public override bool CanWrite => canWrite;

        public override int Read(long offset, Span<byte> buffer)
        {
            // The system may hand over fewer bytes than asked for while
            // more are still to come.
            var total = 0;
            while (total < buffer.Length)
            {
                var read = RandomAccess.Read(handle, buffer[total..], offset + total);
                if (read == 0)
                {
                    break;
                }

                total += read;
            }

            return total;
        }

        public override void Write(long offset, ReadOnlySpan<byte> buffer) => RandomAccess.Write(handle, buffer, offset);

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                handle.Dispose();
            }
        }
    }
}
