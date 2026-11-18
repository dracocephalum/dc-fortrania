// SPDX-License-Identifier: Apache-2.0
// Part of the Fortrania runtime, which code translated from FORTRAN 77 calls.

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Dracocephalum.Fortrania.Runtime;

/// <summary>
/// The input and output of one instance of a translated library: its
/// units, and the files connected to them. Every input and output statement
/// of the FORTRAN is a call here, and nothing outside knows what is behind
/// a unit.
/// </summary>
/// <remarks>
/// <para>
/// A statement that fails gives a status that is not zero, which the
/// translated code stores where <c>IOSTAT</c> says or hands to
/// <see cref="Require"/>. The values are the reference compiler's: the
/// operating system's error number where the system refused, 2 for a file
/// that is not there, 13 for one that may not be used so, 17 for one that
/// is there and must not be; and the compiler library's own from 5001 on.
/// </para>
/// <para>
/// Like the rest of an instance it serves one caller at a time.
/// </para>
/// </remarks>
#if FORTRANIA_RUNTIME_INTERNAL
internal
#else
public
#endif
sealed unsafe partial class FortranIO : IDisposable
{
    // The operating system's numbers, which are the same wherever .NET runs
    // for these three.
    private const int NoSuchFile = 2;
    private const int NotPermitted = 13;
    private const int FileExists = 17;
    private const int SystemFailure = 5;

    // The reference compiler library's numbers.
    private const int OptionConflict = 5001;
    private const int BadOption = 5002;
    private const int MissingOption = 5003;
    private const int RecordOverrun = 5015;
    private const int ShortRecord = 5016;

    private const int FastUnits = 128;

    private readonly IFileProvider _provider;

    // The units a program is most likely to use are found without a
    // dictionary; any other number is as good a unit, and only slower.
    private readonly Unit?[] _fast = new Unit?[FastUnits];
    private readonly Dictionary<int, Unit> _others = new();

    // Streams the host has given names. A name here is that stream, and
    // the provider is not asked.
    private readonly Dictionary<string, Stream> _streams = new(StringComparer.Ordinal);

    // Bytes the host has given names, to be read and not written.
    private readonly Dictionary<string, ReadOnlyMemory<byte>> _images = new(StringComparer.Ordinal);

    // The units a program starts with: standard error, input and output.
    private readonly HashSet<int> _standard = new() { 0, 5, 6 };

    private bool _isDisposed;

    /// <summary>Initializes a new instance of the <see cref="FortranIO"/> class, for files on disk.</summary>
    public FortranIO()
        : this(new DiskFileProvider())
    {
    }

    /// <summary>Initializes a new instance of the <see cref="FortranIO"/> class.</summary>
    /// <param name="provider">What decides what a file name stands for.</param>
    public FortranIO(IFileProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        _provider = provider;
    }

    /// <summary>
    /// Gets or sets the number of bytes the unit of <c>RECL</c> stands for
    /// in a file opened for unformatted direct access. The language leaves
    /// it to the compiler: the reference compiler counts bytes, which is
    /// the default, and others count words of four.
    /// </summary>
    public int RecordLengthUnit { get; set; } = 1;

    /// <summary>Gets what went wrong in the last statement that failed, in words; <see langword="null"/> before any has.</summary>
    public string? LastError { get; private set; }

    /// <summary>
    /// Gives a stream a file name. From then on the name is that stream to
    /// every statement: it exists, can be opened, and is read and written
    /// at any record. The stream is left open when its unit is closed.
    /// </summary>
    /// <param name="name">The name, as the program will give it; case and spelling count.</param>
    /// <param name="stream">A stream that can seek and be read. Nothing else may use it while it is registered.</param>
    public void Register(string name, Stream stream)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(stream);
        if (!stream.CanSeek || !stream.CanRead)
        {
            throw new ArgumentException("A stream taken for a file has to seek and be readable.", nameof(stream));
        }

        _images.Remove(name);
        _streams[name] = stream;
    }

    /// <summary>
    /// Gives bytes held in memory a file name. From then on the name is a
    /// file of those bytes that exists, can be opened and read at any
    /// record, and cannot be written. The bytes are not copied, and the
    /// same ones may be registered with any number of instances and read
    /// by all of them at once.
    /// </summary>
    /// <param name="name">The name, as the program will give it; case and spelling count.</param>
    /// <param name="bytes">The file's contents, which must not change while they are registered.</param>
    public void Register(string name, ReadOnlyMemory<byte> bytes)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        _streams.Remove(name);
        _images[name] = bytes;
    }

    /// <summary>Takes a name back from a stream or from bytes. A unit connected to them stays connected.</summary>
    /// <param name="name">The name they were registered under.</param>
    /// <returns>Whether the name was registered.</returns>
    public bool Unregister(string name) => _streams.Remove(name) | _images.Remove(name);

    /// <summary><c>OPEN</c>: connects a unit to a file. A specifier that is left out is passed as empty.</summary>
    /// <param name="unit">The unit number.</param>
    /// <param name="file">The file's name; trailing blanks do not count.</param>
    /// <param name="status"><c>OLD</c>, <c>NEW</c>, <c>SCRATCH</c>, <c>REPLACE</c> or <c>UNKNOWN</c>, the default.</param>
    /// <param name="access"><c>SEQUENTIAL</c>, the default, or <c>DIRECT</c>.</param>
    /// <param name="form"><c>FORMATTED</c> or <c>UNFORMATTED</c>; by default the first for sequential access and the second for direct.</param>
    /// <param name="recl">The length of a record, which direct access has to give.</param>
    /// <returns>Zero, or what went wrong.</returns>
    public int Open(
        int unit,
        ReadOnlySpan<byte> file = default,
        ReadOnlySpan<byte> status = default,
        ReadOnlySpan<byte> access = default,
        ReadOnlySpan<byte> form = default,
        int? recl = null)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        if (unit < 0)
        {
            return Fail(BadOption, $"Unit {unit} cannot be opened: a unit number is not negative.");
        }

        var name = NameOf(file);
        var wanted = Keyword(status);
        var isDirect = Keyword(access) switch
        {
            "" or "SEQUENTIAL" => false,
            "DIRECT" => true,
            _ => (bool?)null,
        };
        var isFormatted = Keyword(form) switch
        {
            "" => isDirect == false,
            "FORMATTED" => true,
            "UNFORMATTED" => false,
            _ => (bool?)null,
        };

        if (isDirect is null || isFormatted is null || wanted is not ("" or "OLD" or "NEW" or "SCRATCH" or "REPLACE" or "UNKNOWN"))
        {
            return Fail(BadOption, $"Unit {unit} cannot be opened: STATUS, ACCESS or FORM has a value the language does not know.");
        }

        if (recl is <= 0)
        {
            return Fail(BadOption, $"Unit {unit} cannot be opened: RECL has to be positive.");
        }

        if (isDirect.Value && recl is null)
        {
            return Fail(MissingOption, $"Unit {unit} cannot be opened: direct access needs RECL.");
        }

        if (wanted == "SCRATCH" && name.Length > 0)
        {
            return Fail(BadOption, $"Unit {unit} cannot be opened: a scratch file has no name.");
        }

        var length = isDirect.Value ? (isFormatted.Value ? recl!.Value : checked(recl!.Value * RecordLengthUnit)) : 0;

        if (Find(unit) is { } connected)
        {
            // A unit that is connected stays so when the statement names
            // no file or the same one, and then nothing about the
            // connection may change. Any other file takes the unit over.
            if (name.Length == 0 || IdentityOf(name) == connected.Identity)
            {
                var changes = wanted is "SCRATCH" or "NEW" or "REPLACE"
                    || (!access.IsEmpty && isDirect.Value != connected.IsDirect)
                    || (!form.IsEmpty && isFormatted.Value != connected.IsFormatted)
                    || (isDirect.Value && recl is not null && length != connected.RecordLength);
                return changes
                    ? Fail(BadOption, $"Unit {unit} is connected already, and what OPEN asks would change the connection.")
                    : 0;
            }

            Disconnect(connected, delete: connected.IsScratch);
        }

        _standard.Remove(unit);

        if (wanted != "SCRATCH" && name.Length == 0)
        {
            // The reference compiler's name for a file that is given none.
            name = "fort." + unit.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        FortranFile opened;
        try
        {
            if (wanted == "SCRATCH")
            {
                opened = _provider.OpenScratch();
            }
            else if (_images.TryGetValue(name, out var image))
            {
                if (wanted == "NEW")
                {
                    return Fail(FileExists, $"The file {name} exists, and was to be opened as new.");
                }

                if (wanted == "REPLACE")
                {
                    return Fail(NotPermitted, $"The file {name} is held in memory for reading and cannot be replaced.");
                }

                opened = new MemoryFile(image);
            }
            else if (_streams.TryGetValue(name, out var stream))
            {
                if (wanted == "NEW")
                {
                    return Fail(FileExists, $"The file {name} exists, and was to be opened as new.");
                }

                if (wanted == "REPLACE")
                {
                    stream.SetLength(0);
                }

                opened = new StreamFile(stream, leaveOpen: true);
            }
            else
            {
                var exists = _provider.Exists(name);
                if (wanted == "OLD" && !exists)
                {
                    return Fail(NoSuchFile, $"The file {name} does not exist.");
                }

                if (wanted == "NEW" && exists)
                {
                    return Fail(FileExists, $"The file {name} exists, and was to be opened as new.");
                }

                opened = _provider.Open(name, wanted switch
                {
                    "OLD" => FileMode.Open,
                    "NEW" => FileMode.CreateNew,
                    "REPLACE" => FileMode.Create,
                    _ => FileMode.OpenOrCreate,
                });
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException)
        {
            return Fail(StatusOf(exception), $"The file {name} could not be opened: {exception.Message}");
        }

        var isScratch = wanted == "SCRATCH";
        Connect(new Unit(unit, opened, name, isScratch ? string.Empty : IdentityOf(name), isDirect.Value, isFormatted.Value, isScratch, length));
        return 0;
    }

    /// <summary><c>CLOSE</c>: ends a unit's connection. A unit that is not connected is closed already.</summary>
    /// <param name="unit">The unit number.</param>
    /// <param name="status"><c>KEEP</c> or <c>DELETE</c>; by default a scratch file is deleted and any other kept.</param>
    /// <returns>Zero, or what went wrong.</returns>
    public int Close(int unit, ReadOnlySpan<byte> status = default)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        var wanted = Keyword(status);
        if (wanted is not ("" or "KEEP" or "DELETE"))
        {
            return Fail(BadOption, $"Unit {unit} cannot be closed: STATUS has a value the language does not know.");
        }

        _standard.Remove(unit);
        if (Find(unit) is not { } connected)
        {
            return 0;
        }

        if (connected.IsScratch && wanted == "KEEP")
        {
            return Fail(BadOption, $"Unit {unit} is a scratch file, which cannot be kept.");
        }

        try
        {
            Disconnect(connected, delete: connected.IsScratch || wanted == "DELETE");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return Fail(StatusOf(exception), $"Unit {unit} could not be closed: {exception.Message}");
        }

        return 0;
    }

    /// <summary><c>INQUIRE</c> by file. An answer that is not asked for is passed as <see langword="null"/>.</summary>
    /// <param name="file">The file's name; trailing blanks do not count.</param>
    /// <param name="exist">Is set to whether the file exists, as a logical value.</param>
    /// <param name="opened">Is set to whether the file is connected to a unit, as a logical value.</param>
    /// <param name="number">Is set to the unit the file is connected to, or to -1.</param>
    /// <param name="name">Is set to the file's name, followed by blanks.</param>
    /// <param name="nameLength">The length of <paramref name="name"/>.</param>
    /// <param name="named">Is set to true, as a logical value: a file inquired about by name has one.</param>
    /// <returns>Zero, or what went wrong.</returns>
    public int InquireFile(
        ReadOnlySpan<byte> file,
        int* exist = null,
        int* opened = null,
        int* number = null,
        byte* name = null,
        int nameLength = 0,
        int* named = null)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        var asked = NameOf(file);
        Unit? connected = null;
        var exists = false;

        try
        {
            if (asked.Length > 0)
            {
                var identity = IdentityOf(asked);
                connected = FindByIdentity(identity);
                exists = connected is not null || IsRegistered(asked) || _provider.Exists(asked);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException)
        {
            return Fail(StatusOf(exception), $"The file {asked} could not be inquired about: {exception.Message}");
        }

        Set(exist, exists);
        Set(opened, connected is not null);
        Set(number, connected?.Number ?? -1);
        Set(named, true);
        SetText(name, nameLength, asked);
        return 0;
    }

    /// <summary><c>INQUIRE</c> by unit. An answer that is not asked for is passed as <see langword="null"/>.</summary>
    /// <param name="unit">The unit number.</param>
    /// <param name="exist">Is set to whether there is such a unit, as a logical value: any number that is not negative is one.</param>
    /// <param name="opened">Is set to whether the unit is connected, as a logical value.</param>
    /// <param name="number">Is set to the unit's number when it is connected, and to -1 otherwise.</param>
    /// <param name="name">Is set to the name of the file the unit is connected to, followed by blanks; left alone when there is none.</param>
    /// <param name="nameLength">The length of <paramref name="name"/>.</param>
    /// <param name="named">Is set to whether that file has a name, as a logical value.</param>
    /// <returns>Zero, or what went wrong.</returns>
    public int InquireUnit(
        int unit,
        int* exist = null,
        int* opened = null,
        int* number = null,
        byte* name = null,
        int nameLength = 0,
        int* named = null)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        Set(exist, unit >= 0);

        var connected = Find(unit);
        var standard = connected is null && _standard.Contains(unit)
            ? unit switch { 5 => "stdin", 6 => "stdout", _ => "stderr" }
            : null;
        var fileName = connected is { IsScratch: false } ? connected.Name : standard;

        Set(opened, connected is not null || standard is not null);
        Set(number, connected is not null || standard is not null ? unit : -1);
        Set(named, fileName is not null);
        if (fileName is not null)
        {
            SetText(name, nameLength, fileName);
        }

        return 0;
    }

    /// <summary><c>REWIND</c>: moves a unit opened for sequential access back to its start. A unit that is not connected is left so.</summary>
    /// <param name="unit">The unit number.</param>
    /// <returns>Zero, or what went wrong.</returns>
    public int Rewind(int unit)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        if (Find(unit) is not { } connected)
        {
            return 0;
        }

        if (connected.IsDirect)
        {
            return Fail(BadOption, $"Unit {unit} is opened for direct access and cannot be rewound.");
        }

        connected.Position = 0;
        return 0;
    }

    /// <summary>Does what FORTRAN does where a statement fails and has no <c>IOSTAT</c>: ends the program, here by throwing.</summary>
    /// <param name="status">What the statement gave.</param>
    /// <exception cref="FortranIOException"><paramref name="status"/> is not zero.</exception>
    public void Require(int status)
    {
        if (status != 0)
        {
            throw new FortranIOException(status, LastError ?? "An input or output statement failed.");
        }
    }

    /// <summary>Closes every unit. Scratch files are deleted, and what was registered is left as it is.</summary>
    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        foreach (var unit in _fast)
        {
            unit?.File.Dispose();
        }

        foreach (var unit in _others.Values)
        {
            unit.File.Dispose();
        }

        Array.Clear(_fast);
        _others.Clear();
    }

    private static void Set(int* answer, bool value)
    {
        if (answer != null)
        {
            *answer = value ? 1 : 0;
        }
    }

    private static void Set(int* answer, int value)
    {
        if (answer != null)
        {
            *answer = value;
        }
    }

    private static void SetText(byte* text, int length, string value)
    {
        if (text == null || length <= 0)
        {
            return;
        }

        var target = new Span<byte>(text, length);
        target.Fill((byte)' ');
        var bytes = Encoding.UTF8.GetBytes(value);
        bytes.AsSpan(0, Math.Min(bytes.Length, length)).CopyTo(target);
    }

    // A file name as text: the bytes are UTF-8, and trailing blanks are
    // padding.
    private static string NameOf(ReadOnlySpan<byte> file) => Encoding.UTF8.GetString(file.TrimEnd((byte)' '));

    // The value of a specifier such as STATUS, which may be in either case
    // and followed by blanks.
    private static string Keyword(ReadOnlySpan<byte> value) => Encoding.ASCII.GetString(value.TrimEnd((byte)' ')).ToUpperInvariant();

    private static int StatusOf(Exception exception) => exception switch
    {
        FileNotFoundException or DirectoryNotFoundException => NoSuchFile,
        UnauthorizedAccessException => NotPermitted,
        _ => SystemFailure,
    };

    private bool IsRegistered(string name) => _streams.ContainsKey(name) || _images.ContainsKey(name);

    private string IdentityOf(string name) => IsRegistered(name) ? name : _provider.Identify(name);

    private Unit? Find(int unit) => (uint)unit < FastUnits ? _fast[unit] : _others.GetValueOrDefault(unit);

    private Unit? FindByIdentity(string identity)
    {
        foreach (var unit in _fast)
        {
            if (unit is { IsScratch: false } && unit.Identity == identity)
            {
                return unit;
            }
        }

        foreach (var unit in _others.Values)
        {
            if (!unit.IsScratch && unit.Identity == identity)
            {
                return unit;
            }
        }

        return null;
    }

    private void Connect(Unit unit)
    {
        if ((uint)unit.Number < FastUnits)
        {
            _fast[unit.Number] = unit;
        }
        else
        {
            _others[unit.Number] = unit;
        }
    }

    private void Disconnect(Unit unit, bool delete)
    {
        if ((uint)unit.Number < FastUnits)
        {
            _fast[unit.Number] = null;
        }
        else
        {
            _others.Remove(unit.Number);
        }

        unit.File.Dispose();
        if (delete && !unit.IsScratch && !Unregister(unit.Name))
        {
            _provider.Delete(unit.Name);
        }
    }

    private int Fail(int status, string message)
    {
        LastError = message;
        return status;
    }

    // One connection of a unit to a file.
    private sealed class Unit(int number, FortranFile file, string name, string identity, bool isDirect, bool isFormatted, bool isScratch, int recordLength)
    {
        public int Number { get; } = number;

        public FortranFile File { get; } = file;

        public string Name { get; } = name;

        public string Identity { get; } = identity;

        public bool IsDirect { get; } = isDirect;

        public bool IsFormatted { get; } = isFormatted;

        public bool IsScratch { get; } = isScratch;

        // The bytes in a record, for direct access.
        public int RecordLength { get; } = recordLength;

        // Where the next record begins, for sequential access.
        public long Position { get; set; }
    }
}
