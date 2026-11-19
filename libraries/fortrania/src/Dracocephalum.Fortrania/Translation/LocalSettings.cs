namespace Dracocephalum.Fortrania.Translation;

/// <summary>
/// What the local settings file beside a settings file holds: values for
/// its variables that belong to one machine and are not shared.
/// </summary>
public sealed class LocalSettings
{
    /// <summary>Gets or sets the values, by variable name.</summary>
    public IDictionary<string, string> Variables { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
}
