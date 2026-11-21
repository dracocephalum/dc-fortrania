// SPDX-License-Identifier: Apache-2.0
// Part of the Fortrania runtime, which code translated from FORTRAN 77 calls.

#nullable enable

using System;
using System.Collections.Generic;

namespace Dracocephalum.Fortrania.Runtime;

/// <summary>
/// What a context is made with: everything a host decides for a program.
/// Left as it is made, it gives a program nothing but files on disk, which
/// is how a library hosted in another program is to start.
/// </summary>
#if FORTRANIA_RUNTIME_INTERNAL
internal
#else
public
#endif
sealed class FortranContextOptions
{
    /// <summary>
    /// Gets what a FORTRAN program run on its own has: the console for its
    /// screen and keyboard, the process's command line for its own, and
    /// leave to run commands.
    /// </summary>
    public static FortranContextOptions Standalone => new()
    {
        UsesConsole = true,
        CommandLine = Environment.GetCommandLineArgs(),
        AllowsCommands = true,
    };

    /// <summary>Gets or sets what decides what a file name stands for; paths on disk when left <see langword="null"/>.</summary>
    public IFileProvider? FileProvider { get; set; }

    /// <summary>Gets or sets a value indicating whether units 6, 0 and 5 are the console's output, error output and input.</summary>
    public bool UsesConsole { get; set; }

    /// <summary>Gets or sets the program's command line: its own name first, then its arguments.</summary>
    public IReadOnlyList<string> CommandLine { get; set; } = [];

    /// <summary>Gets or sets a value indicating whether the program may have the operating system run a command.</summary>
    public bool AllowsCommands { get; set; }

    /// <summary>Gets or sets where the program's date and time come from; the system's clock when left <see langword="null"/>.</summary>
    public TimeProvider? Clock { get; set; }

    /// <summary>Gets or sets the number of bytes the unit of <c>RECL</c> stands for in unformatted direct access; one unless set.</summary>
    public int RecordLengthUnit { get; set; } = 1;

    /// <summary>Gets or sets what ends a record written as text; the running platform's line ending when left <see langword="null"/>.</summary>
    public string? NewLine { get; set; }
}
