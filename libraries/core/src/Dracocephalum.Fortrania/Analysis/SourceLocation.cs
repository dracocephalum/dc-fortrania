namespace Dracocephalum.Fortrania.Analysis;

/// <summary>Where something being analysed stands, for error messages.</summary>
/// <param name="Line">The one-based number of the line its statement starts on.</param>
/// <param name="IncludeFile">The include file that line is in, or <see langword="null"/> when it is in the file being analysed.</param>
internal readonly record struct SourceLocation(int Line, string? IncludeFile)
{
    public FortranAnalysisException Error(string message) => new(Line, IncludeFile, message);
}
