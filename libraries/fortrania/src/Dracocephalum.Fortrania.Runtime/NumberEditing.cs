// SPDX-License-Identifier: Apache-2.0
// Part of the Fortrania runtime, which code translated from FORTRAN 77 calls.

#nullable enable

using System;
using System.Buffers;
using System.Globalization;
using System.Text;

namespace Dracocephalum.Fortrania.Runtime;

/// <summary>
/// Turns numbers into the characters a format asks for, and back. A field
/// too narrow for its number is filled with asterisks, as the language has
/// it. Rounding is of the number's exact value, and a value exactly half
/// way goes to the even digit, as in the reference compiler's library.
/// </summary>
internal static class NumberEditing
{
    // More digits than any number of 64 bits has after its first, so that
    // asking for this many gives the exact value.
    private const int ExactDigits = 770;

    private static readonly SearchValues<char> MantissaCharacters = SearchValues.Create("+-.0123456789");

    /// <summary><c>Iw</c> or <c>Iw.m</c>.</summary>
    /// <param name="value">The number.</param>
    /// <param name="width">The field's width.</param>
    /// <param name="least">The fewest digits to write, or -1 for no such limit. With zero, a zero is written as blanks.</param>
    /// <returns>The field, <paramref name="width"/> characters long.</returns>
    public static string Integer(long value, int width, int least)
    {
        var digits = Math.Abs((decimal)value).ToString(CultureInfo.InvariantCulture);
        if (least == 0 && value == 0)
        {
            digits = string.Empty;
        }
        else if (least > digits.Length)
        {
            digits = digits.PadLeft(least, '0');
        }

        return Field(value < 0 ? "-" + digits : digits, width);
    }

    /// <summary><c>Lw</c>.</summary>
    /// <param name="value">The value.</param>
    /// <param name="width">The field's width.</param>
    /// <returns>The field: <c>T</c> or <c>F</c> at its right end.</returns>
    public static string Logical(bool value, int width) => Field(value ? "T" : "F", width);

    /// <summary><c>Fw.d</c>.</summary>
    /// <param name="value">The number.</param>
    /// <param name="width">The field's width.</param>
    /// <param name="decimals">The digits after the point.</param>
    /// <returns>The field, <paramref name="width"/> characters long.</returns>
    public static string Fixed(double value, int width, int decimals)
    {
        if (!double.IsFinite(value))
        {
            return NotFinite(value, width);
        }

        var sign = double.IsNegative(value) ? "-" : string.Empty;
        var (digits, exponent) = Round(Math.Abs(value), significant: null, decimals);

        // The digits stand for 0.d1d2... times ten to the exponent; the
        // point goes after the first so many of them.
        var whole = exponent > 0 ? digits[..Math.Min(exponent, digits.Length)].PadRight(exponent, '0') : string.Empty;
        var fraction = exponent >= 0
            ? (exponent < digits.Length ? digits[exponent..] : string.Empty)
            : new string('0', -exponent) + digits;
        fraction = fraction.PadRight(decimals, '0')[..decimals];

        return Pointed(sign, whole, fraction, string.Empty, width);
    }

    /// <summary><c>Ew.d</c>, <c>Ew.dEe</c> or <c>Dw.d</c>.</summary>
    /// <param name="value">The number.</param>
    /// <param name="width">The field's width.</param>
    /// <param name="decimals">The digits after the point.</param>
    /// <param name="exponentDigits">The digits of the exponent, or -1 where the format gives none.</param>
    /// <param name="letter">The letter before the exponent.</param>
    /// <param name="scale">The scale factor in effect: how many digits stand before the point.</param>
    /// <returns>The field, <paramref name="width"/> characters long.</returns>
    public static string Exponent(double value, int width, int decimals, int exponentDigits, char letter, int scale)
    {
        if (!double.IsFinite(value))
        {
            return NotFinite(value, width);
        }

        // With no scale factor the digits follow the point. A positive one
        // puts so many digits before it and writes one digit more in all;
        // a negative one puts so many zeros after it in place of digits.
        var significant = scale > 0 ? decimals + 1 : decimals + scale;
        if (scale > decimals + 1 || significant < 1)
        {
            return new string('*', width);
        }

        var sign = double.IsNegative(value) ? "-" : string.Empty;
        var digits = new string('0', significant);
        var exponent = 0;
        if (value != 0)
        {
            (digits, exponent) = Round(Math.Abs(value), significant, decimals: null);
            exponent -= scale;
        }

        var whole = scale > 0 ? digits[..scale] : string.Empty;
        var fraction = scale > 0 ? digits[scale..] : new string('0', -scale) + digits;

        var size = Math.Abs(exponent).ToString(CultureInfo.InvariantCulture);
        var mark = exponent < 0 ? "-" : "+";
        string tail;
        if (exponentDigits > 0)
        {
            if (size.Length > exponentDigits)
            {
                return new string('*', width);
            }

            tail = letter + mark + size.PadLeft(exponentDigits, '0');
        }
        else if (size.Length <= 2)
        {
            tail = letter + mark + size.PadLeft(2, '0');
        }
        else if (size.Length == 3)
        {
            // Three digits take the place of the letter.
            tail = mark + size;
        }
        else
        {
            return new string('*', width);
        }

        return Pointed(sign, whole, fraction, tail, width);
    }

    /// <summary>Reads a field as an integer.</summary>
    /// <param name="field">The field's characters. Blanks in it do not count, and a field of blanks is zero.</param>
    /// <param name="value">The number.</param>
    /// <returns>Whether the field holds an integer that fits.</returns>
    public static bool TryInteger(ReadOnlySpan<byte> field, out int value)
    {
        var text = Compact(field);
        if (text.Length == 0)
        {
            value = 0;
            return true;
        }

        return int.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out value);
    }

    /// <summary>Reads a field as a number, for <c>F</c>, <c>E</c> or <c>D</c>.</summary>
    /// <param name="field">The field's characters. Blanks in it do not count, and a field of blanks is zero.</param>
    /// <param name="decimals">Where the field has no point: how many of its last digits follow one.</param>
    /// <param name="scale">The scale factor in effect, which a field without an exponent is divided by ten to the power of.</param>
    /// <param name="value">The number.</param>
    /// <returns>Whether the field holds a number.</returns>
    public static bool TryReal(ReadOnlySpan<byte> field, int decimals, int scale, out double value)
    {
        var text = Compact(field).ToUpperInvariant();
        value = 0;
        if (text.Length == 0)
        {
            return true;
        }

        // The exponent may come after E or D, or after a sign alone.
        var split = text.IndexOfAny(['E', 'D']);
        if (split < 0)
        {
            split = text.IndexOfAny(['+', '-'], 1);
        }

        var mantissa = split < 0 ? text : text[..split];
        var exponent = 0;
        if (split >= 0)
        {
            var power = text[split] is 'E' or 'D' ? text[(split + 1)..] : text[split..];
            if (power.Length > 0 && !int.TryParse(power, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out exponent))
            {
                return false;
            }
        }
        else
        {
            exponent = -scale;
        }

        if (!mantissa.Contains('.', StringComparison.Ordinal))
        {
            exponent -= decimals;
        }

        return mantissa.Length > 0
            && mantissa.AsSpan().IndexOfAnyExcept(MantissaCharacters) < 0
            && double.TryParse(
                string.Create(CultureInfo.InvariantCulture, $"{mantissa}E{exponent}"),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out value);
    }

    /// <summary>Reads a field as a logical value: its first letter decides, after blanks and an optional point.</summary>
    /// <param name="field">The field's characters.</param>
    /// <param name="value">The value.</param>
    /// <returns>Whether the field begins with <c>T</c> or <c>F</c>.</returns>
    public static bool TryLogical(ReadOnlySpan<byte> field, out bool value)
    {
        var text = Compact(field).ToUpperInvariant().TrimStart('.');
        value = text.StartsWith('T');
        return value || text.StartsWith('F');
    }

    private static string Compact(ReadOnlySpan<byte> field) =>
        Encoding.ASCII.GetString(field).Replace(" ", string.Empty, StringComparison.Ordinal);

    // Sets a number with a point into its field. The zero before the
    // point is optional and goes first when the field is too narrow.
    private static string Pointed(string sign, string whole, string fraction, string tail, int width)
    {
        var rest = "." + fraction + tail;
        if (whole.Length == 0 && sign.Length + 1 + rest.Length <= width)
        {
            whole = "0";
        }

        return Field(sign + whole + rest, width);
    }

    private static string NotFinite(double value, int width)
    {
        if (double.IsNaN(value))
        {
            return Field("NaN", width);
        }

        var sign = value < 0 ? "-" : string.Empty;
        return Field(sign.Length + 8 <= width ? sign + "Infinity" : sign + "Inf", width);
    }

    // Puts what is to be written at the right end of its field. A width of
    // zero takes whatever the number needs.
    private static string Field(string text, int width)
    {
        if (width == 0)
        {
            return text;
        }

        return text.Length > width ? new string('*', width) : text.PadLeft(width);
    }

    // Rounds a positive number to so many significant digits, or to so
    // many decimals, and gives the digits kept with the power of ten that
    // makes them the number when a point is put before the first.
    private static (string Digits, int Exponent) Round(double value, int? significant, int? decimals)
    {
        if (value == 0)
        {
            return (new string('0', significant ?? 0), 0);
        }

        // Twenty digits more than could be kept say which way to round in
        // all but the rarest case.
        var first = Split(value.ToString("E20", CultureInfo.InvariantCulture)).Exponent;
        var kept = significant ?? first + decimals!.Value;
        if (kept < 0)
        {
            // Its first digit lies after the last place kept, and not next
            // to it: nothing is left of the number.
            return (string.Empty, 0);
        }

        var (digits, exponent) = Split(value.ToString("E" + (kept + 20).ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture));

        var tail = digits.AsSpan(kept);
        var up = tail[0] >= '5';
        if (tail[0] == '5' && tail[1..].IndexOfAnyExcept('0') < 0)
        {
            // It looks exactly half way, which twenty digits cannot settle:
            // the number's whole expansion can.
            var exact = Split(value.ToString("E" + ExactDigits.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture)).Digits.AsSpan(kept);
            var isHalf = exact[0] == '5' && exact[1..].IndexOfAnyExcept('0') < 0;
            up = isHalf ? kept > 0 && (digits[kept - 1] - '0') % 2 == 1 : exact[0] >= '5';
        }

        var result = digits[..kept].ToCharArray();
        if (up)
        {
            var index = kept - 1;
            while (index >= 0 && result[index] == '9')
            {
                result[index--] = '0';
            }

            if (index >= 0)
            {
                result[index]++;
            }
            else
            {
                // Every digit was a nine, or none was kept: the number
                // rounds up to a one in the place before.
                return (significant is null ? "1" + new string(result) : ("1" + new string(result))[..Math.Max(kept, 1)], exponent + 1);
            }
        }

        return (new string(result), exponent);
    }

    // Takes d.ddddE+xx apart into its digits and the power of ten that
    // goes with a point before the first of them.
    private static (string Digits, int Exponent) Split(string scientific)
    {
        var mark = scientific.IndexOf('E', StringComparison.Ordinal);
        var digits = scientific[..mark].Replace(".", string.Empty, StringComparison.Ordinal);
        return (digits, int.Parse(scientific.AsSpan(mark + 1), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture) + 1);
    }
}
