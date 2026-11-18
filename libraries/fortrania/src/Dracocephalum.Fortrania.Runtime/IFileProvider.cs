// SPDX-License-Identifier: Apache-2.0
// Part of the Fortrania runtime, which code translated from FORTRAN 77 calls.

#nullable enable

using System.IO;

namespace Dracocephalum.Fortrania.Runtime;

/// <summary>
/// Decides what the name in an <c>OPEN</c> or <c>INQUIRE</c> statement
/// stands for. The translated code knows files by name and unit number
/// alone; this is where a name becomes something that holds bytes.
/// </summary>
#if FORTRANIA_RUNTIME_INTERNAL
internal
#else
public
#endif
interface IFileProvider
{
    /// <summary>Tells whether a file of that name exists.</summary>
    /// <param name="name">The name as the program gave it, without trailing blanks.</param>
    /// <returns>Whether it exists.</returns>
    bool Exists(string name);

    /// <summary>
    /// Gives the name under which two names for one file are equal, so
    /// that a file already connected to a unit is found under any of them.
    /// </summary>
    /// <param name="name">The name as the program gave it, without trailing blanks.</param>
    /// <returns>The name to compare by, with <see cref="string.Equals(string, string)"/>.</returns>
    string Identify(string name);

    /// <summary>Opens a file.</summary>
    /// <param name="name">The name as the program gave it, without trailing blanks.</param>
    /// <param name="mode">
    /// <see cref="FileMode.Open"/> for a file that has to exist,
    /// <see cref="FileMode.CreateNew"/> for one that must not,
    /// <see cref="FileMode.Create"/> to replace what is there, or
    /// <see cref="FileMode.OpenOrCreate"/>.
    /// </param>
    /// <returns>The file, for reading and writing, or for reading where it cannot be written.</returns>
    FortranFile Open(string name, FileMode mode);

    /// <summary>Makes a file with no name that is gone when it is closed.</summary>
    /// <returns>The file.</returns>
    FortranFile OpenScratch();

    /// <summary>Deletes a file that is not open.</summary>
    /// <param name="name">The name as the program gave it, without trailing blanks.</param>
    void Delete(string name);
}
