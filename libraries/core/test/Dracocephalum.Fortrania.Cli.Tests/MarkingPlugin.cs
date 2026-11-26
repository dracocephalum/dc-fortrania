using Dracocephalum.Fortrania.Plugins;

namespace Dracocephalum.Fortrania.Cli.Tests;

/// <summary>
/// The plugin of this assembly, which the command's tests give it by the
/// assembly's path: it marks the end of every file it is given before they
/// are written.
/// </summary>
public sealed class MarkingPlugin : ITranslationPlugin
{
    /// <summary>The line it ends each file with.</summary>
    public const string Mark = "// Marked by a plugin.";

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
