// SPDX-License-Identifier: Apache-2.0
// Part of the Fortrania runtime, which code translated from FORTRAN 77 calls.

#nullable enable

namespace Dracocephalum.Fortrania.Runtime;

/// <summary>What one item of a format does.</summary>
internal enum FormatKind
{
    /// <summary>A parenthesized list of items, which may be repeated.</summary>
    Group,

    /// <summary><c>A</c>: characters.</summary>
    Text,

    /// <summary><c>I</c>: an integer.</summary>
    Integer,

    /// <summary><c>F</c>: a number with a fixed count of decimals.</summary>
    Fixed,

    /// <summary><c>E</c> or <c>D</c>: a number with an exponent.</summary>
    Exponent,

    /// <summary><c>L</c>: a logical value.</summary>
    Logical,

    /// <summary><c>X</c>: columns passed over.</summary>
    Skip,

    /// <summary>A slash: the end of a record.</summary>
    Slash,

    /// <summary>A colon: where format control stops when the list is used up.</summary>
    Colon,

    /// <summary>A dollar sign: the record is left open at its end.</summary>
    Dollar,

    /// <summary><c>P</c>: the scale factor.</summary>
    Scale,

    /// <summary>Characters written as they stand.</summary>
    Literal,
}
