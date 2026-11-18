// SPDX-License-Identifier: Apache-2.0
// Part of the Fortrania runtime, which code translated from FORTRAN 77 calls.

#nullable enable

using System;
using System.IO;

namespace Dracocephalum.Fortrania.Runtime;

/// <content>
/// Unformatted transfer by record number. A <c>READ</c> or <c>WRITE</c> is
/// three kinds of call: one that names the unit and the record, one for
/// each item of the list, and one that ends the statement and gives its
/// status. After the first failure the rest of the statement does nothing.
/// Items are copied as the bytes they are, in the machine's own order.
/// </content>
#if FORTRANIA_RUNTIME_INTERNAL
internal
#else
public
#endif
sealed unsafe partial class FortranIO
{
    // The statement in progress: its unit, where its record begins in the
    // file, how far into the record the items have come, and what has gone
    // wrong so far.
    private Unit? _current;
    private long _recordStart;
    private int _recordPosition;
    private int _status;
    private bool _isLoaded;

    // The record being read or put together, which grows to the longest
    // record met.
    private byte[] _record = [];

    /// <summary>Begins a <c>READ</c> of one record of a unit opened for unformatted direct access.</summary>
    /// <param name="unit">The unit number.</param>
    /// <param name="record">The number of the record, counted from one.</param>
    public void ReadDirect(int unit, int record)
    {
        Begin(unit, record);
        _isLoaded = false;
    }

    /// <summary>Begins a <c>WRITE</c> of one record of a unit opened for unformatted direct access.</summary>
    /// <param name="unit">The unit number.</param>
    /// <param name="record">The number of the record, counted from one.</param>
    public void WriteDirect(int unit, int record)
    {
        Begin(unit, record);
        if (_status != 0)
        {
            return;
        }

        if (!_current!.File.CanWrite)
        {
            _status = Fail(NotPermitted, $"Unit {unit} is connected to a file that cannot be written.");
            return;
        }

        // What the items do not fill is written as zeros.
        Array.Clear(_record, 0, _current.RecordLength);
    }

    /// <summary>Reads the next item of the list.</summary>
    /// <param name="address">Where the item is: a variable, or the first element of an array.</param>
    /// <param name="bytes">The bytes it takes, all elements together.</param>
    public void Get(void* address, int bytes)
    {
        if (_status != 0)
        {
            return;
        }

        var unit = _current!;
        if (bytes > unit.RecordLength - _recordPosition)
        {
            _status = Fail(ShortRecord, $"Unit {unit.Number} was read past the end of a record of {unit.RecordLength} bytes.");
            return;
        }

        var item = new Span<byte>(address, bytes);
        try
        {
            if (!_isLoaded)
            {
                // An item that is the whole record is read straight to
                // where it goes.
                var whole = bytes == unit.RecordLength;
                var target = whole ? item : _record.AsSpan(0, unit.RecordLength);
                var read = unit.File.Read(_recordStart, target);
                if (read == 0)
                {
                    _status = Fail(BadOption, $"Unit {unit.Number} has no record at the number asked for.");
                    return;
                }

                // A last record that the file ends in the middle of counts
                // as followed by zeros.
                target[read..].Clear();
                _isLoaded = true;
                if (whole)
                {
                    _recordPosition = bytes;
                    return;
                }
            }

            _record.AsSpan(_recordPosition, bytes).CopyTo(item);
            _recordPosition += bytes;
        }
        catch (IOException exception)
        {
            _status = Fail(SystemFailure, $"Unit {unit.Number} could not be read: {exception.Message}");
        }
    }

    /// <summary>Writes the next item of the list.</summary>
    /// <param name="address">Where the item is: a variable, or the first element of an array.</param>
    /// <param name="bytes">The bytes it takes, all elements together.</param>
    public void Put(void* address, int bytes)
    {
        if (_status != 0)
        {
            return;
        }

        var unit = _current!;
        if (bytes > unit.RecordLength - _recordPosition)
        {
            _status = Fail(RecordOverrun, $"Unit {unit.Number} was written past the end of a record of {unit.RecordLength} bytes.");
            return;
        }

        new ReadOnlySpan<byte>(address, bytes).CopyTo(_record.AsSpan(_recordPosition, bytes));
        _recordPosition += bytes;
    }

    /// <summary>Ends a <c>READ</c>.</summary>
    /// <returns>Zero, or what went wrong.</returns>
    public int EndRead()
    {
        _current = null;
        return _status;
    }

    /// <summary>Ends a <c>WRITE</c>, which is when the record goes to the file. Nothing is written when an item failed.</summary>
    /// <returns>Zero, or what went wrong.</returns>
    public int EndWrite()
    {
        var unit = _current;
        _current = null;
        if (_status != 0)
        {
            return _status;
        }

        try
        {
            unit!.File.Write(_recordStart, _record.AsSpan(0, unit.RecordLength));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            _status = Fail(StatusOf(exception), $"Unit {unit!.Number} could not be written: {exception.Message}");
        }

        return _status;
    }

    private void Begin(int unit, int record)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        _status = 0;
        _recordPosition = 0;
        _current = Find(unit);

        if (_current is null)
        {
            _status = Fail(OptionConflict, $"Unit {unit} is not connected to a file.");
        }
        else if (!_current.IsDirect || _current.IsFormatted)
        {
            _status = Fail(OptionConflict, $"Unit {unit} is not opened for unformatted direct access.");
        }
        else if (record < 1)
        {
            _status = Fail(BadOption, $"Unit {unit} has no record {record}: records are counted from one.");
        }
        else
        {
            _recordStart = (long)(record - 1) * _current.RecordLength;
            if (_record.Length < _current.RecordLength)
            {
                _record = new byte[_current.RecordLength];
            }
        }
    }
}
