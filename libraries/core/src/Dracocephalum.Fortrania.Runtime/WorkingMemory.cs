// SPDX-License-Identifier: Apache-2.0
// Part of the Fortrania runtime, which code translated from FORTRAN 77 calls.

#nullable enable

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Dracocephalum.Fortrania.Runtime;

/// <summary>
/// The memory the procedures of a program keep their working buffers in
/// when those are not on the stack. Each translated project has a region
/// of it, where each of its procedures has a fixed place; this lays the
/// regions out, one after another in the order the projects can call each
/// other, and holds the block they lie in.
/// </summary>
/// <remarks>
/// <para>
/// A procedure's place is fixed when it is translated, from the calls by
/// name within its project. Nothing is taken or given back when a
/// procedure is called, and nothing has to be undone after an exception.
/// </para>
/// <para>
/// A procedure called through an argument is not covered by that: it may
/// call back into a library whose chain is still running. Every such call
/// therefore goes through <see cref="Enter"/> and <see cref="Leave"/>,
/// which give it a block of its own, the next level's, with the same
/// regions in it.
/// </para>
/// <para>
/// The block grows when a library that needs more is made, which is safe
/// between calls only, since a running procedure holds addresses into it.
/// A project says what it reaches as well as what it needs itself, so the
/// library called first asks for all that its call can need, before
/// anything runs.
/// </para>
/// </remarks>
#if FORTRANIA_RUNTIME_INTERNAL
internal
#else
public
#endif
sealed unsafe class WorkingMemory : IDisposable
{
    // Buffers hold numbers of every kind; a line of cache is the most any
    // of them, or a vector over them, asks for.
    private const nuint Alignment = 64;

    private readonly WorkingBuffers _chosen;
    private readonly int _levelLimit;
    private readonly List<BufferRegion> _regions = [];

    // The block of each level reached so far, all of one size.
    private readonly List<nint> _blocks = [];
    private long _blockBytes;
    private int _level;
    private bool _isDisposed;

    internal WorkingMemory(WorkingBuffers chosen, int levelLimit)
    {
        _chosen = chosen;
        _levelLimit = levelLimit;
    }

    /// <summary>Gets the number of bytes each level's block has; zero while no project keeps its buffers in one.</summary>
    public long BlockBytes => _blockBytes;

    /// <summary>Gets how many calls through a procedure argument are running, one inside another.</summary>
    public int Level => _level;

    /// <summary>
    /// Gives the region of a project, laying the regions out again when
    /// the project is new to the context or asks for more than it had.
    /// Every library of a project asks when it is made, and all of them are
    /// given the same region, as large as the most any of them asked for:
    /// libraries of a project that never call each other need no more
    /// together than the larger, and one never made takes nothing.
    /// </summary>
    /// <remarks>
    /// A library asks for all that the libraries it calls into within its
    /// project need, and a project that calls into another for all that
    /// one can need; so a library made on its first call, while its caller
    /// runs, finds the block as large as it needs already.
    /// </remarks>
    /// <param name="project">The project's name, which is what tells projects apart.</param>
    /// <param name="ownBytes">What the library needs of the project's own region: its procedures' and those of the libraries of the project they call.</param>
    /// <param name="reachBytes">What a call into the library can need: what it needs of its region and the deepest chain of the projects it calls into.</param>
    /// <param name="refersTo">The names of the projects it calls into.</param>
    /// <param name="translatedFor">
    /// What the project was translated for: <see cref="WorkingBuffers.Default"/>
    /// when its procedures choose on entry, and otherwise the one place
    /// they know.
    /// </param>
    /// <returns>The region.</returns>
    /// <exception cref="InvalidOperationException">
    /// The context asks for one place and the project was translated for
    /// the other; or the block would have to grow while a procedure called
    /// through an argument is running.
    /// </exception>
    public BufferRegion Register(string project, long ownBytes, long reachBytes, ReadOnlySpan<string> refersTo, WorkingBuffers translatedFor)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentOutOfRangeException.ThrowIfNegative(ownBytes);
        ArgumentOutOfRangeException.ThrowIfLessThan(reachBytes, ownBytes);
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        foreach (var known in _regions)
        {
            if (known.Project == project)
            {
                Widen(known, ownBytes, reachBytes);
                return known;
            }
        }

        if (_chosen != WorkingBuffers.Default && translatedFor != WorkingBuffers.Default && _chosen != translatedFor)
        {
            throw new InvalidOperationException(
                $"The context keeps working buffers in the {Name(_chosen)}, and {project} was translated for the {Name(translatedFor)} alone.");
        }

        // The context's choice where it made one, else the project's, and
        // the stack where neither did.
        var place = _chosen != WorkingBuffers.Default ? _chosen : translatedFor;
        var region = new BufferRegion(project, ownBytes, reachBytes, refersTo.ToArray(), isOnStack: place != WorkingBuffers.Block);
        _regions.Add(region);
        try
        {
            LayOut();
        }
        catch (InvalidOperationException)
        {
            // The context is left as it was before the project asked.
            _regions.Remove(region);
            LayOut();
            throw;
        }

        return region;
    }

    // A library of a project already known that needs more of its region
    // than those made before it: the region grows to what it needs, and
    // the regions after it move. Where the block cannot grow, it is left as
    // it was.
    private void Widen(BufferRegion region, long ownBytes, long reachBytes)
    {
        if (ownBytes <= region.OwnBytes && reachBytes <= region.ReachBytes)
        {
            return;
        }

        var (own, reach) = (region.OwnBytes, region.ReachBytes);
        region.OwnBytes = Math.Max(own, ownBytes);
        region.ReachBytes = Math.Max(reach, reachBytes);
        try
        {
            LayOut();
        }
        catch (InvalidOperationException)
        {
            (region.OwnBytes, region.ReachBytes) = (own, reach);
            LayOut();
            throw;
        }
    }

    /// <summary>
    /// Moves every region to the next level's block, for a procedure about
    /// to be called through an argument. <see cref="Leave"/> moves them
    /// back, and is to be called however the procedure ends.
    /// </summary>
    /// <exception cref="InvalidOperationException">More such calls are running, one inside another, than the context allows.</exception>
    public void Enter()
    {
        if (_level >= _levelLimit)
        {
            throw new InvalidOperationException(
                $"More than {_levelLimit} calls through a procedure argument are running, one inside another. A context allows as many as its options say.");
        }

        _level++;
        if (_blockBytes > 0)
        {
            if (_level == _blocks.Count)
            {
                _blocks.Add((nint)NativeMemory.AlignedAlloc((nuint)_blockBytes, Alignment));
            }

            Point();
        }
    }

    /// <summary>Moves every region back to the block it was in before <see cref="Enter"/>.</summary>
    public void Leave()
    {
        _level--;
        if (_blockBytes > 0)
        {
            Point();
        }
    }

    /// <summary>Frees the blocks. No address into them may be used afterwards.</summary>
    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        Free();
    }

    private static string Name(WorkingBuffers place) => place == WorkingBuffers.Block ? "block" : "stack";

    // A region starts after those of every project that can call into its
    // own, so that a chain from one project into the next goes on where
    // the first left off. The block is as large as the deepest such chain
    // can reach, the projects not yet made among them.
    private void LayOut()
    {
        long needed = 0;
        foreach (var region in _regions)
        {
            region.Base = -1;
        }

        foreach (var region in _regions)
        {
            if (!region.IsOnStack)
            {
                needed = Math.Max(needed, BaseOf(region) + region.ReachBytes);
            }
        }

        if (needed > _blockBytes)
        {
            // A procedure running at a deeper level was reached from one
            // that holds addresses into the blocks that would be freed.
            if (_level > 0)
            {
                throw new InvalidOperationException(
                    "A library that needs more working memory than the context has was made while a procedure called through an argument was running. Make it before the call.");
            }

            Free();
            _blockBytes = needed;
            _blocks.Add((nint)NativeMemory.AlignedAlloc((nuint)needed, Alignment));
        }

        if (_blockBytes > 0)
        {
            Point();
        }
    }

    private long BaseOf(BufferRegion region)
    {
        if (region.Base >= 0)
        {
            return region.Base;
        }

        // Projects do not call each other both ways, so this ends.
        long start = 0;
        foreach (var caller in _regions)
        {
            if (!caller.IsOnStack && Array.IndexOf(caller.RefersTo, region.Project) >= 0)
            {
                start = Math.Max(start, BaseOf(caller) + caller.OwnBytes);
            }
        }

        region.Base = start;
        return start;
    }

    private void Point()
    {
        var block = (byte*)_blocks[_level];
        foreach (var region in _regions)
        {
            region.Start = region.IsOnStack ? null : block + region.Base;
        }
    }

    private void Free()
    {
        foreach (var block in _blocks)
        {
            NativeMemory.AlignedFree((void*)block);
        }

        _blocks.Clear();
        _blockBytes = 0;
    }
}
