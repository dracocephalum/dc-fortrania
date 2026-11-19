using System.Globalization;

namespace Dracocephalum.Fortrania.Translation;

/// <summary>Something a translation has to say about the settings or a source file.</summary>
/// <param name="IsError">Whether it stops the translation; otherwise it is a warning.</param>
/// <param name="File">The file it is about, or <see langword="null"/> when it is about the settings.</param>
/// <param name="Line">The line in that file, or zero when it is about no line.</param>
/// <param name="Text">What is wrong.</param>
public sealed record TranslationMessage(bool IsError, string? File, int Line, string Text)
{
    /// <summary>Gives the message as build tools write theirs, so that an editor can go to the place.</summary>
    /// <returns>The file and line, then <c>error</c> or <c>warning</c>, then the text.</returns>
    public override string ToString()
    {
        var kind = IsError ? "error" : "warning";
        return File is null
            ? $"fortrania: {kind}: {Text}"
            : Line > 0
                ? string.Create(CultureInfo.InvariantCulture, $"{File}({Line}): {kind}: {Text}")
                : $"{File}: {kind}: {Text}";
    }
}
