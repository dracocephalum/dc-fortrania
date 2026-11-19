using System.Reflection;
using System.Text.RegularExpressions;
using Dracocephalum.Fortrania.Analysis;
using Dracocephalum.Fortrania.Emit;
using Dracocephalum.Fortrania.Syntax;

namespace Dracocephalum.Fortrania.Translation;

/// <summary>
/// Does a whole translation as its settings describe it: reads the FORTRAN
/// of every source, checks it, and writes the C#, and a project around it
/// where there is none yet. Either all of it is written or none: with an
/// error in the settings or in any source, nothing on disk is touched.
/// </summary>
public static partial class Translator
{
    /// <summary>The folder of a project that translated sources are written to, a folder per source inside it.</summary>
    public const string GeneratedFolder = "Generated";

    /// <summary>What the name of a translated file ends in.</summary>
    public const string GeneratedExtension = ".g.cs";

    /// <summary>Does the translation.</summary>
    /// <param name="settings">What to translate, and to what.</param>
    /// <param name="baseDirectory">The folder the paths in the settings are taken from: where the settings file lies.</param>
    /// <returns>What was done, and what there is to say about it.</returns>
    public static TranslationResult Run(TranslationSettings settings, string baseDirectory)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentException.ThrowIfNullOrEmpty(baseDirectory);

        return new Translation(settings, Path.GetFullPath(baseDirectory)).Run();
    }

    [GeneratedRegex(@"^[A-Za-z_][A-Za-z0-9_]*$")]
    private static partial Regex Identifier();

    // One program unit as it was read.
    private sealed record Unit(string File, SymbolTable Table, UnitSummary Summary)
    {
        public string Name => Summary.Name ?? CSharpEmitter.MainProgramName;
    }

    // One source with what was read of it.
    private sealed class Source(ProjectSettings project, SourceSettings settings)
    {
        public ProjectSettings Project { get; } = project;

        public SourceSettings Settings { get; } = settings;

        public string Namespace { get; } = project.Namespace ?? project.Name;

        public string ClassName { get; } = settings.Class ?? settings.Name;

        public List<Unit> Units { get; } = [];

        public ProcedureCatalogue Catalogue { get; } = new();

        // The text of each file to write, by its name.
        public Dictionary<string, string> Files { get; } = new(StringComparer.OrdinalIgnoreCase);
    }

    private sealed class Translation(TranslationSettings settings, string baseDirectory)
    {
        private readonly List<TranslationMessage> _messages = [];
        private readonly List<Source> _sources = [];
        private readonly Dictionary<ProjectSettings, List<ProjectSettings>> _references = [];
        private readonly Dictionary<ProjectSettings, string> _entries = [];
        private readonly Dictionary<string, IncludeFile?> _includes = new(StringComparer.Ordinal);
        private readonly List<string> _written = [];
        private readonly List<string> _removed = [];

        private bool HasErrors => _messages.Any(message => message.IsError);

        public TranslationResult Run()
        {
            CheckSettings();
            if (!HasErrors)
            {
                Read();
            }

            if (!HasErrors)
            {
                CheckCalls();
                WorkOutReferences();
                WorkOutEntries();
            }

            if (!HasErrors)
            {
                Emit();
            }

            if (!HasErrors)
            {
                Write();
            }

            return new TranslationResult(_messages, _written, _removed, HasErrors ? 0 : _sources.Sum(source => source.Units.Count));
        }

        private void Error(string text, string? file = null, int line = 0) => _messages.Add(new TranslationMessage(true, file, line, text));

        private void Warning(string text, string? file = null, int line = 0) => _messages.Add(new TranslationMessage(false, file, line, text));

        private string FolderOf(ProjectSettings project) =>
            Path.GetFullPath(Path.Combine(baseDirectory, project.Output ?? Path.Combine(settings.Output, project.Name)));

        private static string ProjectFileOf(ProjectSettings project, string folder) => Path.Combine(folder, project.Name + ".csproj");

        // A project that has a project file is one somebody made or
        // changed, and is given sources and nothing else.
        private static bool IsScaffolded(string folder) =>
            !Directory.Exists(folder) || Directory.GetFiles(folder, "*.csproj").Length == 0;

        private void CheckSettings()
        {
            if (settings.Projects.Count == 0)
            {
                Error("The settings name no project.");
            }

            var classes = new HashSet<string>(StringComparer.Ordinal);
            var projects = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var project in settings.Projects)
            {
                var name = project.Name;
                var space = project.Namespace ?? name;
                if (name.Length == 0 || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                {
                    Error($"A project has to have a name that can name a folder; '{name}' cannot.");
                    continue;
                }

                if (!projects.Add(name))
                {
                    Error($"Two projects are named {name}.");
                }

                var parts = space.Split('.');
                if (parts.Any(part => !Identifier().IsMatch(part)))
                {
                    Error($"The namespace {space} of the project {name} is not a namespace; give the project one.");
                }

                if (project.Sources.Count == 0)
                {
                    Error($"The project {name} has no sources.");
                }

                foreach (var source in project.Sources)
                {
                    var className = source.Class ?? source.Name;
                    if (source.Name.Length == 0 || source.Name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                    {
                        Error($"A source of the project {name} has to have a name that can name a folder; '{source.Name}' cannot.");
                    }
                    else if (!Identifier().IsMatch(className))
                    {
                        Error($"The source {source.Name} of the project {name} needs a class name; '{className}' is not one.");
                    }
                    else if (parts.Contains(className, StringComparer.Ordinal))
                    {
                        // C# allows it, and every use of the name then
                        // trips over it. Renaming is the user's to do.
                        Error($"The class {className} of the project {name} has the name of a part of its namespace {space}; give the source a class of another name.");
                    }
                    else if (!classes.Add(space + "." + className))
                    {
                        Error($"Two sources become the class {space}.{className}.");
                    }

                    if (source.Files.Count == 0)
                    {
                        Error($"The source {source.Name} of the project {name} names no files.");
                    }
                }

                foreach (var reference in project.References ?? [])
                {
                    if (!settings.Projects.Any(other => other != project && other.Name == reference))
                    {
                        Error($"The project {name} refers to {reference}, which is no other project of the settings.");
                    }
                }
            }
        }

        private void Read()
        {
            foreach (var project in settings.Projects)
            {
                foreach (var sourceSettings in project.Sources)
                {
                    var source = new Source(project, sourceSettings);
                    _sources.Add(source);

                    foreach (var file in FilesOf(sourceSettings, project))
                    {
                        Read(source, file);
                    }
                }
            }
        }

        private List<string> FilesOf(SourceSettings source, ProjectSettings project)
        {
            var files = new List<string>();
            foreach (var entry in source.Files)
            {
                var path = Path.GetFullPath(Path.Combine(baseDirectory, entry));
                var folder = Path.GetDirectoryName(path)!;
                var found = Directory.Exists(folder)
                    ? Directory.GetFiles(folder, Path.GetFileName(path)).Order(StringComparer.Ordinal).ToList()
                    : [];
                if (found.Count == 0)
                {
                    Error($"The source {source.Name} of the project {project.Name} names '{entry}', and there is no such file.");
                }

                files.AddRange(found.Where(file => !files.Contains(file, StringComparer.OrdinalIgnoreCase)));
            }

            return files;
        }

        private void Read(Source source, string file)
        {
            try
            {
                using var reader = new StreamReader(file);
                foreach (var unit in SourceParser.Parse(FixedFormReader.Read(reader)).Units)
                {
                    var table = SymbolTableBuilder.Build(unit, name => Include(source, file, name));
                    TypeChecker.Check(table);
                    var summary = UnitSummarizer.Summarize(table);
                    source.Catalogue.Add(summary);
                    source.Units.Add(new Unit(file, table, summary));
                }
            }
            catch (Exception exception) when (IsOfTheSource(exception))
            {
                Error(exception.Message, file);
            }
        }

        // An include file is looked for beside the file that includes it,
        // and then in the folders the source names.
        private IncludeFile? Include(Source source, string file, string name)
        {
            var folders = source.Settings.Includes
                .Select(folder => Path.GetFullPath(Path.Combine(baseDirectory, folder)))
                .Prepend(Path.GetDirectoryName(file)!);

            foreach (var folder in folders)
            {
                var path = Path.Combine(folder, name);
                if (_includes.TryGetValue(path, out var known))
                {
                    return known;
                }

                if (File.Exists(path))
                {
                    using var reader = new StreamReader(path);
                    return _includes[path] = SourceParser.ParseInclude(FixedFormReader.Read(reader));
                }
            }

            return null;
        }

        private static bool IsOfTheSource(Exception exception) =>
            exception is FixedFormSyntaxException or FortranSyntaxException or FortranAnalysisException or UnsupportedConstructException;

        private Source? OwnerOf(string name) => _sources.FirstOrDefault(source => source.Catalogue.Find(name) is not null);

        // The libraries of a program may each have a procedure of one name;
        // a call reaches the one in its own source first, then one of its
        // own project, then any.
        private IEnumerable<Source> Visible(Source from) => _sources
            .Where(source => source != from)
            .OrderBy(source => source.Project == from.Project ? 0 : 1);

        private void CheckCalls()
        {
            foreach (var source in _sources)
            {
                ProcedureInterface? Find(string name) =>
                    source.Catalogue.Find(name) ?? Visible(source).Select(other => other.Catalogue.Find(name)).FirstOrDefault(found => found is not null);

                foreach (var unit in source.Units)
                {
                    try
                    {
                        foreach (var missing in CallChecker.Check(unit.Summary, Find).Where(name => !CSharpEmitter.IsSuppliedByContext(name)))
                        {
                            Warning($"{unit.Name} calls {missing}, which no source of the settings defines.", unit.File);
                        }
                    }
                    catch (FortranAnalysisException exception)
                    {
                        Error(exception.Message, unit.File);
                    }
                }
            }
        }

        private void WorkOutReferences()
        {
            foreach (var project in settings.Projects)
            {
                var used = _sources
                    .Where(source => source.Project == project)
                    .SelectMany(source => source.Units.SelectMany(unit => unit.Summary.Calls)
                        .Where(call => source.Catalogue.Find(call.Name) is null)
                        .Select(call => Visible(source).FirstOrDefault(other => other.Catalogue.Find(call.Name) is not null)))
                    .Where(owner => owner is not null && owner.Project != project)
                    .Select(owner => owner!.Project)
                    .Distinct()
                    .ToList();

                if (project.References is { } given)
                {
                    foreach (var missing in used.Where(other => !given.Contains(other.Name, StringComparer.Ordinal)))
                    {
                        Error($"The project {project.Name} calls into {missing.Name} and does not refer to it.");
                    }

                    _references[project] = [.. settings.Projects.Where(other => given.Contains(other.Name, StringComparer.Ordinal))];
                }
                else
                {
                    _references[project] = [.. settings.Projects.Where(used.Contains)];
                }
            }

            // Assemblies cannot refer to each other in a circle.
            foreach (var project in settings.Projects)
            {
                if (Reaches(project, project, []))
                {
                    Error($"The project {project.Name} refers to itself by way of the projects it refers to; sources that call each other both ways belong in one project.");
                    break;
                }
            }
        }

        private bool Reaches(ProjectSettings from, ProjectSettings target, HashSet<ProjectSettings> seen) =>
            _references.GetValueOrDefault(from, []).Any(next => next == target || (seen.Add(next) && Reaches(next, target, seen)));

        // An executable starts with one main program. That is asked only
        // of a project that is being made: in one that exists no Main is
        // written, so there is nothing to choose.
        private void WorkOutEntries()
        {
            foreach (var project in settings.Projects)
            {
                var mains = _sources
                    .Where(source => source.Project == project)
                    .SelectMany(source => source.Units.Where(unit => unit.Summary.Kind == UnitKind.MainProgram).Select(unit => (Source: source, Unit: unit)))
                    .ToList();

                var kind = project.Kind ?? (mains.Count > 0 ? ProjectKind.Executable : ProjectKind.Library);
                if (kind != ProjectKind.Executable || !IsScaffolded(FolderOf(project)))
                {
                    continue;
                }

                if (mains.Count == 0)
                {
                    Error($"The project {project.Name} is to be an executable and has no main program.");
                }
                else if (project.Entry is { } entry)
                {
                    var chosen = mains.Where(main => string.Equals(main.Unit.Name, entry, StringComparison.OrdinalIgnoreCase)).ToList();
                    if (chosen.Count == 1)
                    {
                        _entries[project] = $"context.Get<{chosen[0].Source.ClassName}>().{chosen[0].Unit.Name}();";
                    }
                    else
                    {
                        Error($"The project {project.Name} names {entry} as its entry, which is not one of its main programs: {string.Join(", ", mains.Select(main => main.Unit.Name))}.");
                    }
                }
                else if (mains.Count == 1)
                {
                    _entries[project] = $"context.Get<{mains[0].Source.ClassName}>().{mains[0].Unit.Name}();";
                }
                else
                {
                    Error($"The project {project.Name} has {mains.Count} main programs, {string.Join(", ", mains.Select(main => main.Unit.Name))}; name one as its entry.");
                }
            }
        }

        private void Emit()
        {
            foreach (var source in _sources)
            {
                var options = new EmitOptions(source.Namespace, source.ClassName)
                {
                    References = [.. Visible(source).Select(other => new LibraryReference(other.Namespace, other.ClassName, other.Catalogue.Find))],
                };

                var emitter = new CSharpEmitter(options, source.Catalogue.Find);
                foreach (var unit in source.Units)
                {
                    try
                    {
                        var name = FileName(unit.Name);
                        for (var number = 2; source.Files.ContainsKey(name); number++)
                        {
                            name = FileName(string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{unit.Name}_{number}"));
                        }

                        source.Files[name] = emitter.Emit(unit.Table);
                    }
                    catch (Exception exception) when (IsOfTheSource(exception))
                    {
                        Error(exception.Message, unit.File);
                    }
                }

                source.Files[FileName(source.ClassName)] = emitter.EmitClass();
            }
        }

        // A file named for a routine, which on some systems may not have
        // the name of a device whatever follows the name.
        private static string FileName(string name)
        {
            string[] devices = ["CON", "PRN", "AUX", "NUL"];
            var isDevice = devices.Contains(name, StringComparer.OrdinalIgnoreCase)
                || (name.Length == 4 && char.IsAsciiDigit(name[3]) && (name.StartsWith("COM", StringComparison.OrdinalIgnoreCase) || name.StartsWith("LPT", StringComparison.OrdinalIgnoreCase)));
            return (isDevice ? "_" + name : name) + GeneratedExtension;
        }

        private void Write()
        {
            // What a project is, is decided before any is written: writing
            // one makes it a project that exists.
            var scaffolded = settings.Projects.Where(project => IsScaffolded(FolderOf(project))).ToHashSet();

            foreach (var project in settings.Projects)
            {
                var folder = FolderOf(project);
                foreach (var source in _sources.Where(source => source.Project == project))
                {
                    var generated = Path.Combine(folder, GeneratedFolder, source.Settings.Name);
                    Directory.CreateDirectory(generated);

                    foreach (var (name, text) in source.Files)
                    {
                        WriteIfChanged(Path.Combine(generated, name), text);
                    }

                    // What an earlier translation wrote and this one does
                    // not is removed, and only that: a file that does not
                    // say it was generated is somebody's.
                    foreach (var file in Directory.GetFiles(generated, "*" + GeneratedExtension))
                    {
                        if (!source.Files.ContainsKey(Path.GetFileName(file)) && File.ReadLines(file).FirstOrDefault() == "// <auto-generated>")
                        {
                            File.Delete(file);
                            _removed.Add(file);
                        }
                    }
                }

                if (scaffolded.Contains(project))
                {
                    WriteIfChanged(ProjectFileOf(project, folder), ProjectFile(project, folder));
                    if (_entries.TryGetValue(project, out var entry))
                    {
                        WriteIfChanged(Path.Combine(folder, "Program.cs"), Program(project, entry));
                    }
                }
                else
                {
                    ReportMissingReferences(project, folder);
                }
            }
        }

        private void WriteIfChanged(string path, string text)
        {
            if (!File.Exists(path) || File.ReadAllText(path) != text)
            {
                File.WriteAllText(path, text);
                _written.Add(path);
            }
        }

        // A project file that exists is not changed. What it lacks of the
        // references the calls need is said, for its owner to add.
        private void ReportMissingReferences(ProjectSettings project, string folder)
        {
            var file = Directory.GetFiles(folder, "*.csproj")[0];
            var text = File.ReadAllText(file);
            foreach (var reference in _references[project])
            {
                if (!text.Contains(reference.Name + ".csproj", StringComparison.OrdinalIgnoreCase))
                {
                    Warning($"The project calls into {reference.Name} and its project file does not refer to it.", file);
                }
            }
        }

        private string ProjectFile(ProjectSettings project, string folder)
        {
            string Relative(string path) => Path.GetRelativePath(folder, path).Replace('\\', '/');

            var runtime = settings.Runtime.Project is { } runtimeProject
                ? $"    <ProjectReference Include=\"{Relative(Path.GetFullPath(Path.Combine(baseDirectory, runtimeProject)))}\" />"
                : $"    <PackageReference Include=\"Dracocephalum.Fortrania.Runtime\" Version=\"{settings.Runtime.Package ?? OwnVersion()}\" />";

            var lines = new List<string>
            {
                "<Project Sdk=\"Microsoft.NET.Sdk\">",
                string.Empty,
                "  <PropertyGroup>",
                $"    <TargetFramework>{settings.Framework}</TargetFramework>",
            };

            if (_entries.ContainsKey(project))
            {
                lines.Add("    <OutputType>Exe</OutputType>");
            }

            lines.AddRange(
            [
                $"    <RootNamespace>{project.Namespace ?? project.Name}</RootNamespace>",
                "    <Nullable>enable</Nullable>",
                "    <AllowUnsafeBlocks>true</AllowUnsafeBlocks>",
                "  </PropertyGroup>",
                string.Empty,
                "  <ItemGroup>",
                runtime,
            ]);

            lines.AddRange(_references[project].Select(reference =>
                $"    <ProjectReference Include=\"{Relative(ProjectFileOf(reference, FolderOf(reference)))}\" />"));
            lines.AddRange(["  </ItemGroup>", string.Empty, "</Project>", string.Empty]);
            return string.Join('\n', lines);
        }

        private static string Program(ProjectSettings project, string entry) =>
            $$"""
            using Dracocephalum.Fortrania.Runtime;

            namespace {{project.Namespace ?? project.Name}};

            /// <summary>Starts the translated program as the FORTRAN was started: with the console, its command line, and leave to run commands.</summary>
            internal static class Program
            {
                private static int Main()
                {
                    using var context = new FortranContext(FortranContextOptions.Standalone);
                    try
                    {
                        {{entry}}
                        return 0;
                    }
                    catch (FortranStopException stop)
                    {
                        return stop.ExitCode;
                    }
                }
            }

            """.ReplaceLineEndings("\n");

        private static string OwnVersion()
        {
            var version = typeof(Translator).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "*";
            var metadata = version.IndexOf('+', StringComparison.Ordinal);
            return metadata < 0 ? version : version[..metadata];
        }
    }
}
