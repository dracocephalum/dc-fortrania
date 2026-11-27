namespace Dracocephalum.Fortrania.Translation;

/// <summary>What a translation did.</summary>
/// <param name="Messages">What it had to say, errors and warnings, in the order met.</param>
/// <param name="Written">The files it wrote or changed, by full path. A file whose text was already what it should be is not among them.</param>
/// <param name="Removed">The files it removed because the sources no longer give rise to them, by full path.</param>
/// <param name="Units">The number of program units translated.</param>
public sealed record TranslationResult(
    IReadOnlyList<TranslationMessage> Messages,
    IReadOnlyList<string> Written,
    IReadOnlyList<string> Removed,
    int Units)
{
    /// <summary>Gets a value indicating whether the translation was done: with an error nothing is written at all.</summary>
    public bool Succeeded => !Messages.Any(message => message.IsError);

    /// <summary>
    /// Gets the most each procedure translated can take from the stack, by
    /// project, source and procedure in ordinal order; empty when the
    /// translation was not done.
    /// </summary>
    public IReadOnlyList<StackUse> StackUse { get; init; } = [];

    /// <summary>
    /// Gets what each project translated needs for its procedures' working
    /// buffers, each at its <see cref="Translation.StackUse.Place"/>, by
    /// project in ordinal order; empty when the translation was not done.
    /// </summary>
    public IReadOnlyList<WorkingRegion> WorkingRegions { get; init; } = [];

    /// <summary>
    /// Gets the variables translated that lie off their type's alignment,
    /// by project, source and unit in ordinal order; empty when there are
    /// none or the translation was not done.
    /// </summary>
    public IReadOnlyList<UnalignedPlace> UnalignedPlaces { get; init; } = [];

    /// <summary>
    /// Gets the calls translated that pass a 4-byte number where an 8-byte
    /// one is taken, by project, source and unit in ordinal order; empty
    /// when there are none or the translation was not done.
    /// </summary>
    public IReadOnlyList<WidenedPlace> WidenedPlaces { get; init; } = [];

    /// <summary>Gets the folders of sources downloaded by the run, in full.</summary>
    public IReadOnlyList<string> Fetched { get; init; } = [];

    /// <summary>
    /// Gets every file the run read, in full and in the order first read:
    /// the sources and included files of the projects translated, the
    /// summaries of those read instead, and the assemblies that brought a
    /// summary. Empty when the translation was not done. With
    /// <see cref="Patterns"/> and <see cref="Files"/>, what a build needs to
    /// tell whether a translation would come out the same without running it.
    /// </summary>
    public IReadOnlyList<string> Read { get; init; } = [];

    /// <summary>
    /// Gets the file patterns the sources of the projects translated name,
    /// in full, each as the settings give it with its variables put in: a
    /// file that comes to match one, or that matched and is gone, is a
    /// change no file of <see cref="Read"/> shows.
    /// </summary>
    public IReadOnlyList<string> Patterns { get; init; } = [];

    /// <summary>
    /// Gets the files a pattern of <see cref="Patterns"/> matched that the
    /// settings ignore, in full: such a file matching a pattern is no change.
    /// </summary>
    public IReadOnlyList<string> Ignored { get; init; } = [];

    /// <summary>
    /// Gets every file the translation of the projects translated is made
    /// of, in full, written this time or kept from the last: each project's
    /// translated sources, its summary, and the program a scaffold gives it
    /// where this run scaffolds one.
    /// </summary>
    public IReadOnlyList<string> Files { get; init; } = [];
}
