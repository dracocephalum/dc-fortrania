// SPDX-License-Identifier: Apache-2.0
// Part of the Fortrania runtime, which code translated from FORTRAN 77 calls.

#nullable enable

using System;
using System.IO;

namespace Dracocephalum.Fortrania.Runtime;

/// <content>
/// Internal files, a character variable read and written as text, and
/// <c>BACKSPACE</c>. An internal file has as many records as the variable
/// has elements, each as long as one element: a variable that is not an
/// array is one record. Writing fills each record whole, blanks after what
/// is written; reading gives each record as it is. Nothing of a record is
/// written that does not fit it.
/// </content>
#if FORTRANIA_RUNTIME_INTERNAL
internal
#else
public
#endif
sealed unsafe partial class FortranIO
{
    private const int EndOfRecord = -2;
    private const int NotConnected = 5005;

    // The internal file of the statement in progress: its first byte, the
    // length of a record, how many records it has, and which is next.
    private byte* _internal;
    private int _internalLength;
    private int _internalRecords;
    private int _internalRecord;
    private byte[] _internalInput = [];

    private bool IsInternal => _internal != null;

    /// <summary>Begins a formatted <c>WRITE</c> to an internal file.</summary>
    /// <param name="text">The first byte of the character variable, array or substring written to.</param>
    /// <param name="length">The length of one record: of the variable, or of one element of the array.</param>
    /// <param name="records">The number of records: one, or the number of elements of the array.</param>
    /// <param name="format">The format, parentheses included.</param>
    public void WriteFormatted(byte* text, int length, int records, ReadOnlySpan<byte> format)
    {
        BeginInternal(text, length, records, Transfer.TextWrite);
        BeginFormat(format);
    }

    /// <summary>Begins a list-directed <c>WRITE</c> to an internal file.</summary>
    /// <param name="text">The first byte of the character variable, array or substring written to.</param>
    /// <param name="length">The length of one record.</param>
    /// <param name="records">The number of records.</param>
    public void WriteList(byte* text, int length, int records)
    {
        BeginInternal(text, length, records, Transfer.TextWrite);
        _isList = true;
        _wroteItem = false;
        _wroteText = false;
    }

    /// <summary>Begins a formatted <c>READ</c> from an internal file, which reads its first record.</summary>
    /// <param name="text">The first byte of the character variable, array or substring read from.</param>
    /// <param name="length">The length of one record.</param>
    /// <param name="records">The number of records.</param>
    /// <param name="format">The format, parentheses included.</param>
    public void ReadFormatted(byte* text, int length, int records, ReadOnlySpan<byte> format)
    {
        BeginInternal(text, length, records, Transfer.TextRead);
        BeginFormat(format);
        if (_status == 0)
        {
            NextRecord();
        }
    }

    /// <summary>Begins a list-directed <c>READ</c> from an internal file, which reads its first record.</summary>
    /// <param name="text">The first byte of the character variable, array or substring read from.</param>
    /// <param name="length">The length of one record.</param>
    /// <param name="records">The number of records.</param>
    public void ReadList(byte* text, int length, int records)
    {
        BeginInternal(text, length, records, Transfer.TextRead);
        _isList = true;
        _listEnded = false;
        _tokenRepeats = 0;
        NextRecord();
    }

    /// <summary>
    /// <c>BACKSPACE</c>: moves a unit opened for formatted sequential
    /// access back to the start of the record before. After a read found the
    /// end of the file, it moves back before that end, and the next read
    /// finds it again.
    /// </summary>
    /// <param name="unit">The unit number.</param>
    /// <returns>Zero, or what went wrong.</returns>
    public int Backspace(int unit)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        if (Find(unit) is not { } connected)
        {
            return Fail(NotConnected, $"Unit {unit} is not connected to a file, and cannot be backspaced.");
        }

        if (connected.IsDirect)
        {
            return Fail(OptionConflict, $"Unit {unit} is opened for direct access and cannot be backspaced.");
        }

        if (!connected.IsFormatted)
        {
            return Fail(OptionConflict, $"Unit {unit} is opened for unformatted sequential access, which cannot be backspaced yet.");
        }

        if (connected.IsAtEnd)
        {
            connected.IsAtEnd = false;
            return 0;
        }

        try
        {
            Settle(connected);
            connected.Position = StartOfRecordBefore(connected.File, connected.Position);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return Fail(StatusOf(exception), $"Unit {unit} could not be backspaced: {exception.Message}");
        }

        return 0;
    }

    // Where the record that ends at a position begins: back over its line
    // ending, whichever of the three it is, and on to the ending before it
    // or the start of the file.
    private static long StartOfRecordBefore(FortranFile file, long position)
    {
        Span<byte> before = stackalloc byte[2];
        if (position >= 1)
        {
            var count = (int)Math.Min(position, 2);
            file.Read(position - count, before[..count]);
            var last = before[count - 1];
            if (last == '\n')
            {
                position -= count == 2 && before[0] == '\r' ? 2 : 1;
            }
            else if (last == '\r')
            {
                position--;
            }
        }

        Span<byte> chunk = stackalloc byte[4096];
        while (position > 0)
        {
            var count = (int)Math.Min(position, chunk.Length);
            var read = chunk[..count];
            file.Read(position - count, read);
            var ending = read.LastIndexOfAny((byte)'\r', (byte)'\n');
            if (ending >= 0)
            {
                return position - count + ending + 1;
            }

            position -= count;
        }

        return 0;
    }

    private void BeginInternal(byte* text, int length, int records, Transfer transfer)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        _status = 0;
        _transfer = transfer;
        _isList = false;
        _screen = null;
        _keyboard = null;
        _current = null;
        _internal = text;
        _internalLength = Math.Max(length, 0);
        _internalRecords = Math.Max(records, 0);
        _internalRecord = 0;
        _lineLength = 0;
        _column = 0;
        _staysOnLine = false;
    }

    // The next record of an internal file, for reading.
    private void NextInternalRecord()
    {
        if (_internalRecord >= _internalRecords)
        {
            _status = Fail(EndOfFile, "An internal file was read past its last record.");
            return;
        }

        if (_internalInput.Length < _internalLength)
        {
            _internalInput = new byte[_internalLength];
        }

        new ReadOnlySpan<byte>(_internal + ((long)_internalRecord * _internalLength), _internalLength).CopyTo(_internalInput);
        _internalRecord++;
        SetInput(_internalInput, 0, _internalLength);
    }

    // The record put together, to the next record of an internal file.
    private void WriteInternalRecord()
    {
        if (_internalRecord >= _internalRecords)
        {
            _status = Fail(EndOfFile, "An internal file was written past its last record.");
        }
        else if (_lineLength > _internalLength)
        {
            _status = Fail(EndOfRecord, $"A record of {_lineLength} characters was written to an internal file whose records hold {_internalLength}.");
        }
        else
        {
            var record = new Span<byte>(_internal + ((long)_internalRecord * _internalLength), _internalLength);
            _line.AsSpan(0, _lineLength).CopyTo(record);
            record[_lineLength..].Fill(Blank);
            _internalRecord++;
        }

        _lineLength = 0;
        _column = 0;
    }
}
