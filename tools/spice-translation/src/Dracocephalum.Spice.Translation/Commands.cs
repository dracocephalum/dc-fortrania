using System.Reflection;
using Dracocephalum.Fortrania.Translation;

namespace Dracocephalum.Spice.Translation;

/// <summary>What the tool does, one method a command.</summary>
internal static class Commands
{
    private const string SettingsFile = "spice.fortrania.json";

    /// <summary>Translates the toolkit as the settings beside this project say.</summary>
    /// <param name="force">Whether every file is written again whatever was written before.</param>
    /// <returns>The exit code: zero when the translation was done.</returns>
    public static int Translate(bool force)
    {
        var folder = typeof(Commands).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(attribute => attribute.Key == "ProjectDirectory")?.Value ?? Directory.GetCurrentDirectory();
        var path = Path.Combine(folder, SettingsFile);

        var settings = TranslationSettings.Load(path);
        var result = Translator.Run(settings, folder, new TranslationOptions { Force = force });
        foreach (var message in result.Messages)
        {
            Console.Error.WriteLine(message);
        }

        Console.WriteLine($"{(result.Succeeded ? "Translated" : "Not translated")}: {result.Units} program units, {result.Written.Count} files written, {result.Removed.Count} removed.");
        if (!result.Succeeded)
        {
            return 1;
        }

        foreach (var line in LayoutReport.Write(result, Path.GetFullPath(Path.Combine(folder, settings.Output))))
        {
            Console.WriteLine(line);
        }

        return 0;
    }

    /// <summary>Rewrites what the translation just wrote: the toolkit's header in every file, and the platform in <c>ZZPLATFM</c>.</summary>
    /// <returns>The exit code: zero when every file was rewritten.</returns>
    public static int Rewrite()
    {
        if (Environment.GetEnvironmentVariable("FORTRANIA_WRITTEN") is not { } list || !File.Exists(list))
        {
            Console.Error.WriteLine("rewrite: FORTRANIA_WRITTEN names no list of files; it is run by a translation.");
            return 2;
        }

        var count = 0;
        foreach (var file in File.ReadAllLines(list).Where(line => line.EndsWith(".g.cs", StringComparison.Ordinal)))
        {
            try
            {
                var text = File.ReadAllText(file);
                var isPlatform = Path.GetFileName(file) == PlatformRewrite.FileName;
                var rewritten = ToolkitHeader.AddTo(isPlatform ? PlatformRewrite.Rewrite(text) : text, isPlatform ? PlatformRewrite.Description : null);
                if (rewritten != text)
                {
                    File.WriteAllText(file, rewritten);
                    count++;
                }
            }
            catch (RewriteException exception)
            {
                Console.Error.WriteLine($"{file}: error: {exception.Message}");
                return 1;
            }
        }

        Console.WriteLine($"Rewrote {count} files.");
        return 0;
    }

    /// <summary>Says how the tool is used.</summary>
    /// <returns>The exit code for a command line the tool does not take.</returns>
    public static int ShowUsage()
    {
        Console.Error.WriteLine("Usage: translate [--force] | rewrite");
        return 2;
    }
}
