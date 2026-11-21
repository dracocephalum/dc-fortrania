using System.Globalization;

namespace Dracocephalum.Fortrania.Emit;

/// <summary>
/// Thrown when a program unit uses something the emitter cannot translate
/// yet. The unit is valid FORTRAN; the gap is the emitter's.
/// </summary>
public sealed class UnsupportedConstructException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="UnsupportedConstructException"/> class.</summary>
    public UnsupportedConstructException()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="UnsupportedConstructException"/> class.</summary>
    /// <param name="message">The message.</param>
    public UnsupportedConstructException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="UnsupportedConstructException"/> class.</summary>
    /// <param name="message">The message.</param>
    /// <param name="innerException">The exception that led to this one.</param>
    public UnsupportedConstructException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="UnsupportedConstructException"/> class.</summary>
    /// <param name="line">The one-based number of the line the construct is met on.</param>
    /// <param name="construct">What is not translated yet, as a noun phrase: "CHARACTER variables".</param>
    public UnsupportedConstructException(int line, string construct)
        : base(string.Create(CultureInfo.InvariantCulture, $"Line {line}: {construct} cannot be translated yet."))
    {
        Line = line;
        Construct = construct;
    }

    /// <summary>Gets the one-based number of the line the construct is met on.</summary>
    public int Line { get; }

    /// <summary>Gets what is not translated yet, or <see langword="null"/> when the exception was not made with it.</summary>
    public string? Construct { get; }
}
