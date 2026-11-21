// SPDX-License-Identifier: Apache-2.0
// Part of the Fortrania runtime, which code translated from FORTRAN 77 calls.

#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace Dracocephalum.Fortrania.Runtime;

// Every one of these is called on the context by translated code, also the
// ones that need nothing of it today.
#pragma warning disable CA1822

/// <content>
/// The procedures FORTRAN programs take from their compiler's library and
/// no standard of the language defines: ending the program, the date and
/// time, the command line, the environment, and running a command. A
/// translated program reaches them here, and the context's own settings
/// say what each may do.
/// </content>
#if FORTRANIA_RUNTIME_INTERNAL
internal
#else
public
#endif
sealed unsafe partial class FortranContext
{
    private const byte Blank = (byte)' ';

    /// <summary>
    /// Gets or sets the command line the program was started with: its own
    /// name first, then its arguments. Empty unless set, which is how a
    /// library hosted in another program has it.
    /// </summary>
    public IReadOnlyList<string> CommandLine { get; set; } = [];

    /// <summary>Gets or sets where the program's date and time come from; the system's clock unless set.</summary>
    public TimeProvider Clock { get; set; } = TimeProvider.System;

    /// <summary>
    /// Gets or sets a value indicating whether the program may have the
    /// operating system run a command. It may not unless this is set: a
    /// library hosted in another program is not to start programs on its
    /// host's behalf unasked.
    /// </summary>
    public bool AllowsCommands { get; set; }

    /// <summary><c>CALL EXIT</c> with no status: ends the program, here by throwing.</summary>
    /// <exception cref="FortranStopException">Always.</exception>
    public void Exit() => throw new FortranStopException(0);

    /// <summary><c>CALL EXIT ( STATUS )</c>: ends the program with a status, here by throwing.</summary>
    /// <param name="status">The status the program ends with.</param>
    /// <exception cref="FortranStopException">Always.</exception>
    public void Exit(int* status) => throw new FortranStopException(*status);

    /// <summary><c>IARGC ( )</c>: the number of arguments on the command line, the program's own name not counted.</summary>
    /// <returns>The number of arguments.</returns>
    public int ArgumentCount() => Math.Max(CommandLine.Count - 1, 0);

    /// <summary><c>CALL GETARG ( POS, VALUE )</c>: one argument of the command line.</summary>
    /// <param name="position">Which one: zero for the program's own name, one for its first argument.</param>
    /// <param name="value">Is set to the argument, followed by blanks, or to blanks where there is none.</param>
    /// <param name="valueLength">The length of <paramref name="value"/>.</param>
    public void GetArgument(int* position, byte* value, int valueLength) =>
        SetText(value, valueLength, (uint)*position < (uint)CommandLine.Count ? CommandLine[*position] : string.Empty);

    /// <summary><c>CALL GETENV ( NAME, VALUE )</c>: a variable of the environment.</summary>
    /// <param name="name">The variable's name; trailing blanks do not count.</param>
    /// <param name="nameLength">The length of <paramref name="name"/>.</param>
    /// <param name="value">Is set to the variable's value, followed by blanks, or to blanks where it is not set.</param>
    /// <param name="valueLength">The length of <paramref name="value"/>.</param>
    public void GetEnvironment(byte* name, int nameLength, byte* value, int valueLength)
    {
        var variable = Encoding.UTF8.GetString(new ReadOnlySpan<byte>(name, Math.Max(nameLength, 0)).TrimEnd(Blank));
        SetText(value, valueLength, variable.Length == 0 ? string.Empty : Environment.GetEnvironmentVariable(variable) ?? string.Empty);
    }

    /// <summary><c>CALL DATE_AND_TIME ( DATE )</c>.</summary>
    /// <param name="date">Is set to the date as <c>CCYYMMDD</c>.</param>
    /// <param name="dateLength">The length of <paramref name="date"/>.</param>
    public void DateAndTime(byte* date, int dateLength) => DateAndTime(date, dateLength, null, 0, null, 0, null);

    /// <summary><c>CALL DATE_AND_TIME ( DATE, TIME )</c>.</summary>
    /// <param name="date">Is set to the date as <c>CCYYMMDD</c>.</param>
    /// <param name="dateLength">The length of <paramref name="date"/>.</param>
    /// <param name="time">Is set to the time as <c>hhmmss.sss</c>.</param>
    /// <param name="timeLength">The length of <paramref name="time"/>.</param>
    public void DateAndTime(byte* date, int dateLength, byte* time, int timeLength) =>
        DateAndTime(date, dateLength, time, timeLength, null, 0, null);

    /// <summary><c>CALL DATE_AND_TIME ( DATE, TIME, ZONE )</c>.</summary>
    /// <param name="date">Is set to the date as <c>CCYYMMDD</c>.</param>
    /// <param name="dateLength">The length of <paramref name="date"/>.</param>
    /// <param name="time">Is set to the time as <c>hhmmss.sss</c>.</param>
    /// <param name="timeLength">The length of <paramref name="time"/>.</param>
    /// <param name="zone">Is set to the difference from universal time as <c>+hhmm</c>.</param>
    /// <param name="zoneLength">The length of <paramref name="zone"/>.</param>
    public void DateAndTime(byte* date, int dateLength, byte* time, int timeLength, byte* zone, int zoneLength) =>
        DateAndTime(date, dateLength, time, timeLength, zone, zoneLength, null);

    /// <summary><c>CALL DATE_AND_TIME ( DATE, TIME, ZONE, VALUES )</c>: the local date and time, as text and as numbers.</summary>
    /// <param name="date">Is set to the date as <c>CCYYMMDD</c>.</param>
    /// <param name="dateLength">The length of <paramref name="date"/>.</param>
    /// <param name="time">Is set to the time as <c>hhmmss.sss</c>.</param>
    /// <param name="timeLength">The length of <paramref name="time"/>.</param>
    /// <param name="zone">Is set to the difference from universal time as <c>+hhmm</c>.</param>
    /// <param name="zoneLength">The length of <paramref name="zone"/>.</param>
    /// <param name="values">
    /// Eight integers that are set to the year, the month, the day, the
    /// difference from universal time in minutes, the hour, the minute, the
    /// second and the millisecond.
    /// </param>
    public void DateAndTime(byte* date, int dateLength, byte* time, int timeLength, byte* zone, int zoneLength, int* values)
    {
        var now = Clock.GetLocalNow();
        var offset = (int)now.Offset.TotalMinutes;
        var culture = CultureInfo.InvariantCulture;

        SetText(date, dateLength, now.ToString("yyyyMMdd", culture));
        SetText(time, timeLength, now.ToString("HHmmss.fff", culture));
        SetText(zone, zoneLength, string.Create(culture, $"{(offset < 0 ? '-' : '+')}{Math.Abs(offset) / 60:00}{Math.Abs(offset) % 60:00}"));

        if (values != null)
        {
            values[0] = now.Year;
            values[1] = now.Month;
            values[2] = now.Day;
            values[3] = offset;
            values[4] = now.Hour;
            values[5] = now.Minute;
            values[6] = now.Second;
            values[7] = now.Millisecond;
        }
    }

    /// <summary><c>CALL SYSTEM ( COMMAND )</c>: has the operating system run a command, and waits for it.</summary>
    /// <param name="command">The command, as its command interpreter takes it; trailing blanks do not count.</param>
    /// <param name="commandLength">The length of <paramref name="command"/>.</param>
    public void RunCommand(byte* command, int commandLength)
    {
        int status;
        RunCommand(command, commandLength, &status);
    }

    /// <summary><c>CALL SYSTEM ( COMMAND, STATUS )</c>: has the operating system run a command, and waits for it.</summary>
    /// <param name="command">The command, as its command interpreter takes it; trailing blanks do not count.</param>
    /// <param name="commandLength">The length of <paramref name="command"/>.</param>
    /// <param name="status">
    /// Is set to the status the command ended with, or to -1 where it was
    /// not run: because <see cref="AllowsCommands"/> is not set, or because
    /// the platform starts no programs.
    /// </param>
    public void RunCommand(byte* command, int commandLength, int* status)
    {
        *status = -1;
        if (!AllowsCommands)
        {
            return;
        }

        var text = Encoding.UTF8.GetString(new ReadOnlySpan<byte>(command, Math.Max(commandLength, 0)).TrimEnd(Blank));
        var start = OperatingSystem.IsWindows()
            ? new ProcessStartInfo(Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe") { ArgumentList = { "/c", text } }
            : new ProcessStartInfo("/bin/sh") { ArgumentList = { "-c", text } };
        start.UseShellExecute = false;

        try
        {
            using var process = Process.Start(start);
            if (process is not null)
            {
                process.WaitForExit();
                *status = process.ExitCode;
            }
        }
        catch (Exception exception) when (exception is InvalidOperationException or PlatformNotSupportedException or System.ComponentModel.Win32Exception)
        {
            // The command was not run, and the status says so.
        }
    }

    private static void SetText(byte* text, int length, string value)
    {
        if (text == null || length <= 0)
        {
            return;
        }

        var target = new Span<byte>(text, length);
        target.Fill(Blank);
        var bytes = Encoding.UTF8.GetBytes(value);
        bytes.AsSpan(0, Math.Min(bytes.Length, length)).CopyTo(target);
    }
}
