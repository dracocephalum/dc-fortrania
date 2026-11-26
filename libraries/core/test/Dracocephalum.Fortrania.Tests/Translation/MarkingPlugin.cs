using Dracocephalum.Fortrania.Plugins;

namespace Dracocephalum.Fortrania.Tests.Translation;

/// <summary>
/// The plugin of this assembly, which the tests name by the assembly's path
/// as settings name one: it marks the end of every file before they are
/// written.
/// </summary>
public sealed class MarkingPlugin : ITranslationPlugin
{
    /// <summary>The line it ends each file with.</summary>
    public const string Mark = "// Marked by a plugin the settings name.";

    /// <inheritdoc/>
    public void BeforeWrite(TranslationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        foreach (var file in context.GeneratedFiles)
        {
            file.Text += Mark + "\n";
        }
    }
}
