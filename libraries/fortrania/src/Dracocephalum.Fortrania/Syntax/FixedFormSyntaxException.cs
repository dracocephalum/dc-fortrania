using System.Globalization;

namespace Dracocephalum.Fortrania.Syntax;

/// <summary>
/// Thrown when fixed-form source breaks a rule of the source form itself,
/// before any statement is parsed: a continuation line with nothing to
/// continue, or a label field that holds something other than digits.
/// </summary>
public sealed class FixedFormSyntaxException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="FixedFormSyntaxException"/> class.</summary>
    public FixedFormSyntaxException()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="FixedFormSyntaxException"/> class.</summary>
    /// <param name="message">What is wrong with the source.</param>
    public FixedFormSyntaxException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="FixedFormSyntaxException"/> class.</summary>
    /// <param name="message">What is wrong with the source.</param>
    /// <param name="innerException">The exception that caused this one.</param>
    public FixedFormSyntaxException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="FixedFormSyntaxException"/> class.</summary>
    /// <param name="line">The one-based number of the offending physical line.</param>
    /// <param name="message">What is wrong with that line.</param>
    public FixedFormSyntaxException(int line, string message)
        : base(string.Create(CultureInfo.InvariantCulture, $"Line {line}: {message}"))
    {
        Line = line;
    }

    /// <summary>Gets the one-based number of the offending physical line, or zero when it is not known.</summary>
    public int Line { get; }
}
