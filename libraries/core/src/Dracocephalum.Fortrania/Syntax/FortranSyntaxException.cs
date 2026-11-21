using System.Globalization;

namespace Dracocephalum.Fortrania.Syntax;

/// <summary>
/// Thrown when a statement cannot be read as FORTRAN 77: a character the
/// language does not have, a constant that never ends, a construct in the
/// wrong place. It also reports a construct the translator does not support
/// yet, so that nothing is ever translated wrongly in silence.
/// </summary>
public sealed class FortranSyntaxException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="FortranSyntaxException"/> class.</summary>
    public FortranSyntaxException()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="FortranSyntaxException"/> class.</summary>
    /// <param name="message">What is wrong with the source.</param>
    public FortranSyntaxException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="FortranSyntaxException"/> class.</summary>
    /// <param name="message">What is wrong with the source.</param>
    /// <param name="innerException">The exception that caused this one.</param>
    public FortranSyntaxException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="FortranSyntaxException"/> class.</summary>
    /// <param name="line">The one-based number of the line the statement starts on.</param>
    /// <param name="message">What is wrong with that statement.</param>
    public FortranSyntaxException(int line, string message)
        : base(string.Create(CultureInfo.InvariantCulture, $"Line {line}: {message}"))
    {
        Line = line;
    }

    /// <summary>Gets the one-based number of the line the statement starts on, or zero when it is not known.</summary>
    public int Line { get; }
}
