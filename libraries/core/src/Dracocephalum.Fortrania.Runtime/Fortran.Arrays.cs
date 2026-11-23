// SPDX-License-Identifier: Apache-2.0
// Part of the Fortrania runtime, which code translated from FORTRAN 77 calls.

#nullable enable

using System;
using System.Runtime.CompilerServices;

namespace Dracocephalum.Fortrania.Runtime;

/// <content>
/// Whole runs of an array's elements, for a <c>DO</c> loop that only
/// copies one array to another or gives every element the same value. A
/// translation keeps the loop for a short run and for one these cannot
/// stand for, and asks <see cref="CanMoveElements{T}"/> which it is.
/// </content>
#if FORTRANIA_RUNTIME_INTERNAL
internal
#else
public
#endif
static unsafe partial class Fortran
{
    /// <summary>
    /// How many elements make a run worth handing to the framework, whose
    /// copy and fill are faster over many and cost more to begin than a
    /// few elements take in a loop.
    /// </summary>
    public const int LongRun = 16;

    /// <summary>
    /// Tells whether copying a run of elements at once gives what a loop
    /// that copies them from the first to the last gives, and is worth it.
    /// </summary>
    /// <typeparam name="T">The type of an element.</typeparam>
    /// <param name="from">The first element copied.</param>
    /// <param name="to">Where the first is copied to.</param>
    /// <param name="count">How many elements.</param>
    /// <returns>
    /// Whether the run is long and the target does not start inside the
    /// source after its start. Where it does, the loop reads elements it
    /// has just written, and a copy made at once would not.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool CanMoveElements<T>(T* from, T* to, int count)
        where T : unmanaged =>
        count >= LongRun && (to <= from || to >= from + count);

    /// <summary>
    /// Copies a run of elements at once, where <see cref="CanMoveElements{T}"/>
    /// has said it may be. It is a call and not written in line, and gives
    /// back what the loop's variable is to be, so that the loop beside it
    /// in a translated procedure costs a short run nothing: nothing of the
    /// procedure's has to be kept across the call.
    /// </summary>
    /// <typeparam name="T">The type of an element.</typeparam>
    /// <param name="from">The first element copied.</param>
    /// <param name="to">Where the first is copied to.</param>
    /// <param name="count">How many elements.</param>
    /// <returns>One more than <paramref name="count"/>, which is where a loop from one leaves its variable.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static int MoveElements<T>(T* from, T* to, int count)
        where T : unmanaged
    {
        Buffer.MemoryCopy(from, to, (long)count * sizeof(T), (long)count * sizeof(T));
        return count + 1;
    }

    /// <summary>Gives every element of a run the same value; a call, as <see cref="MoveElements{T}"/> is and for the same reason.</summary>
    /// <typeparam name="T">The type of an element.</typeparam>
    /// <param name="array">The first element.</param>
    /// <param name="count">How many elements.</param>
    /// <param name="value">The value.</param>
    /// <returns>One more than <paramref name="count"/>, which is where a loop from one leaves its variable.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static int FillElements<T>(T* array, int count, T value)
        where T : unmanaged
    {
        new Span<T>(array, count).Fill(value);
        return count + 1;
    }
}
