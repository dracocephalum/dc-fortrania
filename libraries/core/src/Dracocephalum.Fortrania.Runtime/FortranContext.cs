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
/// programs that know nothing of each other, and there is no context that
/// nobody made: whoever wants one makes it and keeps it.
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
sealed partial class FortranContext : IDisposable
{
    // A handle is a place in the table of contexts and the turn that place
    // is on, so that the handle of a context that is gone is told from the
    // handle of the one that took its place.
    private const int PlaceBits = 20;
    private const int PlaceMask = (1 << PlaceBits) - 1;

    private static readonly Lock Registry = new();

    // How many types of library have been given a place so far, in any
    // context: a type's place is the same in all of them.
    private static int _places;

    private static FortranContext?[] _contexts = new FortranContext?[8];
    private static int[] _turns = new int[8];

    private object?[] _libraries = new object?[4];
    private nint _handle;
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

    /// <summary>Initializes a new instance of the <see cref="FortranContext"/> class as a host has decided it.</summary>
    /// <param name="options">What the program is given.</param>
    public FortranContext(FortranContextOptions options)
        : this((options ?? throw new ArgumentNullException(nameof(options))).FileProvider ?? new DiskFileProvider())
    {
        IO.RecordLengthUnit = options.RecordLengthUnit;
        if (options.NewLine is { } ending)
        {
            IO.NewLine = ending;
        }

        if (options.UsesConsole)
        {
            IO.UseConsole();
        }

        CommandLine = options.CommandLine;
        AllowsCommands = options.AllowsCommands;
        Clock = options.Clock ?? TimeProvider.System;
    }

    /// <summary>Gets the program's input and output: its units, files, screen and keyboard.</summary>
    public FortranIO IO { get; }

    /// <summary>Gets the memory the program's libraries keep their variables in between calls.</summary>
    public StateMemory Memory { get; }

    /// <summary>
    /// Gets a value that stands for the context where an object cannot be
    /// passed, in code that is not .NET above all. <see cref="FromHandle"/>
    /// gives the context back. The value means nothing in itself, is never
    /// zero, and stops standing for the context when it is disposed.
    /// </summary>
    public nint Handle
    {
        get
        {
            ObjectDisposedException.ThrowIf(_isDisposed, this);
            if (_handle != 0)
            {
                return _handle;
            }

            lock (Registry)
            {
                if (_handle == 0)
                {
                    var place = Array.IndexOf(_contexts, null);
                    if (place < 0)
                    {
                        place = _contexts.Length;
                        if (place > PlaceMask)
                        {
                            throw new InvalidOperationException("Too many contexts have handles at once.");
                        }

                        // A reader may hold the old tables, which stay as
                        // they were; the new ones are put in place whole.
                        var contexts = new FortranContext?[place * 2];
                        var turns = new int[place * 2];
                        _contexts.CopyTo(contexts, 0);
                        _turns.CopyTo(turns, 0);
                        Volatile.Write(ref _turns, turns);
                        Volatile.Write(ref _contexts, contexts);
                    }

                    var turn = ++_turns[place];
                    Volatile.Write(ref _contexts[place], this);
                    _handle = ((nint)turn << PlaceBits) | (nint)(place + 1);
                }

                return _handle;
            }
        }
    }

    /// <summary>Gives the context a handle stands for.</summary>
    /// <param name="handle">A value <see cref="Handle"/> gave.</param>
    /// <returns>The context.</returns>
    /// <exception cref="ArgumentException">The value is no handle, or the context it stood for has been disposed.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FortranContext FromHandle(nint handle)
    {
        var contexts = Volatile.Read(ref _contexts);
        var place = (int)(handle & PlaceMask) - 1;
        if ((uint)place < (uint)contexts.Length && contexts[place] is { } context && context._handle == handle)
        {
            return context;
        }

        throw new ArgumentException("The value is not the handle of a context that is still in use.", nameof(handle));
    }

    /// <summary>
    /// Makes a library part of the program. A translated library does this
    /// itself when it is made, so this is called by nothing else.
    /// </summary>
    /// <typeparam name="T">The library's class.</typeparam>
    /// <param name="library">The library.</param>
    /// <exception cref="InvalidOperationException">The program has a library of that class already.</exception>
    public void Add<T>(T library)
        where T : class, IFortranLibrary<T>
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

    /// <summary>
    /// Gives the program's library of a class, making it the first time it
    /// is asked for. A context has one library of each class, as a FORTRAN
    /// program is linked with each of its libraries once.
    /// </summary>
    /// <remarks>
    /// It is asked for by its type as a service is asked of a container,
    /// and is to the context what a singleton is to one: made once, on
    /// first use, and the same from then on. It differs in that nothing is
    /// registered beforehand. A library made by hand before it is first
    /// asked for, an instance of a class derived from the library's among
    /// them, is the one that is given.
    /// </remarks>
    /// <typeparam name="T">The library's class.</typeparam>
    /// <returns>The library.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T Get<T>()
        where T : class, IFortranLibrary<T>
    {
        var libraries = _libraries;
        var place = Place<T>.Index;
        if ((uint)place < (uint)libraries.Length && libraries[place] is { } library)
        {
            // Only Add stores at this place, and only a T.
            return Unsafe.As<T>(library);
        }

        return Make<T>();
    }

    /// <summary>Closes the program's files and frees the memory its libraries keep. None of them may be used afterwards.</summary>
    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        if (_handle != 0)
        {
            lock (Registry)
            {
                Volatile.Write(ref _contexts[(int)(_handle & PlaceMask) - 1], null);
            }
        }

        IO.Dispose();
        Memory.Dispose();
        Array.Clear(_libraries);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private T Make<T>()
        where T : class, IFortranLibrary<T>
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        // A library adds itself when it is made; one that did not is added
        // here.
        var made = T.Create(this);
        var place = Place<T>.Index;
        if ((uint)place >= (uint)_libraries.Length || _libraries[place] is null)
        {
            Add(made);
        }

        return Unsafe.As<T>(_libraries[place]!);
    }

    // The place of one class of library in every context's list.
    private static class Place<T>
    {
        public static readonly int Index = Interlocked.Increment(ref _places) - 1;
    }
}
