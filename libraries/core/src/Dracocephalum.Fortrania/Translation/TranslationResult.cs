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
}
