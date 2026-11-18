// SPDX-License-Identifier: Apache-2.0
// Part of the Fortrania runtime, which code translated from FORTRAN 77 calls.

#nullable enable

using System;

namespace Dracocephalum.Fortrania.Runtime;

/// <summary>
/// Thrown where the FORTRAN program ends itself with <c>STOP</c>, so that a
/// program hosting the translated code is not ended with it.
/// </summary>
#if FORTRANIA_RUNTIME_INTERNAL
internal
#else
public
#endif
sealed class FortranStopException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="FortranStopException"/> class.</summary>
    public FortranStopException()
        : this(0)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="FortranStopException"/> class.</summary>
    /// <param name="message">The message.</param>
    public FortranStopException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="FortranStopException"/> class.</summary>
    /// <param name="message">The message.</param>
    /// <param name="innerException">The exception that led to this one.</param>
    public FortranStopException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="FortranStopException"/> class.</summary>
    /// <param name="exitCode">The code the program ends with.</param>
    public FortranStopException(int exitCode)
        : base("The FORTRAN program stopped.") => ExitCode = exitCode;

    /// <summary>Gets the code the program ends with; zero when the statement gave none.</summary>
    public int ExitCode { get; }
}
