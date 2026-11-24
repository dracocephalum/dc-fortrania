using System.Reflection;
using System.Text.Json;
using Dracocephalum.Fortrania.Translation;

namespace Dracocephalum.Fortrania.Cli;

/// <summary>
/// The <c>fortrania</c> command: reads its command line, and hands a
/// translation or a restore to <see cref="Translator"/>. It holds no rule of
/// a translation's own; what to translate, and to what, is the settings
/// file's to say.
/// </summary>
/// <remarks>
/// The first word is what to do, <c>translate</c> or <c>restore</c>, and
/// after <c>translate</c> the word for what to translate to, <c>csharp</c>
/// for C#, so that another thing to do or another target is a word beside
/// its kind. Each word and option may be shortened as
/// <see cref="UniquePrefix"/> allows. What is written to the error stream is
/// for whoever reads it, in the form a build reads for a file and line; what
/// is written to the output is the outcome. The exit code is zero when it
/// did what it was asked, one when it could not, and two when the command
/// line is not one it takes.
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
        Usage: fortrania translate csharp [settings] [--project <name>]... [--force] [--offline]
               fortrania restore [settings] [--project <name>]... [--if-needed]
               fortrania --version

          translate csharp   translates to C# as the settings file says
          restore            fetches the sources a translation would read, where
                             the settings say where from, and translates nothing
          settings           the settings file, or a folder to look for it in; the
                             one fortrania.json or *.fortrania.json found there or
                             in the nearest folder above, this one where none is
                             given
          --project          only the project of that name; each other project is
                             read from the summary its last translation wrote;
                             may be given more than once
          --force            translates every unit, whether it changed or not
          --offline          fetches nothing, and reads a project whose sources
                             are not here from its last translation's summary
          --if-needed        fetches only for a project that has no translation
          --version          says which version this is

        At a prompt, a word or an option may be shortened to any start of it
        that no other shares: fortrania t cs --f. A script or a build file
        spells each in full, since a word added later may share the start.
        """;

    private static readonly string[] Commands = ["help", "restore", "translate"];
    private static readonly string[] Options = ["--help", "--version"];
    private static readonly string[] Targets = ["csharp"];
    private static readonly string[] TranslateOptions = ["--force", "--offline", "--project"];
    private static readonly string[] RestoreOptions = ["--if-needed", "--project"];

    /// <summary>Runs the command.</summary>
    /// <param name="arguments">The command line, without the command's own name.</param>
    /// <param name="output">Where the outcome is written.</param>
    /// <param name="error">Where messages and complaints are written.</param>
    /// <param name="workingDirectory">The folder a settings file is looked for from, and a relative one taken from.</param>
    /// <returns>The exit code.</returns>
    public static int Run(string[] arguments, TextWriter output, TextWriter error, string workingDirectory)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);

        if (arguments is [] or ["-h" or "-?"])
        {
            (arguments.Length == 0 ? error : output).WriteLine(Usage);
            return arguments.Length == 0 ? Misused : Done;
        }

        var first = arguments[0].StartsWith('-')
            ? Resolve(arguments[0], Options, "an option", error)
            : Resolve(arguments[0], Commands, "a command", error);
        switch (first)
        {
            case null:
                return Misused;
            case "--help" or "help" when arguments.Length == 1:
                output.WriteLine(Usage);
                return Done;
            case "--version" when arguments.Length == 1:
                output.WriteLine(ReadVersion());
                return Done;
            case "translate" when arguments.Length > 1 && !arguments[1].StartsWith('-'):
                return Resolve(arguments[1], Targets, "a target of translate", error) switch
                {
                    "csharp" when Parse(arguments[2..], TranslateOptions, "translate csharp", error) is { } parsed =>
                        TranslateToCSharp(parsed, output, error, workingDirectory),
                    _ => Misused,
                };
            case "translate":
                return Refuse(error, "translate is to be followed by what to translate to: csharp.");
            case "restore":
                return Parse(arguments[1..], RestoreOptions, "restore", error) is { } restore
                    ? RestoreSources(restore, output, error, workingDirectory)
                    : Misused;
            default:
                return Refuse(error, $"{first} takes nothing after it.");
        }
    }

    // The word a typed one stands for, or nothing, said, where it stands for
    // none or for several.
    private static string? Resolve(string typed, IReadOnlyList<string> words, string kind, TextWriter error)
    {
        if (UniquePrefix.Resolve(typed, words, out var candidates) is { } word)
        {
            return word;
        }

        Refuse(error, candidates.Count > 0
            ? $"{typed} could be {string.Join(" or ", candidates)}; give more of it."
            : $"{typed} is not {kind}.");
        return null;
    }

    private static int Refuse(TextWriter error, string message)
    {
        error.WriteLine("fortrania: error: " + message);
        error.WriteLine(Usage);
        return Misused;
    }

    // The settings named, the projects named and the flags given after a
    // command, or nothing, said, where the words are not ones it takes.
    private static Arguments? Parse(string[] arguments, IReadOnlyList<string> options, string command, TextWriter error)
    {
        string? settings = null;
        List<string>? projects = null;
        var flags = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < arguments.Length; index++)
        {
            var argument = arguments[index];
            if (argument.StartsWith('-'))
            {
                var option = argument.StartsWith("--", StringComparison.Ordinal) && argument.Length > 2
                    ? Resolve(argument, options, $"an option of {command}", error)
                    : null;
                if (option is null)
                {
                    if (!argument.StartsWith("--", StringComparison.Ordinal) || argument.Length == 2)
                    {
                        Refuse(error, $"{argument} is not an option of {command}.");
                    }

                    return null;
                }

                if (option != "--project")
                {
                    flags.Add(option);
                    continue;
                }

                if (index + 1 == arguments.Length || arguments[index + 1].StartsWith('-'))
                {
                    Refuse(error, "--project is to be followed by the name of a project of the settings.");
                    return null;
                }

                (projects ??= []).Add(arguments[++index]);
            }
            else if (settings is null)
            {
                settings = argument;
            }
            else
            {
                Refuse(error, $"{argument} is a second settings file; {command} takes one.");
                return null;
            }
        }

        return new Arguments(settings, projects, flags);
    }

    private static int TranslateToCSharp(Arguments arguments, TextWriter output, TextWriter error, string workingDirectory)
    {
        if (Load(arguments.Settings, workingDirectory, error) is not var (path, settings))
        {
            return Failed;
        }

        // Every path in the settings is taken from where the file lies, so
        // that a translation is the same from whatever folder it is run.
        var result = Translator.Run(settings, Path.GetDirectoryName(path)!, new TranslationOptions
        {
            Force = arguments.Flags.Contains("--force"),
            Offline = arguments.Flags.Contains("--offline"),
            Projects = arguments.Projects,
        });
        foreach (var message in result.Messages)
        {
            error.WriteLine(message);
        }

        output.WriteLine(
            $"{(result.Succeeded ? "Translated" : "Not translated")}: {result.Units} program units, {result.Written.Count} files written, {result.Removed.Count} removed.");
        return result.Succeeded ? Done : Failed;
    }

    private static int RestoreSources(Arguments arguments, TextWriter output, TextWriter error, string workingDirectory)
    {
        if (Load(arguments.Settings, workingDirectory, error) is not var (path, settings))
        {
            return Failed;
        }

        var result = Translator.Restore(
            settings,
            Path.GetDirectoryName(path)!,
            new TranslationOptions { Projects = arguments.Projects },
            onlyWhereNotTranslated: arguments.Flags.Contains("--if-needed"));
        foreach (var message in result.Messages)
        {
            error.WriteLine(message);
        }

        output.WriteLine($"{(result.Succeeded ? "Restored" : "Not restored")}: {result.Fetched.Count} folders of sources fetched.");
        return result.Succeeded ? Done : Failed;
    }

    // The settings file found, and what it says; nothing, said, where there
    // is none or it is not settings.
    private static (string Path, TranslationSettings Settings)? Load(string? named, string workingDirectory, TextWriter error)
    {
        if (FindSettings(named, workingDirectory, error) is not { } path)
        {
            return null;
        }

        try
        {
            return (path, TranslationSettings.Load(path));
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
        {
            error.WriteLine($"{path}: error: {exception.Message}");
            return null;
        }
    }

    // The settings file named, taken from the working folder where it is
    // relative; or, where a folder is named or nothing is, the one with the
    // name a settings file has in that folder or the nearest above it, as a
    // build finds its own files. Several in one folder are refused; none is
    // said and is nothing.
    private static string? FindSettings(string? named, string workingDirectory, TextWriter error)
    {
        var start = workingDirectory;
        if (named is not null)
        {
            var path = Path.GetFullPath(named, workingDirectory);
            if (File.Exists(path))
            {
                return path;
            }

            if (!Directory.Exists(path))
            {
                error.WriteLine($"fortrania: error: there is no settings file {path}.");
                return null;
            }

            start = path;
        }

        for (var folder = new DirectoryInfo(start); folder is not null; folder = folder.Parent)
        {
            var found = folder.EnumerateFiles("*" + SettingsName)
                .Select(file => file.FullName)
                .Where(file => Path.GetFileName(file) is var name
                    && (name.Equals(SettingsName, StringComparison.OrdinalIgnoreCase) || name.EndsWith("." + SettingsName, StringComparison.OrdinalIgnoreCase)))
                .Order(StringComparer.Ordinal)
                .ToList();
            switch (found)
            {
                case [var only]:
                    return only;
                case [_, ..]:
                    error.WriteLine($"fortrania: error: {folder.FullName} has several settings files, {string.Join(", ", found.Select(Path.GetFileName))}; name the one to use.");
                    return null;
            }
        }

        error.WriteLine($"fortrania: error: there is no {SettingsName} or *.{SettingsName} in {start} or above it; name the settings file.");
        return null;
    }

    // The version the package was built as, with the commit it was built
    // from, as the build stamps it.
    private static string ReadVersion() =>
        typeof(CommandLine).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? typeof(CommandLine).Assembly.GetName().Version?.ToString()
            ?? "unknown";

    // What a command was told after its own words.
    private sealed record Arguments(string? Settings, IReadOnlyCollection<string>? Projects, IReadOnlySet<string> Flags);
}
