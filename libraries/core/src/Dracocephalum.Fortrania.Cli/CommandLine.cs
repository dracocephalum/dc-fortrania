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
        Usage: fortrania translate csharp [settings]... [--reference <assembly>]... [--force] [--offline] [--record <file>]
               fortrania restore [settings]... [--if-needed]
               fortrania --version

          translate csharp   translates to C# the project each settings file says
          restore            fetches the sources a translation would read, where
                             the settings say where from, and translates nothing
          settings           a project's settings file, or a folder holding the
                             one fortrania.json or *.fortrania.json; this folder
                             where none is given; several are translated together,
                             each calling into those it needs of the others
          --reference        an assembly a translated project may call into, a
                             translation compiled that carries its project's
                             summary; may be given more than once
          --force            translates every unit, whether it changed or not
          --offline          fetches nothing, and reads a project whose sources
                             are not here from its last translation's summary
          --record           writes to the file what was read and what the
                             translation is made of, for a build to tell when
                             to translate again; removes it if it fails

          --if-needed        fetches only for a project that has no translation
          --version          says which version this is

        At a prompt, a word or an option may be shortened to any start of it
        that no other shares: fortrania t cs --f. A script or a build file
        spells each in full, since a word added later may share the start.
        """;

    private static readonly string[] Commands = ["help", "restore", "translate"];
    private static readonly string[] Options = ["--help", "--version"];
    private static readonly string[] Targets = ["csharp"];
    private static readonly string[] TranslateOptions = ["--force", "--offline", "--record", "--reference"];
    private static readonly string[] RestoreOptions = ["--if-needed"];

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

    // The settings named, the assemblies named and the flags given after a
    // command, or nothing, said, where the words are not ones it takes.
    private static Arguments? Parse(string[] arguments, IReadOnlyList<string> options, string command, TextWriter error)
    {
        var settings = new List<string>();
        List<string>? references = null;
        string? record = null;

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

                if (option is not ("--record" or "--reference"))
                {
                    flags.Add(option);
                    continue;
                }

                if (index + 1 == arguments.Length || arguments[index + 1].StartsWith('-'))
                {
                    Refuse(error, option switch
                    {
                        "--record" => "--record is to be followed by the path of the file to write.",
                        _ => "--reference is to be followed by the path of an assembly.",
                    });
                    return null;
                }

                var value = arguments[++index];
                switch (option)
                {
                    case "--reference":
                        (references ??= []).Add(value);
                        break;
                    case "--record" when record is not null:
                        Refuse(error, "--record is given twice; it takes one.");
                        return null;
                    default:
                        record = value;
                        break;
                }
            }
            else
            {
                settings.Add(argument);
            }
        }

        return new Arguments(settings, references, record, flags);
    }

    private static int TranslateToCSharp(Arguments arguments, TextWriter output, TextWriter error, string workingDirectory)
    {
        if (Load(arguments.Settings, workingDirectory, error) is not { } loaded)
        {
            return Failed;
        }

        // Every path in the settings is taken from where the file lies, so
        // that a translation is the same from whatever folder it is run.
        var result = Translator.Run([.. loaded.Select(file => file.Settings)], new TranslationOptions
        {
            Force = arguments.Flags.Contains("--force"),
            Offline = arguments.Flags.Contains("--offline"),

            // An assembly named on the command line is taken from where the
            // command runs, as a person typing it means.
            References = arguments.References?.Select(reference => Path.GetFullPath(reference, workingDirectory)).ToList(),
        });
        foreach (var message in result.Messages)
        {
            error.WriteLine(message);
        }

        if (arguments.Record is { } record && !WriteRecord(Path.GetFullPath(record, workingDirectory), loaded.Select(file => file.Path), result, error))
        {
            return Failed;
        }

        output.WriteLine(
            $"{(result.Succeeded ? "Translated" : "Not translated")}: {result.Units} program units, {result.Written.Count} files written, {result.Removed.Count} removed.");
        return result.Succeeded ? Done : Failed;
    }

    // What a build reads to tell when to translate again, a line a file:
    // "read" for what the translation was made from, the settings files among it,
    // "pattern" for a file pattern its sources name, and "file" for what the
    // translation is made of. It is written only for a translation that was
    // done, and one that was not removes the last, so that a build that
    // failed is not taken for one that needs nothing.
    private static bool WriteRecord(string record, IEnumerable<string> settingsPaths, TranslationResult result, TextWriter error)
    {
        try
        {
            if (!result.Succeeded)
            {
                File.Delete(record);
                return true;
            }

            var read = settingsPaths.SelectMany(path => new[] { path, TranslationSettings.LocalFileOf(path) }.Where(File.Exists));
            var lines = read.Concat(result.Read).Select(file => "read " + file)
                .Concat(result.Patterns.Select(pattern => "pattern " + pattern))
                .Concat(result.Files.Select(file => "file " + file));
            Directory.CreateDirectory(Path.GetDirectoryName(record) ?? ".");
            File.WriteAllLines(record, lines);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            error.WriteLine($"{record}: error: {exception.Message}");
            return false;
        }
    }

    private static int RestoreSources(Arguments arguments, TextWriter output, TextWriter error, string workingDirectory)
    {
        if (Load(arguments.Settings, workingDirectory, error) is not { } loaded)
        {
            return Failed;
        }

        var result = Translator.Restore(
            [.. loaded.Select(file => file.Settings)],
            new TranslationOptions(),
            onlyWhereNotTranslated: arguments.Flags.Contains("--if-needed"));
        foreach (var message in result.Messages)
        {
            error.WriteLine(message);
        }

        output.WriteLine($"{(result.Succeeded ? "Restored" : "Not restored")}: {result.Fetched.Count} folders of sources fetched.");
        return result.Succeeded ? Done : Failed;
    }

    // Each settings file named, or the one found where none is, and what it
    // says; nothing, with each problem said, where one is not there or is
    // not settings.
    private static List<(string Path, TranslationSettings Settings)>? Load(IReadOnlyList<string> named, string workingDirectory, TextWriter error)
    {
        var loaded = new List<(string Path, TranslationSettings Settings)>();
        var failed = false;
        foreach (var name in named.Count == 0 ? [null] : named.Cast<string?>())
        {
            if (FindSettings(name, workingDirectory, error) is not { } path)
            {
                failed = true;
                continue;
            }

            try
            {
                loaded.Add((path, TranslationSettings.Load(path)));
            }
            catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
            {
                error.WriteLine($"{path}: error: {exception.Message}");
                failed = true;
            }
        }

        return failed ? null : loaded;
    }

    // The settings file named, taken from the working folder where it is
    // relative; or, where a folder is named or nothing is, the one with the
    // name a settings file has in that folder or the working one. None, or
    // several to choose from, is said and is nothing: no folder above is
    // looked in, so that no file is taken that nobody pointed at.
    private static string? FindSettings(string? named, string workingDirectory, TextWriter error)
    {
        var folder = workingDirectory;
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

            folder = path;
        }

        var found = Directory.EnumerateFiles(folder, "*" + SettingsName)
            .Where(file => Path.GetFileName(file) is var name
                && (name.Equals(SettingsName, StringComparison.OrdinalIgnoreCase) || name.EndsWith("." + SettingsName, StringComparison.OrdinalIgnoreCase)))
            .Order(StringComparer.Ordinal)
            .ToList();
        switch (found)
        {
            case [var only]:
                return only;
            case []:
                error.WriteLine($"fortrania: error: there is no {SettingsName} or *.{SettingsName} in {folder}; name the settings file.");
                return null;
            default:
                error.WriteLine($"fortrania: error: {folder} has several settings files, {string.Join(", ", found.Select(Path.GetFileName))}; name the one to use.");
                return null;
        }
    }

    // The version the package was built as, with the commit it was built
    // from, as the build stamps it.
    private static string ReadVersion() =>
        typeof(CommandLine).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? typeof(CommandLine).Assembly.GetName().Version?.ToString()
            ?? "unknown";

    // What a command was told after its own words.
    private sealed record Arguments(IReadOnlyList<string> Settings, IReadOnlyCollection<string>? References, string? Record, IReadOnlySet<string> Flags);
}
