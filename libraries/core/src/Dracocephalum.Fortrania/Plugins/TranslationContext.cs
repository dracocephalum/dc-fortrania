using Dracocephalum.Fortrania.Translation;

namespace Dracocephalum.Fortrania.Plugins;

/// <summary>
/// One run of a translation as its plugins see it: the same object at every
/// point of the run, holding more as the run goes, and the way a plugin says
/// what is wrong.
/// </summary>
/// <remarks>
/// What a property holds before the point that fills it is empty, or
/// <see langword="null"/>, as each says. A message reported here is one of
/// the run's, in <see cref="TranslationResult.Messages"/>, and an error stops
/// the run after the plugins of that point.
/// </remarks>
public sealed class TranslationContext
{
    private readonly List<TranslationMessage> _messages;

    internal TranslationContext(IReadOnlyList<TranslationProject> projects, List<TranslationMessage> messages)
    {
        Projects = projects;
        _messages = messages;
    }

    /// <summary>Gets the projects the run translates, as they were given to it.</summary>
    public IReadOnlyList<TranslationProject> Projects { get; }

    /// <summary>
    /// Gets every C# file of the translation, of every project, in the order
    /// they are written; empty until <see cref="ITranslationPlugin.BeforeWrite"/>.
    /// Files written around a translation, a summary, a project file or a
    /// program, are not among them.
    /// </summary>
    public IReadOnlyList<GeneratedFile> GeneratedFiles { get; internal set; } = [];

    /// <summary>Gets what the run did; <see langword="null"/> until <see cref="ITranslationPlugin.AfterWrite"/>.</summary>
    public TranslationResult? Result { get; internal set; }

    /// <summary>Reports an error, which stops the run once the plugins of this point have been called.</summary>
    /// <param name="text">What is wrong.</param>
    /// <param name="file">The file it is about, by full path; <see langword="null"/> when it is about none.</param>
    /// <param name="line">The line in that file, from one; zero when it is about no line.</param>
    public void Error(string text, string? file = null, int line = 0) => Report(isError: true, text, file, line);

    /// <summary>Reports a warning, which lets the run go on.</summary>
    /// <param name="text">What is to be said.</param>
    /// <param name="file">The file it is about, by full path; <see langword="null"/> when it is about none.</param>
    /// <param name="line">The line in that file, from one; zero when it is about no line.</param>
    public void Warning(string text, string? file = null, int line = 0) => Report(isError: false, text, file, line);

    private void Report(bool isError, string text, string? file, int line)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        ArgumentOutOfRangeException.ThrowIfNegative(line);
        _messages.Add(new TranslationMessage(isError, file, line, text));
    }
}
