using System.Reflection;
using System.Text.Json;
using Dracocephalum.Fortrania.Translation;

namespace Dracocephalum.Fortrania.Cli;

/// <summary>
/// The <c>fortrania</c> command: reads its command line, and hands a
/// translation to <see cref="Translator"/>. It holds no rule of a
/// translation's own; what to translate, and to what, is the settings
/// file's to say.
/// </summary>
/// <remarks>
/// The first word is what to produce, <c>cs</c> for C#, so that another
/// target or another thing to do is a word beside it. What is written to
/// the error stream is for whoever reads it, in the form a build reads
/// for a file and line; what is written to the output is the outcome.
/// The exit code is zero when it did what it was asked, one when it
/// could not, and two when the command line is not one it takes.
/// </remarks>
internal static class CommandLine
{
    /// <summary>Exit code: done as asked.</summary>
    public const int Done = 0;

    /// <summary>Exit code: the settings or the translation failed.</summary>
    public const int Failed = 1;

    /// <summary>Exit code: the command line is not one the command takes.</summary>
    public const int Misused = 2;

    /// <summary>The name a settings file found by itself has, or ends in after a name of its own.</summary>
    public const string SettingsName = "fortrania.json";

    private const string Usage =
        """
        Usage: fortrania cs [settings] [--force]
               fortrania --version

          cs          translates to C# as the settings file says
          settings    the settings file; where none is given, the one
                      fortrania.json or *.fortrania.json in this folder
          --force     translates every unit, whether it changed or not
          --version   says which version this is
        """;

    /// <summary>Runs the command.</summary>
    /// <param name="arguments">The command line, without the command's own name.</param>
    /// <param name="output">Where the outcome is written.</param>
    /// <param name="error">Where messages and complaints are written.</param>
    /// <param name="workingDirectory">The folder a settings file is looked for in, and a relative one taken from.</param>
    /// <returns>The exit code.</returns>
    public static int Run(string[] arguments, TextWriter output, TextWriter error, string workingDirectory)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);

        switch (arguments)
        {
            case ["--help" or "-h" or "-?" or "help"]:
                output.WriteLine(Usage);
                return Done;
            case ["--version"]:
                output.WriteLine(ReadVersion());
                return Done;
            case ["cs", .. var rest]:
                return TranslateToCSharp(rest, output, error, workingDirectory);
            default:
                error.WriteLine(Usage);
                return Misused;
        }
    }

    private static int TranslateToCSharp(IReadOnlyList<string> arguments, TextWriter output, TextWriter error, string workingDirectory)
    {
        var force = false;
        string? named = null;
        foreach (var argument in arguments)
        {
            if (argument == "--force")
            {
                force = true;
            }
            else if (argument.StartsWith('-') || named is not null)
            {
                error.WriteLine($"fortrania: error: {argument} is not an option of cs, or is a second settings file.");
                error.WriteLine(Usage);
                return Misused;
            }
            else
            {
                named = argument;
            }
        }

        if (FindSettings(named, workingDirectory, error) is not { } path)
        {
            return Failed;
        }

        TranslationSettings settings;
        try
        {
            settings = TranslationSettings.Load(path);
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
        {
            error.WriteLine($"{path}: error: {exception.Message}");
            return Failed;
        }

        // Every path in the settings is taken from where the file lies, so
        // that a translation is the same from whatever folder it is run.
        var result = Translator.Run(settings, Path.GetDirectoryName(path)!, new TranslationOptions { Force = force });
        foreach (var message in result.Messages)
        {
            error.WriteLine(message);
        }

        output.WriteLine(
            $"{(result.Succeeded ? "Translated" : "Not translated")}: {result.Units} program units, {result.Written.Count} files written, {result.Removed.Count} removed.");
        return result.Succeeded ? Done : Failed;
    }

    // The settings file named, taken from the working folder where it is
    // relative; or where none is named, the one in the working folder with
    // the name a settings file has. None, or several to choose from, is
    // said and is nothing.
    private static string? FindSettings(string? named, string workingDirectory, TextWriter error)
    {
        if (named is not null)
        {
            var path = Path.GetFullPath(named, workingDirectory);
            if (File.Exists(path))
            {
                return path;
            }

            error.WriteLine($"fortrania: error: there is no settings file {path}.");
            return null;
        }

        var found = Directory.EnumerateFiles(workingDirectory, "*" + SettingsName)
            .Where(file => Path.GetFileName(file) is var name
                && (name.Equals(SettingsName, StringComparison.OrdinalIgnoreCase) || name.EndsWith("." + SettingsName, StringComparison.OrdinalIgnoreCase)))
            .Order(StringComparer.Ordinal)
            .ToList();
        switch (found)
        {
            case [var only]:
                return only;
            case []:
                error.WriteLine($"fortrania: error: there is no {SettingsName} or *.{SettingsName} in {workingDirectory}; name the settings file.");
                return null;
            default:
                error.WriteLine($"fortrania: error: {workingDirectory} has several settings files, {string.Join(", ", found.Select(Path.GetFileName))}; name the one to use.");
                return null;
        }
    }

    // The version the package was built as, with the commit it was built
    // from, as the build stamps it.
    private static string ReadVersion() =>
        typeof(CommandLine).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? typeof(CommandLine).Assembly.GetName().Version?.ToString()
            ?? "unknown";
}
