// SPDX-License-Identifier: Apache-2.0
// Part of the Fortrania runtime, which code translated from FORTRAN 77 calls.

#nullable enable

using System;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Dracocephalum.Fortrania.Runtime;

/// <summary>
/// What the translated libraries of one program share, as the libraries a
/// FORTRAN program is linked from share its process: one set of units, one
/// screen and keyboard, and the memory their variables are kept in between
/// calls. A translated library is given a context when it is made and uses
/// what it finds there; it decides nothing about it. Two contexts are two
/// programs that know nothing of each other.
/// </summary>
/// <remarks>
/// A context serves one caller at a time, and with it every library made
/// over it.
/// </remarks>
#if FORTRANIA_RUNTIME_INTERNAL
internal
#else
public
#endif
sealed class FortranContext : IDisposable
{
    // How many types of library have been given a place so far, in any
    // context: a type's place is the same in all of them.
    private static int _places;

    private object?[] _libraries = new object?[4];
    private bool _isDisposed;

    /// <summary>Initializes a new instance of the <see cref="FortranContext"/> class, for files on disk.</summary>
    public FortranContext()
        : this(new DiskFileProvider())
    {
    }

    /// <summary>Initializes a new instance of the <see cref="FortranContext"/> class.</summary>
    /// <param name="provider">What decides what a file name stands for.</param>
    public FortranContext(IFileProvider provider)
    {
        IO = new FortranIO(provider);
        Memory = new StateMemory();
    }

    /// <summary>Gets the program's input and output: its units, files, screen and keyboard.</summary>
    public FortranIO IO { get; }

    /// <summary>Gets the memory the program's libraries keep their variables in between calls.</summary>
    public StateMemory Memory { get; }

    /// <summary>
    /// Makes a library part of the program, so that a procedure passed as
    /// an argument can find the library it belongs to. A translated
    /// library does this itself when it is made.
    /// </summary>
    /// <typeparam name="T">The library's class.</typeparam>
    /// <param name="library">The library.</param>
    /// <exception cref="InvalidOperationException">The program has a library of that class already.</exception>
    public void Add<T>(T library)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(library);
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        var place = Place<T>.Index;
        if (place >= _libraries.Length)
        {
            Array.Resize(ref _libraries, Math.Max(place + 1, _libraries.Length * 2));
        }

        if (_libraries[place] is not null)
        {
            throw new InvalidOperationException($"The program has a library of the class {typeof(T).Name} already.");
        }

        _libraries[place] = library;
    }

    /// <summary>Gives the program's library of a class.</summary>
    /// <typeparam name="T">The library's class.</typeparam>
    /// <returns>The library.</returns>
    /// <exception cref="InvalidOperationException">The program has no library of that class.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T Get<T>()
        where T : class
    {
        var libraries = _libraries;
        var place = Place<T>.Index;
        if ((uint)place < (uint)libraries.Length && libraries[place] is { } library)
        {
            // Only Add stores at this place, and only a T.
            return Unsafe.As<T>(library);
        }

        throw new InvalidOperationException($"The program has no library of the class {typeof(T).Name}.");
    }

    /// <summary>Closes the program's files and frees the memory its libraries keep. None of them may be used afterwards.</summary>
    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        IO.Dispose();
        Memory.Dispose();
        Array.Clear(_libraries);
    }

    // The place of one class of library in every context's list.
    private static class Place<T>
    {
        public static readonly int Index = Interlocked.Increment(ref _places) - 1;
    }
}
