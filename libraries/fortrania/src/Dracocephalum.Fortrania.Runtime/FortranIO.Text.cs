// SPDX-License-Identifier: Apache-2.0
// Part of the Fortrania runtime, which code translated from FORTRAN 77 calls.

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Dracocephalum.Fortrania.Runtime;

/// <content>
/// Formatted transfer in sequence: text, a record to a line. A statement
/// names its unit and its format, passes its items one by one, and ends;
/// the format is followed as the items come, so that each item meets the
/// part of the format that is its turn. Text is bytes throughout; they are
/// decoded only for the screen and encoded only from the keyboard.
/// </content>
#if FORTRANIA_RUNTIME_INTERNAL
internal
#else
public
#endif
sealed unsafe partial class FortranIO
{
    private const int EndOfFile = -1;
    private const int FormatError = 5006;
    private const int PastEndOfFile = 5008;
    private const int BadValue = 5010;

    private const byte Blank = (byte)' ';

    // The format of the statement in progress, and how far it has been
    // followed: a place in the list of each group entered.
    private readonly List<Frame> _frames = [];
    private List<FormatItem>? _format;
    private byte[] _formatText = [];
    private int _reversion;
    private bool _sawData;
    private int _scale;
    private bool _staysOnLine;

    // What kind of statement is in progress.
    private Transfer _transfer;

    // The screen or keyboard, where the statement's unit is one of the
    // units a program starts with.
    private TextWriter? _screen;
    private TextReader? _keyboard;

    // The record being put together for writing.
    private byte[] _line = new byte[256];
    private int _lineLength;
    private int _column;

    // The record being read: where its bytes lie.
    private byte[] _input = [];
    private int _inputStart;
    private int _inputLength;

    private byte[] _newLineBytes = Encoding.ASCII.GetBytes(Environment.NewLine);
    private string _newLine = Environment.NewLine;

    private enum Transfer
    {
        None,
        Direct,
        TextRead,
        TextWrite,
    }

    /// <summary>
    /// Gets or sets what ends a record written as text. By default it is
    /// the running platform's line ending, which is what the reference
    /// compiler writes there. Whatever it is set to, a record read may end
    /// in a line feed, a carriage return, or the two together.
    /// </summary>
    public string NewLine
    {
        get
        {
            return _newLine;
        }

        set
        {
            ArgumentNullException.ThrowIfNull(value);
            _newLine = value;
            _newLineBytes = Encoding.UTF8.GetBytes(value);
        }
    }

    /// <summary>
    /// Gets or sets where unit 6 writes while no file is opened on it. By
    /// default it is <see langword="null"/> and what is written is dropped:
    /// an instance is one of perhaps many in a program, and the console is
    /// the program's.
    /// </summary>
    public TextWriter? Output { get; set; }

    /// <summary>Gets or sets where unit 0 writes while no file is opened on it; by default <see langword="null"/>, and what is written is dropped.</summary>
    public TextWriter? Error { get; set; }

    /// <summary>Gets or sets where unit 5 reads while no file is opened on it; by default <see langword="null"/>, and a read finds the end of the file.</summary>
    public TextReader? Input { get; set; }

    /// <summary>
    /// Sets units 6, 0 and 5 to the console's output, error output and
    /// input, as a FORTRAN program run on its own has them.
    /// </summary>
    public void UseConsole()
    {
        Output = Console.Out;
        Error = Console.Error;
        Input = Console.In;
    }

    /// <summary>Begins a formatted <c>WRITE</c> or a <c>PRINT</c>.</summary>
    /// <param name="unit">The unit number; 6 where the statement has an asterisk.</param>
    /// <param name="format">The format, parentheses included.</param>
    public void WriteFormatted(int unit, ReadOnlySpan<byte> format)
    {
        if (BeginWrite(unit))
        {
            BeginFormat(format);
        }
    }

    /// <summary>Begins a formatted <c>READ</c>, which reads the first record.</summary>
    /// <param name="unit">The unit number; 5 where the statement has an asterisk.</param>
    /// <param name="format">The format, parentheses included.</param>
    public void ReadFormatted(int unit, ReadOnlySpan<byte> format)
    {
        if (BeginRead(unit))
        {
            BeginFormat(format);
            if (_status == 0)
            {
                NextRecord();
            }
        }
    }

    private bool BeginWrite(int unit)
    {
        BeginText(unit, Transfer.TextWrite);
        if (_status != 0)
        {
            return false;
        }

        if (_current is null)
        {
            if (unit == 5)
            {
                _status = Fail(OptionConflict, "Unit 5 is the keyboard and cannot be written.");
                return false;
            }

            _screen = (unit == 0 ? Error : Output) ?? TextWriter.Null;
        }
        else if (!_current.File.CanWrite)
        {
            _status = Fail(NotPermitted, $"Unit {unit} is connected to a file that cannot be written.");
            return false;
        }

        _lineLength = 0;
        _column = 0;
        _staysOnLine = false;
        return true;
    }

    private bool BeginRead(int unit)
    {
        BeginText(unit, Transfer.TextRead);
        if (_status != 0)
        {
            return false;
        }

        if (_current is null)
        {
            if (unit != 5)
            {
                _status = Fail(OptionConflict, $"Unit {unit} is the screen and cannot be read.");
                return false;
            }

            _keyboard = Input ?? TextReader.Null;
        }

        return true;
    }

    /// <summary>Writes a character value as the next item.</summary>
    /// <param name="text">Its first byte.</param>
    /// <param name="length">Its length.</param>
    public void Write(byte* text, int length)
    {
        if (_isList)
        {
            ListWrite(new ReadOnlySpan<byte>(text, Math.Max(length, 0)));
        }
        else if (NextData() is { } item)
        {
            if (item.Kind != FormatKind.Text)
            {
                Mismatch(item, "a character value");
                return;
            }

            PutText(item, new ReadOnlySpan<byte>(text, Math.Max(length, 0)));
        }
    }

    /// <summary>Writes an integer as the next item.</summary>
    /// <param name="value">The number.</param>
    public void Write(int value)
    {
        if (_isList)
        {
            ListWrite(NumberEditing.Integer(value, 11, -1));
        }
        else if (NextData() is { } item)
        {
            switch (item.Kind)
            {
                case FormatKind.Integer:
                    Put(NumberEditing.Integer(value, item.Width, item.Digits));
                    break;
                case FormatKind.Text:
                    PutText(item, new ReadOnlySpan<byte>(&value, sizeof(int)));
                    break;
                default:
                    Mismatch(item, "an integer");
                    break;
            }
        }
    }

    /// <summary>Writes a real number as the next item.</summary>
    /// <param name="value">The number.</param>
    public void Write(float value)
    {
        if (_isList)
        {
            ListWrite(NumberEditing.General(value, 16, 9, 2));
        }
        else if (NextData() is { } item)
        {
            if (item.Kind == FormatKind.Text)
            {
                PutText(item, new ReadOnlySpan<byte>(&value, sizeof(float)));
            }
            else
            {
                PutReal(item, value);
            }
        }
    }

    /// <summary>Writes a double precision number as the next item.</summary>
    /// <param name="value">The number.</param>
    public void Write(double value)
    {
        if (_isList)
        {
            ListWrite(NumberEditing.General(value, 25, 17, 3));
        }
        else if (NextData() is { } item)
        {
            if (item.Kind == FormatKind.Text)
            {
                PutText(item, new ReadOnlySpan<byte>(&value, sizeof(double)));
            }
            else
            {
                PutReal(item, value);
            }
        }
    }

    /// <summary>Writes a logical value as the next item.</summary>
    /// <param name="value">The value.</param>
    public void Write(bool value)
    {
        if (_isList)
        {
            ListWrite(value ? "T" : "F");
        }
        else if (NextData() is { } item)
        {
            if (item.Kind != FormatKind.Logical)
            {
                Mismatch(item, "a logical value");
                return;
            }

            Put(NumberEditing.Logical(value, item.Width));
        }
    }

    /// <summary>Writes the elements of a character array as the next items.</summary>
    /// <param name="text">The first element's first byte.</param>
    /// <param name="length">The length of one element.</param>
    /// <param name="count">The number of elements.</param>
    public void Write(byte* text, int length, int count)
    {
        for (var index = 0; index < count; index++)
        {
            Write(text + ((long)index * length), length);
        }
    }

    /// <summary>Writes the elements of an integer array as the next items.</summary>
    /// <param name="values">The first element.</param>
    /// <param name="count">The number of elements.</param>
    public void Write(int* values, int count)
    {
        for (var index = 0; index < count; index++)
        {
            Write(values[index]);
        }
    }

    /// <summary>Writes the elements of a real array as the next items.</summary>
    /// <param name="values">The first element.</param>
    /// <param name="count">The number of elements.</param>
    public void Write(float* values, int count)
    {
        for (var index = 0; index < count; index++)
        {
            Write(values[index]);
        }
    }

    /// <summary>Writes the elements of a double precision array as the next items.</summary>
    /// <param name="values">The first element.</param>
    /// <param name="count">The number of elements.</param>
    public void Write(double* values, int count)
    {
        for (var index = 0; index < count; index++)
        {
            Write(values[index]);
        }
    }

    /// <summary>Writes the elements of a logical array as the next items.</summary>
    /// <param name="values">The first element, as it is stored.</param>
    /// <param name="count">The number of elements.</param>
    public void WriteLogicals(int* values, int count)
    {
        for (var index = 0; index < count; index++)
        {
            Write(values[index] != 0);
        }
    }

    /// <summary>Reads a character value as the next item.</summary>
    /// <param name="text">Its first byte.</param>
    /// <param name="length">Its length.</param>
    public void Read(byte* text, int length)
    {
        if (_isList)
        {
            ListRead(text, length);
        }
        else if (NextData() is { } item)
        {
            if (item.Kind != FormatKind.Text)
            {
                Mismatch(item, "a character value");
                return;
            }

            // A field wider than the value gives its last characters, and
            // a narrower one is followed by blanks.
            var target = new Span<byte>(text, Math.Max(length, 0));
            var field = Field(item.Width < 0 ? target.Length : item.Width);
            if (field.Length > target.Length)
            {
                field = field[(field.Length - target.Length)..];
            }

            field.CopyTo(target);
            target[field.Length..].Fill(Blank);
        }
    }

    /// <summary>Reads an integer as the next item.</summary>
    /// <param name="value">Where it goes.</param>
    public void Read(int* value)
    {
        if (_isList)
        {
            ListRead(value);
        }
        else if (NextData() is { } item)
        {
            if (item.Kind != FormatKind.Integer)
            {
                Mismatch(item, "an integer");
            }
            else if (NumberEditing.TryInteger(Field(item.Width), out var number))
            {
                *value = number;
            }
            else
            {
                _status = Fail(BadValue, "A field read as an integer does not hold one.");
            }
        }
    }

    /// <summary>Reads a real number as the next item.</summary>
    /// <param name="value">Where it goes.</param>
    public void Read(float* value)
    {
        double number;
        if (GetReal(&number))
        {
            *value = (float)number;
        }
    }

    /// <summary>Reads a double precision number as the next item.</summary>
    /// <param name="value">Where it goes.</param>
    public void Read(double* value)
    {
        double number;
        if (GetReal(&number))
        {
            *value = number;
        }
    }

    /// <summary>Reads a logical value as the next item.</summary>
    /// <param name="value">Where it goes, as it is stored.</param>
    public void ReadLogical(int* value)
    {
        if (_isList)
        {
            ListReadLogical(value);
        }
        else if (NextData() is { } item)
        {
            if (item.Kind != FormatKind.Logical)
            {
                Mismatch(item, "a logical value");
            }
            else if (NumberEditing.TryLogical(Field(item.Width), out var answer))
            {
                *value = answer ? 1 : 0;
            }
            else
            {
                _status = Fail(BadValue, "A field read as a logical value does not hold one.");
            }
        }
    }

    /// <summary>Reads the elements of a character array as the next items.</summary>
    /// <param name="text">The first element's first byte.</param>
    /// <param name="length">The length of one element.</param>
    /// <param name="count">The number of elements.</param>
    public void Read(byte* text, int length, int count)
    {
        for (var index = 0; index < count; index++)
        {
            Read(text + ((long)index * length), length);
        }
    }

    /// <summary>Reads the elements of an integer array as the next items.</summary>
    /// <param name="values">The first element.</param>
    /// <param name="count">The number of elements.</param>
    public void Read(int* values, int count)
    {
        for (var index = 0; index < count; index++)
        {
            Read(values + index);
        }
    }

    /// <summary>Reads the elements of a real array as the next items.</summary>
    /// <param name="values">The first element.</param>
    /// <param name="count">The number of elements.</param>
    public void Read(float* values, int count)
    {
        for (var index = 0; index < count; index++)
        {
            Read(values + index);
        }
    }

    /// <summary>Reads the elements of a double precision array as the next items.</summary>
    /// <param name="values">The first element.</param>
    /// <param name="count">The number of elements.</param>
    public void Read(double* values, int count)
    {
        for (var index = 0; index < count; index++)
        {
            Read(values + index);
        }
    }

    /// <summary>Reads the elements of a logical array as the next items.</summary>
    /// <param name="values">The first element, as it is stored.</param>
    /// <param name="count">The number of elements.</param>
    public void ReadLogicals(int* values, int count)
    {
        for (var index = 0; index < count; index++)
        {
            ReadLogical(values + index);
        }
    }

    private void BeginText(int unit, Transfer transfer)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        _status = 0;
        _transfer = transfer;
        _isList = false;
        _internal = null;
        _screen = null;
        _keyboard = null;
        _current = Find(unit);

        if (_current is null)
        {
            if (!_standard.Contains(unit))
            {
                _status = Fail(OptionConflict, $"Unit {unit} is not connected to a file.");
            }
        }
        else if (_current.IsDirect || !_current.IsFormatted)
        {
            _status = Fail(OptionConflict, $"Unit {unit} is not opened for formatted sequential access.");
        }
    }

    private void BeginFormat(ReadOnlySpan<byte> format)
    {
        // A statement is mostly run again with the format it had before.
        if (_format is null || !format.SequenceEqual(_formatText))
        {
            _format = FormatParser.Parse(format);
            _formatText = format.ToArray();
            if (_format is not null)
            {
                // When the format is used up and items are left, it starts
                // again at the group its last top-level parenthesis closed,
                // or at its beginning when it has none.
                _reversion = Math.Max(_format.FindLastIndex(item => item.Kind == FormatKind.Group), 0);
            }
        }

        if (_format is null)
        {
            _status = Fail(FormatError, "The format is not one this runtime can follow: " + Encoding.UTF8.GetString(format));
            return;
        }

        _frames.Clear();
        _frames.Add(new Frame(_format, 1));
        _sawData = false;
        _scale = 0;
    }

    // Follows the format to the next item that takes a value, doing on the
    // way what the items before it ask. Gives nothing when the statement
    // has failed.
    private FormatItem? NextData()
    {
        if (_status != 0)
        {
            return null;
        }

        var item = Next(atEnd: false);
        if (item is null && _status == 0)
        {
            _status = Fail(FormatError, "The format has nothing for an item of the list.");
        }

        return item;
    }

    private FormatItem? Next(bool atEnd)
    {
        while (_status == 0)
        {
            var frame = _frames[^1];
            if (frame.Index == frame.Items.Count)
            {
                if (_frames.Count > 1)
                {
                    if (--frame.Rounds > 0)
                    {
                        frame.Index = 0;
                    }
                    else
                    {
                        _frames.RemoveAt(_frames.Count - 1);
                    }

                    continue;
                }

                // The whole format is used up. With items left it starts
                // again on a new record; one that took no item on its way
                // round never would.
                if (atEnd || !_sawData)
                {
                    return null;
                }

                NextRecord();
                _sawData = false;
                frame.Index = _reversion;
                continue;
            }

            var item = frame.Items[frame.Index];
            switch (item.Kind)
            {
                case FormatKind.Group:
                    frame.Index++;
                    _frames.Add(new Frame(item.Items, item.Repeat));
                    break;

                case FormatKind.Colon:
                    if (atEnd)
                    {
                        return null;
                    }

                    frame.Index++;
                    break;

                case FormatKind.Skip:
                    _column += item.Width;
                    frame.Index++;
                    break;

                case FormatKind.Slash:
                    for (var count = 0; count < item.Repeat && _status == 0; count++)
                    {
                        NextRecord();
                    }

                    frame.Index++;
                    break;

                case FormatKind.Dollar:
                    _staysOnLine = true;
                    frame.Index++;
                    break;

                case FormatKind.Scale:
                    _scale = item.Scale;
                    frame.Index++;
                    break;

                case FormatKind.Literal:
                    if (_transfer == Transfer.TextRead)
                    {
                        _status = Fail(FormatError, "A format with a character constant cannot be read by.");
                        return null;
                    }

                    PutBytes(item.Characters);
                    frame.Index++;
                    break;

                default:
                    if (atEnd)
                    {
                        return null;
                    }

                    if (frame.Used == 0)
                    {
                        frame.Used = item.Repeat;
                    }

                    if (--frame.Used == 0)
                    {
                        frame.Index++;
                    }

                    _sawData = true;
                    return item;
            }
        }

        return null;
    }

    // Ends the record in hand and begins another: writes the line, or
    // reads the next one.
    private void NextRecord()
    {
        if (_transfer == Transfer.TextWrite)
        {
            WriteLine(endsLine: true);
            return;
        }

        _column = 0;
        if (IsInternal)
        {
            NextInternalRecord();
            return;
        }

        if (_keyboard is not null)
        {
            var line = _keyboard.ReadLine();
            if (line is null)
            {
                _status = Fail(EndOfFile, "The keyboard's input has ended.");
                return;
            }

            _input = Encoding.UTF8.GetBytes(line);
            _inputStart = 0;
            _inputLength = _input.Length;
            return;
        }

        var unit = _current!;
        if (unit.IsAtEnd)
        {
            _status = Fail(PastEndOfFile, $"Unit {unit.Number} was read again after its end was reached.");
            return;
        }

        try
        {
            // What a program has just written is the end of its file.
            if (unit.HasWritten || !ReadLine(unit))
            {
                unit.IsAtEnd = true;
                _status = Fail(EndOfFile, $"Unit {unit.Number} has no more records.");
            }
        }
        catch (IOException exception)
        {
            _status = Fail(SystemFailure, $"Unit {unit.Number} could not be read: {exception.Message}");
        }
    }

    private int EndText()
    {
        // What the format still holds before its next item that takes a
        // value is done all the same: text to write, records to end.
        // A list-directed statement has no format, and what it wrote is
        // its one record.
        var transfer = _transfer;
        if (!_isList)
        {
            Next(atEnd: true);
        }

        if (_status == 0 && transfer == Transfer.TextWrite)
        {
            WriteLine(endsLine: _isList || !_staysOnLine);
        }

        _transfer = Transfer.None;
        _current = null;
        _internal = null;
        return _status;
    }

    private void WriteLine(bool endsLine)
    {
        if (IsInternal)
        {
            WriteInternalRecord();
            return;
        }

        try
        {
            if (_screen is not null)
            {
                _screen.Write(Encoding.UTF8.GetString(_line, 0, _lineLength));
                if (endsLine)
                {
                    _screen.Write(_newLine);
                }
            }
            else
            {
                var unit = _current!;
                if (endsLine)
                {
                    Reserve(_lineLength + _newLineBytes.Length);
                    _newLineBytes.CopyTo(_line, _lineLength);
                    _lineLength += _newLineBytes.Length;
                }

                unit.File.Write(unit.Position, _line.AsSpan(0, _lineLength));
                unit.Position += _lineLength;
                unit.HasWritten = true;
                unit.IsAtEnd = false;
                unit.BufferCount = 0;
                unit.BufferStart = -1;
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException or ObjectDisposedException)
        {
            _status = Fail(StatusOf(exception), $"A record could not be written: {exception.Message}");
        }

        _lineLength = 0;
        _column = 0;
    }

    // Makes what was written last the end of the file, as writing in
    // sequence does.
    private static void Settle(Unit unit)
    {
        if (unit.HasWritten)
        {
            unit.HasWritten = false;
            unit.File.Truncate(unit.Position);
        }
    }

    // Finds the next line of a file, whichever of the three endings it
    // has, and leaves the unit after it. False at the end of the file.
    private bool ReadLine(Unit unit)
    {
        unit.Buffer ??= new byte[4096];
        var offset = unit.Position - unit.BufferStart;
        if (unit.BufferStart < 0 || offset < 0 || offset > unit.BufferCount)
        {
            unit.BufferStart = unit.Position;
            unit.BufferCount = unit.File.Read(unit.Position, unit.Buffer);
            unit.BufferEndsFile = unit.BufferCount < unit.Buffer.Length;
            offset = 0;
        }

        var start = (int)offset;
        var scanned = 0;
        while (true)
        {
            var window = unit.Buffer.AsSpan(start, unit.BufferCount - start);
            var end = window[scanned..].IndexOfAny((byte)'\r', (byte)'\n');
            var needsMore = end < 0;
            if (!needsMore)
            {
                end += scanned;
                var ending = 1;
                if (window[end] == '\r')
                {
                    // A line feed after it belongs to the same ending, and
                    // may not have been read yet.
                    if (end + 1 < window.Length)
                    {
                        ending = window[end + 1] == '\n' ? 2 : 1;
                    }
                    else
                    {
                        needsMore = !unit.BufferEndsFile;
                    }
                }

                if (!needsMore)
                {
                    SetInput(unit.Buffer, start, end);
                    unit.Position += end + ending;
                    return true;
                }

                scanned = end;
            }
            else
            {
                scanned = window.Length;
            }

            if (unit.BufferEndsFile)
            {
                // The last line may have no ending at all.
                if (window.Length == 0)
                {
                    return false;
                }

                SetInput(unit.Buffer, start, window.Length);
                unit.Position += window.Length;
                return true;
            }

            // The line runs past what has been read: it is moved to the
            // front, into a larger buffer if it fills this one, and more
            // of the file is read behind it.
            var kept = unit.BufferCount - start;
            var buffer = kept == unit.Buffer.Length ? new byte[unit.Buffer.Length * 2] : unit.Buffer;
            unit.Buffer.AsSpan(start, kept).CopyTo(buffer);
            unit.Buffer = buffer;
            unit.BufferStart += start;
            start = 0;

            var read = unit.File.Read(unit.BufferStart + kept, buffer.AsSpan(kept));
            unit.BufferEndsFile = read < buffer.Length - kept;
            unit.BufferCount = kept + read;
        }
    }

    private void SetInput(byte[] bytes, int start, int length)
    {
        _input = bytes;
        _inputStart = start;
        _inputLength = length;
        _column = 0;
    }

    // The next field of the record being read. Where the record ends
    // first, the field is what there is of it.
    private ReadOnlySpan<byte> Field(int width)
    {
        var available = Math.Clamp(_inputLength - _column, 0, width);
        var field = _input.AsSpan(_inputStart + Math.Min(_column, _inputLength), available);
        _column += width;
        return field;
    }

    private bool GetReal(double* value)
    {
        if (_isList)
        {
            return ListRead(value);
        }

        if (NextData() is not { } item)
        {
            return false;
        }

        if (item.Kind is not (FormatKind.Fixed or FormatKind.Exponent))
        {
            Mismatch(item, "a real number");
            return false;
        }

        if (!NumberEditing.TryReal(Field(item.Width), item.Digits, _scale, out *value))
        {
            _status = Fail(BadValue, "A field read as a number does not hold one.");
            return false;
        }

        return true;
    }

    private void PutReal(FormatItem item, double value)
    {
        switch (item.Kind)
        {
            case FormatKind.Fixed:
                if (_scale != 0)
                {
                    _status = Fail(FormatError, "A scale factor with F editing is not supported yet.");
                    break;
                }

                Put(NumberEditing.Fixed(value, item.Width, item.Digits));
                break;
            case FormatKind.Exponent:
                Put(NumberEditing.Exponent(value, item.Width, item.Digits, item.ExponentDigits, (char)item.Letter, _scale));
                break;
            default:
                Mismatch(item, "a real number");
                break;
        }
    }

    // A wider field has the value at its right end, and a narrower one
    // takes the value's first characters.
    private void PutText(FormatItem item, ReadOnlySpan<byte> text)
    {
        var width = item.Width < 0 ? text.Length : item.Width;
        if (width > text.Length)
        {
            _column += width - text.Length;
            PutBytes(text);
        }
        else
        {
            PutBytes(text[..width]);
        }
    }

    private void Put(string field)
    {
        Span<byte> bytes = stackalloc byte[field.Length <= 256 ? field.Length : 0];
        if (bytes.Length == field.Length)
        {
            Encoding.ASCII.GetBytes(field, bytes);
            PutBytes(bytes);
        }
        else
        {
            PutBytes(Encoding.ASCII.GetBytes(field));
        }
    }

    // Writes at the column the record has come to. Columns passed over
    // before it are blanks; passed over and never written after, they are
    // not part of the record.
    private void PutBytes(ReadOnlySpan<byte> bytes)
    {
        Reserve(_column + bytes.Length);
        if (_column > _lineLength)
        {
            _line.AsSpan(_lineLength, _column - _lineLength).Fill(Blank);
        }

        bytes.CopyTo(_line.AsSpan(_column));
        _column += bytes.Length;
        _lineLength = Math.Max(_lineLength, _column);
    }

    private void Reserve(int length)
    {
        if (length > _line.Length)
        {
            Array.Resize(ref _line, Math.Max(length, _line.Length * 2));
        }
    }

    private void Mismatch(FormatItem item, string given) =>
        _status = Fail(FormatError, $"The format has {item.Kind} editing where the list has {given}.");

    // A place in a list of format items: the item whose turn it is, how
    // often that item is still to be used, and how often the list is
    // still to be gone through.
    private sealed class Frame(List<FormatItem> items, int rounds)
    {
        public List<FormatItem> Items { get; } = items;

        public int Index { get; set; }

        public int Used { get; set; }

        public int Rounds { get; set; } = rounds;
    }
}
