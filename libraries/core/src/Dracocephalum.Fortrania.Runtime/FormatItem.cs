// SPDX-License-Identifier: Apache-2.0
// Part of the Fortrania runtime, which code translated from FORTRAN 77 calls.

#nullable enable

using System.Collections.Generic;

namespace Dracocephalum.Fortrania.Runtime;

/// <summary>One item of a format, with the numbers written around its letter.</summary>
internal sealed class FormatItem
{
    public FormatKind Kind { get; init; }

    // How often the item is used before the next one; for a slash, how
    // many records end.
    public int Repeat { get; init; } = 1;

    // The field's width, or the count of columns passed over; -1 where an
    // A has none and takes the item's own length.
    public int Width { get; init; } = -1;

    // The digits after the point; for an integer, the fewest digits.
    public int Digits { get; init; } = -1;

    // The digits of an exponent, where the format gives them.
    public int ExponentDigits { get; init; } = -1;

    // The letter an exponent is written with.
    public byte Letter { get; init; }

    public int Scale { get; init; }

    public byte[] Characters { get; init; } = [];

    public List<FormatItem> Items { get; init; } = [];
}
