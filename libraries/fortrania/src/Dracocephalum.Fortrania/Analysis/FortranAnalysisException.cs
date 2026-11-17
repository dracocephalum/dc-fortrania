using System.Globalization;

namespace Dracocephalum.Fortrania.Analysis;

/// <summary>
/// Thrown when statements that each parse do not make sense together: a name
/// declared in two ways that exclude each other, a declaration in the wrong
/// place, an include file that cannot be found. It also reports a construct
/// the translator does not support yet, so that nothing is ever translated
/// wrongly in silence.
/// </summary>
public sealed class FortranAnalysisException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="FortranAnalysisException"/> class.</summary>
    public FortranAnalysisException()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="FortranAnalysisException"/> class.</summary>
    /// <param name="message">What is wrong with the source.</param>
    public FortranAnalysisException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="FortranAnalysisException"/> class.</summary>
    /// <param name="message">What is wrong with the source.</param>
    /// <param name="innerException">The exception that caused this one.</param>
    public FortranAnalysisException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="FortranAnalysisException"/> class.</summary>
    /// <param name="line">The one-based number of the line the statement starts on.</param>
    /// <param name="includeFile">The include file the line is in, as named in the <c>INCLUDE</c> statement, or <see langword="null"/> when it is in the file being analysed.</param>
    /// <param name="message">What is wrong with that statement.</param>
    public FortranAnalysisException(int line, string? includeFile, string message)
        : base(includeFile is null
            ? string.Create(CultureInfo.InvariantCulture, $"Line {line}: {message}")
            : string.Create(CultureInfo.InvariantCulture, $"Line {line} of include file '{includeFile}': {message}"))
    {
        Line = line;
        IncludeFile = includeFile;
    }

    /// <summary>Gets the one-based number of the line the statement starts on, or zero when it is not known.</summary>
    public int Line { get; }

    /// <summary>
    /// Gets the include file <see cref="Line"/> counts in, as named in the
    /// <c>INCLUDE</c> statement, or <see langword="null"/> when it counts in
    /// the file being analysed.
    /// </summary>
    public string? IncludeFile { get; }
}
