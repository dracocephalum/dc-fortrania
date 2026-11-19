namespace Dracocephalum.Fortrania.Translation;

/// <summary>
/// A command a translation runs: a program and its arguments, given as a
/// list so that nothing needs quoting and no shell is involved.
/// </summary>
public sealed class CommandSettings
{
    /// <summary>Gets or sets the program, then each of its arguments.</summary>
    public IList<string> Run { get; set; } = [];
}
