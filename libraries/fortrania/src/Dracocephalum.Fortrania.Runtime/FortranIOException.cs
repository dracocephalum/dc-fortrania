// SPDX-License-Identifier: Apache-2.0
// Part of the Fortrania runtime, which code translated from FORTRAN 77 calls.

#nullable enable

using System;

namespace Dracocephalum.Fortrania.Runtime;

/// <summary>
/// Thrown where an input or output statement fails and has no
/// <c>IOSTAT</c> to say so to the program, which FORTRAN ends at that
/// point. A program hosting the translated code is not ended with it.
/// </summary>
#if FORTRANIA_RUNTIME_INTERNAL
internal
#else
public
#endif
sealed class FortranIOException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="FortranIOException"/> class.</summary>
    public FortranIOException()
        : this(0, "An input or output statement failed.")
    {
    }

    /// <summary>Initializes a new instance of the <see cref="FortranIOException"/> class.</summary>
    /// <param name="message">The message.</param>
    public FortranIOException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="FortranIOException"/> class.</summary>
    /// <param name="message">The message.</param>
    /// <param name="innerException">The exception that led to this one.</param>
    public FortranIOException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="FortranIOException"/> class.</summary>
    /// <param name="status">The value <c>IOSTAT</c> would have been given.</param>
    /// <param name="message">The message.</param>
    public FortranIOException(int status, string message)
        : base(message) => Status = status;

    /// <summary>Gets the value <c>IOSTAT</c> would have been given.</summary>
    public int Status { get; }
}
