// SPDX-License-Identifier: Apache-2.0
// Part of the Fortrania runtime, which code translated from FORTRAN 77 calls.

#nullable enable

using System;
using System.Globalization;
using System.Text;

namespace Dracocephalum.Fortrania.Runtime;

/// <content>
/// List-directed transfer: text with no format, where each item's type
/// says how it is written and the values read are told apart by blanks and
/// commas. The language leaves the layout of what is written to the
/// compiler, and this is the reference compiler's: a record begins with a
/// blank, an integer takes eleven columns, a number is written to all the
/// digits its type holds, and a blank stands between two items unless both
/// are character values.
/// </content>
#if FORTRANIA_RUNTIME_INTERNAL
internal
#else
public
#endif
sealed unsafe partial class FortranIO
{
    private const byte Tab = 9;

    // Whether the statement in progress is list-directed, and for one that
    // writes, whether it has written an item and whether that was text.
    private bool _isList;
    private bool _wroteItem;
    private bool _wroteText;

    // The value last read: its characters, whether it stood in quotes,
    // whether it was no value at all, and how often it is still to be
    // given, since a value may be written once with a count before it.
    private byte[] _token = new byte[64];
    private int _tokenLength;
    private bool _tokenIsQuoted;
    private bool _tokenIsNull;
    private int _tokenRepeats;

    // Whether a slash has ended the list: the items left keep their values.
    private bool _listEnded;

    /// <summary>Begins a list-directed <c>WRITE</c> or <c>PRINT</c>.</summary>
    /// <param name="unit">The unit number; 6 where the statement has an asterisk.</param>
    public void WriteList(int unit)
    {
        if (BeginWrite(unit))
        {
            _isList = true;
            _wroteItem = false;
            _wroteText = false;
        }
    }

    /// <summary>Begins a list-directed <c>READ</c>, which reads the first record.</summary>
    /// <param name="unit">The unit number; 5 where the statement has an asterisk.</param>
    public void ReadList(int unit)
    {
        if (BeginRead(unit))
        {
            _isList = true;
            _listEnded = false;
            _tokenRepeats = 0;
            NextRecord();
        }
    }

    private void ListWrite(ReadOnlySpan<byte> text)
    {
        if (_status != 0)
        {
            return;
        }

        if (!_wroteItem || !_wroteText)
        {
            PutBytes(" "u8);
        }

        PutBytes(text);
        _wroteItem = true;
        _wroteText = true;
    }

    private void ListWrite(string field)
    {
        if (_status != 0)
        {
            return;
        }

        PutBytes(" "u8);
        Put(field);
        _wroteItem = true;
        _wroteText = false;
    }

    // Reads the next value of the list into the token. False where the
    // item is to keep the value it has, or the statement has failed.
    private bool NextValue()
    {
        if (_status != 0 || _listEnded)
        {
            return false;
        }

        if (_tokenRepeats > 0)
        {
            _tokenRepeats--;
            return !_tokenIsNull;
        }

        // Blanks and the ends of records lie between values.
        while (true)
        {
            while (_column < _inputLength && Current is Blank or Tab)
            {
                _column++;
            }

            if (_column < _inputLength)
            {
                break;
            }

            NextRecord();
            if (_status != 0)
            {
                return false;
            }
        }

        _tokenLength = 0;
        _tokenIsQuoted = false;
        _tokenIsNull = false;

        if (Current == '/')
        {
            _listEnded = true;
            return false;
        }

        // A count and an asterisk before a value give it that many times.
        var start = _column;
        var count = 0;
        while (_column < _inputLength && Current is >= (byte)'0' and <= (byte)'9' && count < 100_000_000)
        {
            count = (count * 10) + (Current - '0');
            _column++;
        }

        if (_column > start && _column < _inputLength && Current == '*' && count > 0)
        {
            _column++;
            _tokenRepeats = count - 1;
        }
        else
        {
            _column = start;
        }

        if (_column >= _inputLength || Current is Blank or Tab or (byte)',' or (byte)'/')
        {
            // Nothing stands where a value would: two commas in a row, or
            // a count with nothing after it.
            _tokenIsNull = true;
        }
        else if (Current is (byte)'\'' or (byte)'"')
        {
            ReadQuoted(Current);
        }
        else
        {
            while (_column < _inputLength && Current is not (Blank or Tab or (byte)',' or (byte)'/'))
            {
                Append(Current);
                _column++;
            }
        }

        // One comma after a value belongs to it.
        while (_column < _inputLength && Current is Blank or Tab)
        {
            _column++;
        }

        if (_column < _inputLength && Current == ',')
        {
            _column++;
        }

        return _status == 0 && !_tokenIsNull;
    }

    // A value in apostrophes or quotation marks. Two of the mark stand for
    // one, and the value goes on into the next record where this one ends
    // before it does.
    private void ReadQuoted(byte mark)
    {
        _tokenIsQuoted = true;
        _column++;
        while (true)
        {
            if (_column >= _inputLength)
            {
                NextRecord();
                if (_status != 0)
                {
                    return;
                }

                continue;
            }

            var character = Current;
            _column++;
            if (character != mark)
            {
                Append(character);
            }
            else if (_column < _inputLength && Current == mark)
            {
                Append(mark);
                _column++;
            }
            else
            {
                return;
            }
        }
    }

    private byte Current => _input[_inputStart + _column];

    private void Append(byte character)
    {
        if (_tokenLength == _token.Length)
        {
            Array.Resize(ref _token, _token.Length * 2);
        }

        _token[_tokenLength++] = character;
    }

    private void ListRead(byte* text, int length)
    {
        if (NextValue())
        {
            var target = new Span<byte>(text, Math.Max(length, 0));
            var value = _token.AsSpan(0, Math.Min(_tokenLength, target.Length));
            value.CopyTo(target);
            target[value.Length..].Fill(Blank);
        }
    }

    private void ListRead(int* value)
    {
        if (NextValue())
        {
            if (!_tokenIsQuoted && int.TryParse(_token.AsSpan(0, _tokenLength), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var number))
            {
                *value = number;
            }
            else
            {
                BadListValue("an integer");
            }
        }
    }

    private bool ListRead(double* value)
    {
        if (!NextValue())
        {
            return false;
        }

        if (!_tokenIsQuoted && NumberEditing.TryReal(_token.AsSpan(0, _tokenLength), decimals: 0, scale: 0, out *value))
        {
            return true;
        }

        BadListValue("a number");
        return false;
    }

    private void ListReadLogical(int* value)
    {
        if (NextValue())
        {
            if (!_tokenIsQuoted && NumberEditing.TryLogical(_token.AsSpan(0, _tokenLength), out var answer))
            {
                *value = answer ? 1 : 0;
            }
            else
            {
                BadListValue("a logical value");
            }
        }
    }

    private void BadListValue(string wanted) =>
        _status = Fail(BadValue, $"A value read as {wanted} is not one: {Encoding.UTF8.GetString(_token, 0, _tokenLength)}");
}
