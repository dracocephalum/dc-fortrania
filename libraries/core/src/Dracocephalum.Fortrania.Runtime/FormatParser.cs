// SPDX-License-Identifier: Apache-2.0
// Part of the Fortrania runtime, which code translated from FORTRAN 77 calls.

#nullable enable

using System;
using System.Collections.Generic;

namespace Dracocephalum.Fortrania.Runtime;

/// <summary>
/// Reads a format, the character value that says how a list of items is
/// laid out in records, into its items.
/// </summary>
internal ref struct FormatParser
{
    private readonly ReadOnlySpan<byte> _text;
    private int _position;

    private FormatParser(ReadOnlySpan<byte> text)
    {
        _text = text;
        _position = 0;
    }

    /// <summary>Reads a format.</summary>
    /// <param name="format">The format, parentheses included. What follows the closing one is not looked at.</param>
    /// <returns>Its items, or <see langword="null"/> where it is not a format this runtime reads.</returns>
    public static List<FormatItem>? Parse(ReadOnlySpan<byte> format)
    {
        var parser = new FormatParser(format);
        return parser.Peek() == '(' && parser.TryGroup(out var items) ? items : null;
    }

    // The next character that is not a blank, in upper case; zero at the
    // end. Blanks mean nothing in a format outside a character constant.
    private byte Peek()
    {
        while (_position < _text.Length && _text[_position] == ' ')
        {
            _position++;
        }

        if (_position >= _text.Length)
        {
            return 0;
        }

        var character = _text[_position];
        return character is >= (byte)'a' and <= (byte)'z' ? (byte)(character - 32) : character;
    }

    private bool TryNumber(out int number)
    {
        number = 0;
        var any = false;
        while (Peek() is >= (byte)'0' and <= (byte)'9' && number < 100_000_000)
        {
            number = (number * 10) + (_text[_position++] - '0');
            any = true;
        }

        return any;
    }

    // A list of items in parentheses; the opening one is next.
    private bool TryGroup(out List<FormatItem> items)
    {
        items = [];
        _position++;

        while (true)
        {
            switch (Peek())
            {
                case 0:
                    return false;
                case (byte)')':
                    _position++;
                    return true;
                case (byte)',':
                    _position++;
                    continue;
            }

            if (!TryItem(items))
            {
                return false;
            }
        }
    }

    private bool TryItem(List<FormatItem> items)
    {
        var negative = false;
        if (Peek() is (byte)'-' or (byte)'+')
        {
            negative = _text[_position++] == '-';
            if (!TryNumber(out var scale) || Peek() != 'P')
            {
                return false;
            }

            _position++;
            items.Add(new FormatItem { Kind = FormatKind.Scale, Scale = negative ? -scale : scale });
            return true;
        }

        var counted = TryNumber(out var count);
        var letter = Peek();
        if (letter == 0)
        {
            return false;
        }

        _position++;
        switch (letter)
        {
            case (byte)'(':
                _position--;
                if (!TryGroup(out var inner))
                {
                    return false;
                }

                items.Add(new FormatItem { Kind = FormatKind.Group, Repeat = counted ? count : 1, Items = inner });
                return true;

            case (byte)'P' when counted:
                items.Add(new FormatItem { Kind = FormatKind.Scale, Scale = count });
                return true;

            case (byte)'X':
                items.Add(new FormatItem { Kind = FormatKind.Skip, Width = counted ? count : 1 });
                return true;

            case (byte)'/':
                items.Add(new FormatItem { Kind = FormatKind.Slash, Repeat = counted ? count : 1 });
                return true;

            case (byte)':' when !counted:
                items.Add(new FormatItem { Kind = FormatKind.Colon });
                return true;

            case (byte)'$' when !counted:
                items.Add(new FormatItem { Kind = FormatKind.Dollar });
                return true;

            case (byte)'\'' when !counted:
                return TryLiteral(items);

            case (byte)'A':
                items.Add(new FormatItem { Kind = FormatKind.Text, Repeat = counted ? count : 1, Width = TryNumber(out var width) ? width : -1 });
                return true;

            case (byte)'L':
                if (!TryNumber(out var logicalWidth))
                {
                    return false;
                }

                items.Add(new FormatItem { Kind = FormatKind.Logical, Repeat = counted ? count : 1, Width = logicalWidth });
                return true;

            case (byte)'I':
                if (!TryNumber(out var integerWidth))
                {
                    return false;
                }

                var least = -1;
                if (Peek() == '.')
                {
                    _position++;
                    if (!TryNumber(out least))
                    {
                        return false;
                    }
                }

                items.Add(new FormatItem { Kind = FormatKind.Integer, Repeat = counted ? count : 1, Width = integerWidth, Digits = least });
                return true;

            case (byte)'F' or (byte)'E' or (byte)'D':
                return TryReal(items, letter, counted ? count : 1);

            default:
                return false;
        }
    }

    private bool TryReal(List<FormatItem> items, byte letter, int repeat)
    {
        if (!TryNumber(out var width) || Peek() != '.')
        {
            return false;
        }

        _position++;
        if (!TryNumber(out var digits))
        {
            return false;
        }

        var exponent = -1;
        if (letter == 'E' && Peek() == 'E')
        {
            _position++;
            if (!TryNumber(out exponent) || exponent == 0)
            {
                return false;
            }
        }

        items.Add(new FormatItem
        {
            Kind = letter == 'F' ? FormatKind.Fixed : FormatKind.Exponent,
            Repeat = repeat,
            Width = width,
            Digits = digits,
            ExponentDigits = exponent,
            Letter = letter,
        });
        return true;
    }

    // A character constant; its opening apostrophe has been read. Two
    // apostrophes inside it stand for one.
    private bool TryLiteral(List<FormatItem> items)
    {
        var characters = new List<byte>();
        while (_position < _text.Length)
        {
            var character = _text[_position++];
            if (character != '\'')
            {
                characters.Add(character);
            }
            else if (_position < _text.Length && _text[_position] == '\'')
            {
                characters.Add(character);
                _position++;
            }
            else
            {
                items.Add(new FormatItem { Kind = FormatKind.Literal, Characters = [.. characters] });
                return true;
            }
        }

        return false;
    }
}
