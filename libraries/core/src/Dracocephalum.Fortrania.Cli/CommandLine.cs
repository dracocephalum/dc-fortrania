using System.Reflection;
using System.Text.Json;
using Dracocephalum.Fortrania.Translation;

namespace Dracocephalum.Fortrania.Cli;

/// <summary>
/// The <c>fortrania</c> command: reads its command line, and hands a
/// translation to <see cref="Translator"/>. It holds no rule of a
/// translation's own; what to translate, and to what, is the settings file's
/// to say.
/// </summary>
/// <remarks>
/// The first word is what to do, <c>translate</c>, and after it the word for what to translate to, <c>csharp</c>
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
        Usage: fortrania translate csharp --name <project> [settings]... [--summary <file>]
                                          [--reference <assembly or summary>]... [--plugin <assembly>]...
                                          [--force] [--fetch never|once|always] [--inventory <file>]
                                          [--namespace <namespace>]
                                          [--scaffold [--entry <program>] [--framework <framework>]]
               fortrania --version

          translate csharp   translates to C# the project the settings files make
          --name             the project's name, which is its assembly's: what
                             other projects know it by and its summary is named
                             for; always given
          settings           a settings file, or a folder whose fortrania.json
                             and *.fortrania.json are all taken; this folder
                             where none is given; all of them are one project
          --summary          the file the project's summary is kept in, beside
                             its project file; <project>.fortrania-summary.json
                             beside the first settings file unless given
          --reference        what the project may call into: an assembly that
                             carries a summary, or a summary file; may be given
                             more than once
          --plugin           an assembly whose plugins take part in the
                             translation, every public type in it that
                             implements ITranslationPlugin; may be given more
                             than once, the plugins called in that order
          --force            writes every translated file again
          --fetch            when the sources the settings say where to download
                             from are downloaded: once, the default, where they
                             are not here; never, compiling a project whose
                             sources are not here as it was translated last; or
                             always, replacing what an earlier fetch made
          --inventory        writes to the file what was read and what the
                             translation is made of, for a build to tell when
                             to translate again; removes it if it fails
          --namespace        the namespace of the project's classes, over what
                             its settings say
          --scaffold         writes a project file of the project's name beside
                             its summary where there is none, and Program.cs
                             where it has a main program; after that they are
                             yours to change
          --entry            the main program Program.cs starts with, where the
                             project has several
          --framework        the target framework of a project file written;
                             net10.0 unless given

          --version          says which version this is

        At a prompt, a word or an option may be shortened to any start of it
        that no other shares: fortrania t cs --name Example --fo. A script or a
        build file spells each in full, since a word added later may share the
        start.
        """;

    private static readonly string[] Commands = ["help", "translate"];
    private static readonly string[] Options = ["--help", "--version"];
    private static readonly string[] Targets = ["csharp"];
    private static readonly string[] TranslateOptions =
        ["--entry", "--fetch", "--force", "--framework", "--inventory", "--name", "--namespace", "--plugin", "--reference", "--scaffold", "--summary"];

    // The options followed by a value; --reference and --plugin may be
    // given more than once, and the others once.
    private static readonly string[] ValuedOptions = ["--entry", "--fetch", "--framework", "--inventory", "--name", "--namespace", "--plugin", "--reference", "--summary"];

    // What --fetch takes, each spelled in full.
    private static readonly Dictionary<string, FetchPolicy> FetchPolicies = new(StringComparer.Ordinal)
    {
        ["never"] = FetchPolicy.Never,
        ["once"] = FetchPolicy.Once,
        ["always"] = FetchPolicy.Always,
    };

    // The options only a scaffold reads.
    private static readonly string[] ScaffoldOptions = ["--entry", "--framework"];

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

    // The settings named, the assemblies named, the values and the flags
    // given after a command, or nothing, said, where the words are not ones
    // it takes.
    private static Arguments? Parse(string[] arguments, IReadOnlyList<string> options, string command, TextWriter error)
    {
        var settings = new List<string>();
        List<string>? references = null;
        List<string>? plugins = null;
        var values = new Dictionary<string, string>(StringComparer.Ordinal);

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

                if (!ValuedOptions.Contains(option, StringComparer.Ordinal))
                {
                    flags.Add(option);
                    continue;
                }

                if (index + 1 == arguments.Length || arguments[index + 1].StartsWith('-'))
                {
                    Refuse(error, $"{option} is to be followed by {DescribeValue(option)}.");
                    return null;
                }

                var value = arguments[++index];
                if (option == "--reference")
                {
                    (references ??= []).Add(value);
                }
                else if (option == "--plugin")
                {
                    (plugins ??= []).Add(value);
                }
                else if (!values.TryAdd(option, value))
                {
                    Refuse(error, $"{option} is given twice; it takes one.");
                    return null;
                }
            }
            else
            {
                settings.Add(argument);
            }
        }

        return new Arguments(settings, references, plugins, values, flags);
    }

    private static string DescribeValue(string option) => option switch
    {
        "--entry" => "the name of a main program",
        "--fetch" => "never, once or always",
        "--framework" => "a target framework, net10.0 say",
        "--inventory" => "the path of the file to write",
        "--name" => "the project's name",
        "--namespace" => "a namespace",
        "--plugin" => "the path of an assembly of plugins",
        "--summary" => "the path of the summary file",
        _ => "the path of an assembly or of a summary file",
    };

    private static int TranslateToCSharp(Arguments arguments, TextWriter output, TextWriter error, string workingDirectory)
    {
        var scaffold = arguments.Flags.Contains("--scaffold");
        if (!scaffold && ScaffoldOptions.FirstOrDefault(arguments.Values.ContainsKey) is { } scaffolding)
        {
            return Refuse(error, $"{scaffolding} says what a scaffold writes, and is given without --scaffold.");
        }

        if (RequireName(arguments, error) is not { } name)
        {
            return Misused;
        }

        var fetch = FetchPolicy.Once;
        if (arguments.Values.GetValueOrDefault("--fetch") is { } fetchGiven && !FetchPolicies.TryGetValue(fetchGiven, out fetch))
        {
            return Refuse(error, $"--fetch is {fetchGiven}; it is never, once or always.");
        }

        if (Load(arguments.Settings, workingDirectory, error) is not { } loaded)
        {
            return Failed;
        }

        // Every path in the settings is taken from where the file lies, so
        // that a translation is the same from whatever folder it is run.
        var result = Translator.Run(ToProject(name, arguments, loaded, workingDirectory), new TranslationOptions
        {
            Force = arguments.Flags.Contains("--force"),
            Fetch = fetch,

            // A path named on the command line is taken from where the
            // command runs, as a person typing it means.
            References = arguments.References?.Select(reference => Path.GetFullPath(reference, workingDirectory)).ToList(),
            PluginAssemblies = arguments.Plugins?.Select(plugin => Path.GetFullPath(plugin, workingDirectory)).ToList(),
            Namespace = arguments.Values.GetValueOrDefault("--namespace"),
            Scaffold = scaffold,
            Entry = arguments.Values.GetValueOrDefault("--entry"),
            Framework = arguments.Values.GetValueOrDefault("--framework") ?? TranslationOptions.DefaultFramework,
        });
        foreach (var message in result.Messages)
        {
            error.WriteLine(message);
        }

        if (arguments.Values.GetValueOrDefault("--inventory") is { } inventory
            && !WriteInventory(Path.GetFullPath(inventory, workingDirectory), loaded.Select(file => file.Path), result, error))
        {
            return Failed;
        }

        output.WriteLine(
            $"{(result.Succeeded ? "Translated" : "Not translated")}: {result.Units} program units, {result.Written.Count} files written, {result.Removed.Count} removed.");
        return result.Succeeded ? Done : Failed;
    }

    // The project's name, which is never guessed: a build gives its
    // assembly's, and nothing else is the same wherever it is compiled.
    private static string? RequireName(Arguments arguments, TextWriter error)
    {
        if (arguments.Values.GetValueOrDefault("--name") is { Length: > 0 } name)
        {
            return name;
        }

        Refuse(error, "--name is to be given: the name of the project, its assembly's.");
        return null;
    }

    private static TranslationProject ToProject(string name, Arguments arguments, List<(string Path, TranslationSettings Settings)> loaded, string workingDirectory) => new()
    {
        Name = name,
        Settings = [.. loaded.Select(file => file.Settings)],
        Summary = arguments.Values.GetValueOrDefault("--summary") is { } summary ? Path.GetFullPath(summary, workingDirectory) : null,
    };

    // What a build reads to tell when to translate again, a line a file:
    // "read" for what the translation was made from, the settings files and
    // the plugins among it, "pattern" for a file pattern its sources name,
    // and "file" for what the translation is made of. It is written only for
    // a translation that was done, and one that was not removes the last, so
    // that a build that failed is not taken for one that needs nothing.
    private static bool WriteInventory(string inventory, IEnumerable<string> settingsPaths, TranslationResult result, TextWriter error)
    {
        try
        {
            if (!result.Succeeded)
            {
                File.Delete(inventory);
                return true;
            }

            var read = settingsPaths.SelectMany(path => new[] { path, TranslationSettings.GetLocalFile(path) }.Where(File.Exists));
            var lines = read.Concat(result.Read).Select(file => "read " + file)
                .Concat(result.Patterns.Select(pattern => "pattern " + pattern))
                .Concat(result.Files.Select(file => "file " + file));
            Directory.CreateDirectory(Path.GetDirectoryName(inventory) ?? ".");
            File.WriteAllLines(inventory, lines);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            error.WriteLine($"{inventory}: error: {exception.Message}");
            return false;
        }
    }

    // Each settings file named, and those of each folder named, or of the
    // working folder where nothing is, and what each says; nothing, with
    // each problem said, where one is not there or is not settings. A file
    // named twice, or both by itself and by its folder, is taken once.
    private static List<(string Path, TranslationSettings Settings)>? Load(IReadOnlyList<string> named, string workingDirectory, TextWriter error)
    {
        var loaded = new List<(string Path, TranslationSettings Settings)>();
        var taken = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var failed = false;
        foreach (var name in named.Count == 0 ? [null] : named.Cast<string?>())
        {
            if (FindSettings(name, workingDirectory, error) is not { } paths)
            {
                failed = true;
                continue;
            }

            foreach (var path in paths.Where(taken.Add))
            {
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
        }

        return failed ? null : loaded;
    }

    // The settings file named, taken from the working folder where it is
    // relative; or, where a folder is named or nothing is, every one with
    // the name a settings file has in that folder or the working one, in
    // the order of their names. None is said and is nothing: no folder
    // above is looked in, so that no file is taken that nobody pointed at.
    private static List<string>? FindSettings(string? named, string workingDirectory, TextWriter error)
    {
        var folder = workingDirectory;
        if (named is not null)
        {
            var path = Path.GetFullPath(named, workingDirectory);
            if (File.Exists(path))
            {
                return [path];
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
        if (found.Count == 0)
        {
            error.WriteLine($"fortrania: error: there is no {SettingsName} or *.{SettingsName} in {folder}; name the settings file.");
            return null;
        }

        return found;
    }

    // The version the package was built as, with the commit it was built
    // from, as the build stamps it.
    private static string ReadVersion() =>
        typeof(CommandLine).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? typeof(CommandLine).Assembly.GetName().Version?.ToString()
            ?? "unknown";

    // What a command was told after its own words: the values by their
    // options.
    private sealed record Arguments(
        IReadOnlyList<string> Settings,
        IReadOnlyCollection<string>? References,
        IReadOnlyCollection<string>? Plugins,
        IReadOnlyDictionary<string, string> Values,
        IReadOnlySet<string> Flags);
}
