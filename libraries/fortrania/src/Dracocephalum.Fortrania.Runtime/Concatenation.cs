// SPDX-License-Identifier: Apache-2.0
// Part of the Fortrania runtime, which code translated from FORTRAN 77 calls.

#nullable enable

using System;
using System.Runtime.CompilerServices;

namespace Dracocephalum.Fortrania.Runtime;

/// <summary>
/// Character values being joined end to end into one place, as the
/// <c>//</c> operator joins them: each is added after the last, and what
/// does not fit is dropped. <see cref="Fortran.Concat"/> starts one.
/// </summary>
#if FORTRANIA_RUNTIME_INTERNAL
internal
#else
public
#endif
unsafe readonly struct Concatenation
{
    private readonly byte* _target;
    private readonly int _capacity;
    private readonly int _length;

    internal Concatenation(byte* target, int capacity, int length)
    {
        _target = target;
        _capacity = capacity;
        _length = length;
    }

    /// <summary>Adds a value after what has been added so far.</summary>
    /// <param name="text">The value. It may not overlap the place being joined into.</param>
    /// <param name="length">Its length; below one it is an empty value.</param>
    /// <returns>The concatenation, with the value added.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Concatenation Add(byte* text, int length)
    {
        var count = Math.Min(length, _capacity - _length);
        if (count <= 0)
        {
            return this;
        }

        Buffer.MemoryCopy(text, _target + _length, count, count);
        return new Concatenation(_target, _capacity, _length + count);
    }

    /// <summary>
    /// Ends a concatenation made straight into the variable it is assigned
    /// to, by filling what room is left with blanks.
    /// </summary>
    public void Pad() => new Span<byte>(_target + _length, _capacity - _length).Fill((byte)' ');

    /// <summary>Ends a concatenation made into a place set aside for it, and gives the value it came to.</summary>
    /// <param name="length">Where to store the value's length.</param>
    /// <returns>The address of the value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte* End(int* length)
    {
        *length = _length;
        return _target;
    }
}
