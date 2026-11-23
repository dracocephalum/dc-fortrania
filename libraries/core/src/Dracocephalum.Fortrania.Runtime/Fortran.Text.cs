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

    // How many bytes are fewer than it pays to hand to a search of the
    // framework's.
    private const int ShortRun = 8;

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

    /// <summary>Starts joining character values end to end into one place.</summary>
    /// <param name="target">Where the joined value is to lie.</param>
    /// <param name="capacity">The most it may hold; below one it holds nothing.</param>
    /// <returns>A concatenation with nothing added yet.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Concatenation Concat(byte* target, int capacity) => new(target, Math.Max(capacity, 0), 0);

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

    /// <summary>
    /// Tells whether two character values are equal, the shorter taken as
    /// followed by blanks, where the second is known to be the shorter or
    /// of the same length: a constant, nearly always. It says what
    /// <see cref="Compare"/> says by giving zero, for any two values, and
    /// does less to say it.
    /// </summary>
    /// <param name="value">The first value.</param>
    /// <param name="length">The length of the first value.</param>
    /// <param name="shorter">The second value.</param>
    /// <param name="shorterLength">The length of the second value. Where it is the greater after all, the answer is still right.</param>
    /// <returns>Whether they are equal.</returns>
    /// <remarks>
    /// Only the first bytes are compared where the test is used, and the
    /// rest is a call: most tests in a chain of them fail on that byte, and
    /// the compiler that writes this in line writes only so much in line
    /// in one method, of which the whole test would take several times
    /// as much. <c>DESIGN.md</c>, <em>Equality with a constant is tested
    /// as equality</em>, has the measurements.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsEqual(byte* value, int length, byte* shorter, int shorterLength) =>
        (length <= 0 || shorterLength <= 0 || *value == *shorter) && IsEqualInFull(value, length, shorter, shorterLength);

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool IsEqualInFull(byte* value, int length, byte* shorter, int shorterLength)
    {
        if (length < shorterLength || shorterLength < 0)
        {
            return Compare(value, length, shorter, shorterLength) == 0;
        }

        if (!new ReadOnlySpan<byte>(value, shorterLength).SequenceEqual(new ReadOnlySpan<byte>(shorter, shorterLength)))
        {
            return false;
        }

        // What is left of the longer has to be blank. A few bytes are
        // looked at one by one: the framework's search is faster over
        // many and costs more to begin than these take.
        var rest = length - shorterLength;
        if (rest >= ShortRun)
        {
            return !new ReadOnlySpan<byte>(value + shorterLength, rest).ContainsAnyExcept(Blank);
        }

        for (var index = shorterLength; index < length; index++)
        {
            if (value[index] != Blank)
            {
                return false;
            }
        }

        return true;
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
