// SPDX-License-Identifier: Apache-2.0
// Part of the Fortrania runtime, which code translated from FORTRAN 77 calls.

#nullable enable

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Dracocephalum.Fortrania.Runtime;

/// <content>
/// Character values. One is an address and a length in bytes; it has no
/// terminator, and where two of different lengths meet, the shorter counts
/// as followed by blanks. A length below one is an empty value.
/// </content>
#if FORTRANIA_RUNTIME_INTERNAL
internal
#else
public
#endif
static unsafe partial class Fortran
{
    private const byte Blank = (byte)' ';

    /// <summary>Gives the address of a character constant.</summary>
    /// <param name="constant">
    /// A <c>u8</c> literal and nothing else. The compiler keeps the bytes of
    /// such a literal in the assembly's own image, which never moves, so
    /// the address stays good for as long as the assembly is loaded. No
    /// other span may be given.
    /// </param>
    /// <returns>The address of the first byte.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte* AddrOf(ReadOnlySpan<byte> constant) =>
        (byte*)Unsafe.AsPointer(ref MemoryMarshal.GetReference(constant));

    /// <summary>Stores one character where a procedure can be given its address, as <c>CHAR</c> needs.</summary>
    /// <param name="slot">A local variable of the caller set aside for it.</param>
    /// <param name="value">The character's code.</param>
    /// <returns><paramref name="slot"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte* Arg(byte* slot, int value)
    {
        *slot = (byte)value;
        return slot;
    }

    /// <summary>
    /// Assigns one character value to another: as much as fits is copied,
    /// and what room is left is filled with blanks.
    /// </summary>
    /// <param name="target">Where to store.</param>
    /// <param name="targetLength">The length of the target.</param>
    /// <param name="source">The value assigned. It may overlap the target.</param>
    /// <param name="sourceLength">The length of the value assigned.</param>
    public static void Assign(byte* target, int targetLength, byte* source, int sourceLength)
    {
        if (targetLength <= 0)
        {
            return;
        }

        if (sourceLength >= targetLength)
        {
            Buffer.MemoryCopy(source, target, targetLength, targetLength);
            return;
        }

        if (sourceLength < 0)
        {
            sourceLength = 0;
        }

        Buffer.MemoryCopy(source, target, targetLength, sourceLength);
        new Span<byte>(target + sourceLength, targetLength - sourceLength).Fill(Blank);
    }

    /// <summary>Sets two character values in order, byte by byte, the shorter taken as followed by blanks.</summary>
    /// <param name="left">The first value.</param>
    /// <param name="leftLength">The length of the first value.</param>
    /// <param name="right">The second value.</param>
    /// <param name="rightLength">The length of the second value.</param>
    /// <returns>A negative number when the first comes before the second, zero when they are equal, and a positive number otherwise.</returns>
    public static int Compare(byte* left, int leftLength, byte* right, int rightLength)
    {
        leftLength = Math.Max(leftLength, 0);
        rightLength = Math.Max(rightLength, 0);
        var common = Math.Min(leftLength, rightLength);

        var order = new ReadOnlySpan<byte>(left, common).SequenceCompareTo(new ReadOnlySpan<byte>(right, common));
        if (order != 0)
        {
            return order;
        }

        for (var index = common; index < leftLength; index++)
        {
            if (left[index] != Blank)
            {
                return left[index] < Blank ? -1 : 1;
            }
        }

        for (var index = common; index < rightLength; index++)
        {
            if (right[index] != Blank)
            {
                return right[index] > Blank ? -1 : 1;
            }
        }

        return 0;
    }

    /// <summary>Finds where one character value first occurs in another, as <c>INDEX</c> does.</summary>
    /// <param name="text">The value searched.</param>
    /// <param name="textLength">The length of the value searched.</param>
    /// <param name="sought">The value looked for.</param>
    /// <param name="soughtLength">The length of the value looked for.</param>
    /// <returns>The position of its first character, counted from one, or zero when it does not occur.</returns>
    public static int Index(byte* text, int textLength, byte* sought, int soughtLength)
    {
        if (soughtLength <= 0 || soughtLength > textLength)
        {
            return 0;
        }

        return new ReadOnlySpan<byte>(text, textLength).IndexOf(new ReadOnlySpan<byte>(sought, soughtLength)) + 1;
    }
}
