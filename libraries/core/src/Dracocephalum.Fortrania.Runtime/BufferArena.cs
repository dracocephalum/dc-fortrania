// SPDX-License-Identifier: Apache-2.0
// Part of the Fortrania runtime, which code translated from FORTRAN 77 calls.

#nullable enable

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Dracocephalum.Fortrania.Runtime;

/// <summary>
/// The native memory the procedures of a program keep their working
/// buffers in when those are not on the stack. Each translated project has
/// a region of it, where each of its procedures has a fixed place; the
/// arena lays the regions out, one after another in the order the projects
/// can call each other, and holds the memory they lie in.
/// </summary>
/// <remarks>
/// <para>
/// A procedure's place is fixed when it is translated, from the calls by
/// name within its project. Nothing is taken or given back when a
/// procedure is called, and nothing has to be undone after an exception.
/// </para>
/// <para>
/// The arena is used in copies, one open on top of another, all laid out
/// alike. The outermost call into translated code opens the first with
/// <see cref="Push"/> and closes it with <see cref="Pop"/> however it ends;
/// a call through a procedure argument, which may call back into a library
/// whose chain is still running, opens one more for itself. A procedure
/// takes its buffers from the copy open when it is entered, and keeps them
/// there until it returns.
/// </para>
/// <para>
/// A library that needs more than the arena has, or that moves a region,
/// changes the layout while procedures may be running in it. Their memory
/// is never taken from under them: each copy in use is replaced by one laid
/// out anew, made as soon as its depth is open, and the old one is kept
/// until the copy it was is closed, when every procedure that can be using
/// it has returned.
/// </para>
/// <para>A context, and so its arena, is used by one thread at a time.</para>
/// </remarks>
#if FORTRANIA_RUNTIME_INTERNAL
internal
#else
public
#endif
sealed unsafe class BufferArena : IDisposable
{
    // Buffers hold numbers of every kind; a line of cache is the most any
    // of them, or a vector over them, asks for.
    private const nuint Alignment = 64;

    private readonly BufferPlacement _chosen;
    private readonly int _depthLimit;
    private readonly List<BufferRegion> _regions = [];

    // The copy made for each depth reached so far, by the depth less one:
    // zero where none is made yet, or where the one there was replaced.
    private readonly List<nint> _copies = [];

    // Copies replaced while procedures may still be using them, each with
    // the depth whose close frees it.
    private readonly List<(nint Copy, int Depth)> _retired = [];

    private long _bytes;
    private int _depth;
    private bool _isDisposed;

    internal BufferArena(BufferPlacement chosen, int callbackDepth)
    {
        _chosen = chosen;

        // The outermost call opens a copy, and each call through a procedure
        // argument running inside it one more.
        _depthLimit = callbackDepth + 1;
    }

    /// <summary>Gets how many bytes each copy of the arena has; zero while no project keeps its buffers in it.</summary>
    public long Bytes => _bytes;

    /// <summary>
    /// Gets how many copies of the arena are open: none while nothing
    /// translated runs, one during a call from outside, and one more for
    /// each call through a procedure argument running inside it.
    /// </summary>
    public int Depth => _depth;

    // How many replaced copies are kept for procedures that may still be
    // using them.
    internal int RetiredCount => _retired.Count;

    /// <summary>
    /// Gives the region of a project, laying the regions out again when the
    /// project is new to the context or asks for more than it had. Every
    /// library of a project asks when it is made, and all of them are given
    /// the same region, as large as the most any of them asked for:
    /// libraries of a project that never call each other need no more
    /// together than the larger, and one never made takes nothing.
    /// </summary>
    /// <remarks>
    /// A library asks for all that the libraries it calls into within its
    /// project need, and a project that calls into another for all that one
    /// can need; so a library made on its first call, while its caller runs,
    /// finds the arena as large as it needs already. One that does not, made
    /// by code the translation does not see, is given a new copy, and the
    /// procedures running keep theirs.
    /// </remarks>
    /// <param name="project">The project's name, which is what tells projects apart.</param>
    /// <param name="ownBytes">What the library needs of the project's own region: its procedures' and those of the libraries of the project they call.</param>
    /// <param name="reachBytes">What a call into the library can need: what it needs of its region and the deepest chain of the projects it calls into.</param>
    /// <param name="refersTo">The names of the projects it calls into.</param>
    /// <param name="translatedFor">
    /// What the project was translated for: <see cref="BufferPlacement.Default"/>
    /// when its procedures choose on entry, and otherwise the one place they
    /// know.
    /// </param>
    /// <returns>The region.</returns>
    /// <exception cref="InvalidOperationException">The context asks for one place and the project was translated for the other.</exception>
    public BufferRegion Register(string project, long ownBytes, long reachBytes, ReadOnlySpan<string> refersTo, BufferPlacement translatedFor)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentOutOfRangeException.ThrowIfNegative(ownBytes);
        ArgumentOutOfRangeException.ThrowIfLessThan(reachBytes, ownBytes);
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        BufferRegion? region = null;
        foreach (var known in _regions)
        {
            if (known.Project == project)
            {
                region = known;
                break;
            }
        }

        if (region is not null && ownBytes <= region.OwnBytes && reachBytes <= region.ReachBytes)
        {
            return region;
        }

        var isNew = region is null;
        if (region is null)
        {
            if (_chosen != BufferPlacement.Default && translatedFor != BufferPlacement.Default && _chosen != translatedFor)
            {
                throw new InvalidOperationException(
                    $"The context keeps working buffers in the {Name(_chosen)}, and {project} was translated for the {Name(translatedFor)} alone.");
            }

            // The context's choice where it made one, else the project's,
            // and the stack where neither did.
            var place = _chosen != BufferPlacement.Default ? _chosen : translatedFor;
            region = new BufferRegion(project, ownBytes, reachBytes, refersTo.ToArray(), isOnStack: place != BufferPlacement.Arena);
            _regions.Add(region);
        }

        var (own, reach) = (region.OwnBytes, region.ReachBytes);
        region.OwnBytes = Math.Max(own, ownBytes);
        region.ReachBytes = Math.Max(reach, reachBytes);
        try
        {
            LayOut();
        }
        catch
        {
            // Nothing of the arena changed but what the library asked for,
            // which it is left without.
            if (isNew)
            {
                _regions.Remove(region);
            }
            else
            {
                (region.OwnBytes, region.ReachBytes) = (own, reach);
            }

            throw;
        }

        return region;
    }

    /// <summary>
    /// Opens a copy of the arena on top of those open, for a call that may
    /// reach libraries whose procedures are running in the copy below: the
    /// outermost call into translated code, or a call through a procedure
    /// argument. <see cref="Pop"/> closes it, and is to be called however
    /// the call ends.
    /// </summary>
    /// <exception cref="InvalidOperationException">More calls through a procedure argument are running, one inside another, than the context allows.</exception>
    public void Push()
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        if (_depth >= _depthLimit)
        {
            throw new InvalidOperationException(
                $"More than {_depthLimit - 1} calls through a procedure argument are running, one inside another. A context allows as many as its options say.");
        }

        // Made before anything changes, so that a failure leaves the arena
        // as it was.
        MakeCopy(_depth);
        _depth++;
        Point();
    }

    /// <summary>Closes the copy of the arena <see cref="Push"/> opened last, and goes back to the one below.</summary>
    /// <exception cref="InvalidOperationException">No copy is open.</exception>
    public void Pop()
    {
        if (_depth == 0)
        {
            throw new InvalidOperationException("No copy of the arena is open to close.");
        }

        // Every procedure entered while this copy was open has returned, so
        // what was replaced at this depth is used by nothing.
        for (var index = _retired.Count - 1; index >= 0; index--)
        {
            if (_retired[index].Depth == _depth)
            {
                Free(_retired[index].Copy);
                _retired.RemoveAt(index);
            }
        }

        _depth--;
        Point();
    }

    /// <summary>Frees the arena's memory. No address into it may be used afterwards.</summary>
    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        foreach (var copy in _copies)
        {
            if (copy != 0)
            {
                Free(copy);
            }
        }

        foreach (var (copy, _) in _retired)
        {
            Free(copy);
        }

        _copies.Clear();
        _retired.Clear();
        _bytes = 0;
    }

    private static string Name(BufferPlacement place) => place == BufferPlacement.Arena ? "arena" : "stack";

    private static nint Allocate(long bytes) => (nint)NativeMemory.AlignedAlloc((nuint)bytes, Alignment);

    private static void Free(nint copy) => NativeMemory.AlignedFree((void*)copy);

    // A region starts after those of every project that can call into its
    // own, so that a chain from one project into the next goes on where the
    // first left off. A copy is as large as the deepest such chain can
    // reach, the projects not yet made among them. Where that is more than
    // a copy has, or a region moves, every copy is replaced: the one open
    // now at once, and one deeper down when its depth is open again. The new
    // copy is made before anything changes, so that a failure leaves the
    // arena as it was.
    private void LayOut()
    {
        var bases = new long[_regions.Count];
        Array.Fill(bases, -1);
        long needed = 0;
        var moves = false;
        for (var index = 0; index < _regions.Count; index++)
        {
            var region = _regions[index];
            if (!region.IsOnStack)
            {
                var start = BaseOf(index, bases);
                needed = Math.Max(needed, start + region.ReachBytes);
                moves |= region.Base >= 0 && region.Base != start;
            }
        }

        if (needed > _bytes || moves)
        {
            var bytes = Math.Max(needed, _bytes);
            var fresh = _depth > 0 ? Allocate(bytes) : 0;
            for (var index = 0; index < _copies.Count; index++)
            {
                // A copy of a depth open may hold the buffers of procedures
                // running; one deeper than that holds nothing.
                if (_copies[index] != 0)
                {
                    if (index < _depth)
                    {
                        _retired.Add((_copies[index], index + 1));
                    }
                    else
                    {
                        Free(_copies[index]);
                    }

                    _copies[index] = 0;
                }
            }

            _bytes = bytes;
            if (_depth > 0)
            {
                while (_copies.Count < _depth)
                {
                    _copies.Add(0);
                }

                _copies[_depth - 1] = fresh;
            }
        }

        for (var index = 0; index < _regions.Count; index++)
        {
            _regions[index].Base = bases[index];
        }

        Point();
    }

    // Projects do not call each other both ways, so this ends.
    private long BaseOf(int index, long[] bases)
    {
        if (bases[index] >= 0)
        {
            return bases[index];
        }

        var project = _regions[index].Project;
        long start = 0;
        for (var caller = 0; caller < _regions.Count; caller++)
        {
            if (!_regions[caller].IsOnStack && Array.IndexOf(_regions[caller].RefersTo, project) >= 0)
            {
                start = Math.Max(start, BaseOf(caller, bases) + _regions[caller].OwnBytes);
            }
        }

        bases[index] = start;
        return start;
    }

    // The copy a depth uses, made where there is none: the first time the
    // depth is reached, or the first time it is open again after its copy
    // was replaced.
    private void MakeCopy(int index)
    {
        if (_bytes == 0)
        {
            return;
        }

        while (_copies.Count <= index)
        {
            _copies.Add(0);
        }

        if (_copies[index] == 0)
        {
            _copies[index] = Allocate(_bytes);
        }
    }

    // Each region of the arena told where its procedures take their
    // buffers now: from the copy open, or through a call that opens one
    // when none is. Where the copy cannot be made, the stack: a procedure
    // reached from one running tests for nothing else, and the stack is
    // memory that is there.
    private void Point()
    {
        byte* copy = null;
        if (_depth > 0)
        {
            try
            {
                MakeCopy(_depth - 1);
            }
            catch
            {
                foreach (var region in _regions)
                {
                    if (!region.IsOnStack)
                    {
                        region.CurrentSource = BufferSource.Stack;
                        region.Start = null;
                    }
                }

                throw;
            }

            copy = _bytes > 0 ? (byte*)_copies[_depth - 1] : null;
        }

        foreach (var region in _regions)
        {
            if (!region.IsOnStack)
            {
                region.CurrentSource = _depth > 0 ? BufferSource.Arena : BufferSource.None;
                region.Start = copy != null ? copy + region.Base : null;
            }
        }
    }
}
