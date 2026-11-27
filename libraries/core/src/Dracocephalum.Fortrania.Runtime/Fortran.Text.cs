// SPDX-License-Identifier: Apache-2.0
// Part of the Fortrania runtime, which code translated from FORTRAN 77 calls.

#nullable enable

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

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

    // How many bytes at most are copied or filled here, a few words at a
    // time, and not by the framework.
    private const uint LongestInPieces = 64;

    private const ulong EightBlanks = 0x2020202020202020;
    private const uint FourBlanks = 0x20202020;

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
    /// <remarks>
    /// Up to sixty-four bytes are copied and filled here, a few
    /// words at a time, and only more are handed to the framework, whose
    /// copy and fill are made for any number of bytes and take longer to
    /// begin than a name of a few characters takes to move. <c>DESIGN.md</c>,
    /// <em>A short character value is assigned without the framework</em>,
    /// has the measurements.
    /// </remarks>
    public static void Assign(byte* target, int targetLength, byte* source, int sourceLength)
    {
        if (sourceLength >= targetLength)
        {
            if (targetLength > 0)
            {
                Copy(target, source, (uint)targetLength);
            }

            return;
        }

        if (targetLength <= 0)
        {
            return;
        }

        if (sourceLength > 0)
        {
            Copy(target, source, (uint)sourceLength);
        }
        else
        {
            sourceLength = 0;
        }

        FillWithBlanks(target + sourceLength, (uint)(targetLength - sourceLength));
    }

    // Copies one to LongestInPieces bytes by reading two or four pieces that
    // cover them, overlapping where the count is no multiple of a piece,
    // and only then storing: so the two places may overlap, as the
    // framework's copy lets them.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Copy(byte* target, byte* source, uint count)
    {
        if (count >= 16)
        {
            if (count > LongestInPieces || !Vector128.IsHardwareAccelerated)
            {
                Buffer.MemoryCopy(source, target, count, count);
            }
            else if (count > 32)
            {
                var first = Vector128.Load(source);
                var second = Vector128.Load(source + 16);
                var third = Vector128.Load(source + count - 32);
                var fourth = Vector128.Load(source + count - 16);
                first.Store(target);
                second.Store(target + 16);
                third.Store(target + count - 32);
                fourth.Store(target + count - 16);
            }
            else
            {
                var first = Vector128.Load(source);
                var last = Vector128.Load(source + count - 16);
                first.Store(target);
                last.Store(target + count - 16);
            }
        }
        else if (count >= 8)
        {
            var first = Unsafe.ReadUnaligned<ulong>(source);
            var last = Unsafe.ReadUnaligned<ulong>(source + count - 8);
            Unsafe.WriteUnaligned(target, first);
            Unsafe.WriteUnaligned(target + count - 8, last);
        }
        else if (count >= 4)
        {
            var first = Unsafe.ReadUnaligned<uint>(source);
            var last = Unsafe.ReadUnaligned<uint>(source + count - 4);
            Unsafe.WriteUnaligned(target, first);
            Unsafe.WriteUnaligned(target + count - 4, last);
        }
        else
        {
            // One to three bytes: the first, the middle and the last are
            // all of them, some taken twice.
            var first = source[0];
            var middle = source[count >> 1];
            var last = source[count - 1];
            target[0] = first;
            target[count >> 1] = middle;
            target[count - 1] = last;
        }
    }

    // Fills one byte or more with blanks, in pieces as Copy copies.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void FillWithBlanks(byte* target, uint count)
    {
        if (count >= 16)
        {
            if (count > LongestInPieces || !Vector128.IsHardwareAccelerated)
            {
                new Span<byte>(target, (int)count).Fill(Blank);
                return;
            }

            var blanks = Vector128.Create(Blank);
            if (count > 32)
            {
                blanks.Store(target + 16);
                blanks.Store(target + count - 32);
            }

            blanks.Store(target);
            blanks.Store(target + count - 16);
        }
        else if (count >= 8)
        {
            Unsafe.WriteUnaligned(target, EightBlanks);
            Unsafe.WriteUnaligned(target + count - 8, EightBlanks);
        }
        else if (count >= 4)
        {
            Unsafe.WriteUnaligned(target, FourBlanks);
            Unsafe.WriteUnaligned(target + count - 4, FourBlanks);
        }
        else
        {
            target[0] = Blank;
            target[count >> 1] = Blank;
            target[count - 1] = Blank;
        }
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
    /// followed by blanks. It says what <see cref="Compare"/> says by
    /// giving zero, for any two values, and does less to say it.
    /// </summary>
    /// <param name="left">The first value.</param>
    /// <param name="leftLength">The length of the first value.</param>
    /// <param name="right">The second value.</param>
    /// <param name="rightLength">The length of the second value.</param>
    /// <returns>Whether they are equal.</returns>
    /// <remarks>
    /// Only the first bytes are compared where the test is used, and the
    /// rest is a call: most tests in a chain of them fail on that byte, and
    /// the compiler that writes this in line writes only so much in line
    /// in one method, of which the whole test would take several times
    /// as much. <c>DESIGN.md</c>, <em>Equality of character values is
    /// tested as equality</em>, has the measurements.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsEqual(byte* left, int leftLength, byte* right, int rightLength) =>
        (leftLength <= 0 || rightLength <= 0 || *left == *right) && IsEqualInFull(left, leftLength, right, rightLength);

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool IsEqualInFull(byte* longer, int length, byte* shorter, int shorterLength)
    {
        // Equality does not mind which of the two is the longer.
        if (length < shorterLength)
        {
            var address = longer;
            longer = shorter;
            shorter = address;
            (length, shorterLength) = (shorterLength, length);
        }

        shorterLength = Math.Max(shorterLength, 0);
        if (!new ReadOnlySpan<byte>(longer, shorterLength).SequenceEqual(new ReadOnlySpan<byte>(shorter, shorterLength)))
        {
            return false;
        }

        // What is left of the longer has to be blank. A few bytes are
        // looked at one by one: the framework's search is faster over
        // many and costs more to begin than these take.
        var rest = length - shorterLength;
        if (rest >= ShortRun)
        {
            return !new ReadOnlySpan<byte>(longer + shorterLength, rest).ContainsAnyExcept(Blank);
        }

        for (var index = shorterLength; index < length; index++)
        {
            if (longer[index] != Blank)
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
