using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
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

    /// <summary>
    /// The file in each folder of translated sources that records what the
    /// translator wrote there, so that a file a later step has changed is
    /// left alone until what the translator would write is different.
    /// </summary>
    public const string ManifestName = ".fortrania.json";

    private const string ProjectFileExtension = ".csproj";

    /// <summary>Translates one project.</summary>
    /// <param name="settings">What to translate, and to what, with the folder they lie in.</param>
    /// <returns>What was done, and what there is to say about it.</returns>
    /// <exception cref="ArgumentException">The settings do not say what folder they lie in.</exception>
    public static TranslationResult Run(TranslationSettings settings) => Run([settings], new TranslationOptions());

    /// <summary>Translates one project, in the way the options say.</summary>
    /// <param name="settings">What to translate, and to what, with the folder they lie in.</param>
    /// <param name="options">How this run is to go.</param>
    /// <returns>What was done, and what there is to say about it.</returns>
    /// <exception cref="ArgumentException">The settings do not say what folder they lie in.</exception>
    public static TranslationResult Run(TranslationSettings settings, TranslationOptions options) => Run([settings], options);

    /// <summary>Translates several projects in one run.</summary>
    /// <param name="projects">The settings of each project, with the folder each lies in.</param>
    /// <returns>What was done, and what there is to say about it.</returns>
    /// <exception cref="ArgumentException">Settings do not say what folder they lie in.</exception>
    public static TranslationResult Run(IReadOnlyList<TranslationSettings> projects) => Run(projects, new TranslationOptions());

    /// <summary>
    /// Translates several projects in one run, in the way the options say.
    /// The projects call into each other as one translation: a project
    /// translated with those it calls into is translated as it would be
    /// against their summaries.
    /// </summary>
    /// <param name="projects">The settings of each project, with the folder each lies in.</param>
    /// <param name="options">How this run is to go.</param>
    /// <returns>What was done, and what there is to say about it.</returns>
    /// <exception cref="ArgumentException">Settings do not say what folder they lie in.</exception>
    public static TranslationResult Run(IReadOnlyList<TranslationSettings> projects, TranslationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return new Translation(PairWithFolders(projects), options).Run();
    }

    /// <summary>
    /// Fetches the sources a translation would read and translates nothing:
    /// those of every project to be translated that say where to download
    /// them and are not on the machine.
    /// </summary>
    /// <param name="projects">The settings of each project that would be translated, with the folder each lies in.</param>
    /// <param name="options">How the run is to go.</param>
    /// <param name="onlyWhereNotTranslated">
    /// Whether a project that has a translation already, its summary beside
    /// it, is passed over: a build then fetches only for a project it could
    /// not compile otherwise.
    /// </param>
    /// <returns>What was fetched, and what there is to say about it; nothing is written but the sources.</returns>
    /// <exception cref="ArgumentException">Settings do not say what folder they lie in.</exception>
    public static TranslationResult Restore(IReadOnlyList<TranslationSettings> projects, TranslationOptions options, bool onlyWhereNotTranslated)
    {
        ArgumentNullException.ThrowIfNull(options);
        return new Translation(PairWithFolders(projects), options).Restore(onlyWhereNotTranslated);
    }

    private static List<(TranslationSettings Settings, string Folder)> PairWithFolders(IReadOnlyList<TranslationSettings> projects)
    {
        ArgumentNullException.ThrowIfNull(projects);
        var paired = new List<(TranslationSettings Settings, string Folder)>(projects.Count);
        foreach (var settings in projects)
        {
            if (settings?.Folder is not { Length: > 0 } folder)
            {
                throw new ArgumentException("Settings to translate say what folder they lie in; Load gives them the folder of their file.", nameof(projects));
            }

            paired.Add((settings, Path.GetFullPath(folder)));
        }

        return paired;
    }

    [GeneratedRegex(@"^[A-Za-z_][A-Za-z0-9_]*$")]
    private static partial Regex Identifier();

    [GeneratedRegex(@"\$\(([A-Za-z_][A-Za-z0-9_.]*)\)")]
    private static partial Regex VariableReference();

    // What the translator wrote to one folder of translated sources: which
    // translator it was, and the hash of each file's text, by the file's
    // name. The version is here and not in every file, so that a new
    // translator changes no file whose translation it does not change.
    private sealed record Manifest(string Translator, IReadOnlyDictionary<string, string> Files);

    // One program unit as it was read.
    private sealed record Unit(string File, SymbolTable Table, UnitSummary Summary)
    {
        public string Name => Summary.Name ?? CSharpEmitter.MainProgramName;
    }

    // A project that takes part in a run: one of the run's, by its settings,
    // or one an assembly brings. The name is the one it goes by, given or
    // taken from its project file; the folder is the one its paths are
    // taken from.
    private sealed class Project(TranslationSettings settings, string name, string baseDirectory)
    {
        public TranslationSettings Settings { get; } = settings;

        public string Name { get; } = name;

        public string Namespace => Settings.Namespace ?? Name;

        public string BaseDirectory { get; } = baseDirectory;

        public IList<SourceSettings> Sources => Settings.Sources;
    }

    // One source with what was read of it.
    private sealed class Source(Project project, SourceSettings settings)
    {
        public Project Project { get; } = project;

        public SourceSettings Settings { get; } = settings;

        // Whether the source was read from its project's summary and not
        // from its files; it is then not translated or written.
        public bool IsSummarized { get; init; }

        // The procedures of a source read from a summary, by the name each
        // is called by; empty for a source that is translated.
        public Dictionary<string, ProcedureSummary> Summarized { get; } = new(StringComparer.Ordinal);

        public string Namespace { get; } = project.Namespace;

        public string ClassName { get; } = settings.Class ?? settings.Name;

        public List<Unit> Units { get; } = [];

        public ProcedureCatalogue Catalogue { get; } = new();

        // The text of each file to write, by its name.
        public Dictionary<string, string> Files { get; } = new(StringComparer.OrdinalIgnoreCase);

        // Each unit as it was emitted, by the name of its file, to be
        // written again once every procedure has its place.
        public Dictionary<string, EmittedUnit> Emitted { get; } = new(StringComparer.OrdinalIgnoreCase);

        // Each method written, by the name it is called by, with its unit.
        public Dictionary<string, (Unit Unit, EmittedMethod Method)> Methods { get; } = new(StringComparer.Ordinal);
    }

    private sealed partial class Translation(IReadOnlyList<(TranslationSettings Settings, string Folder)> given, TranslationOptions options)
    {
        private readonly List<TranslationMessage> _messages = [];

        // The projects of the run, in the order they were given, each by the
        // name it goes by.
        private readonly List<Project> _given = [];
        private readonly List<Source> _sources = [];
        private readonly Dictionary<Project, List<Project>> _references = [];
        private readonly Dictionary<Project, string> _entries = [];
        private readonly Dictionary<string, IncludeFile?> _includes = new(StringComparer.Ordinal);
        private readonly List<string> _written = [];

        // What was written of each project, for the commands it runs.
        private readonly Dictionary<Project, List<string>> _writtenOf = [];
        private readonly List<string> _fetched = [];
        private readonly List<string> _read = [];
        private readonly HashSet<string> _readOnce = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<string> _patterns = [];
        private readonly List<string> _files = [];
        private readonly List<string> _removed = [];
        private readonly List<StackUse> _stackUse = [];
        private readonly List<UnalignedPlace> _unaligned = [];
        private readonly List<WidenedPlace> _widened = [];

        // Whether a restore passes over a project that has a translation
        // already, which a translation never does.
        private bool _onlyWhereNotTranslated;

        private bool HasErrors => _messages.Any(message => message.IsError);

        public TranslationResult Run()
        {
            TakeProjects();
            CheckSettings();
            CheckVariables();
            CheckCommands();
            if (!HasErrors && !options.Offline)
            {
                FetchSources();
            }

            if (!HasErrors)
            {
                Read();
                ReadReferences();
            }

            if (!HasErrors)
            {
                CheckReferencesNamed();
            }

            if (!HasErrors)
            {
                CheckCalls();
                WorkOutReferences();
                WorkOutEffects();
                WorkOutEntries();
            }

            if (!HasErrors)
            {
                Emit();
            }

            if (!HasErrors)
            {
                WorkOutStackUse();
                WriteWithPlaces();
            }

            if (!HasErrors)
            {
                Write();
            }

            if (!HasErrors)
            {
                RunCommands();
            }

            return HasErrors
                ? new TranslationResult(_messages, _written, _removed, 0)
                : new TranslationResult(_messages, _written, _removed, _sources.Where(source => !source.IsSummarized).Sum(source => source.Units.Count))
                {
                    StackUse = _stackUse,
                    WorkingRegions = _workingRegions,
                    UnalignedPlaces = _unaligned,
                    WidenedPlaces = _widened,
                    Fetched = _fetched,
                    Read = _read,
                    Patterns = _patterns,
                    Files = _files,
                };
        }

        // Notes a file read, once and in the order first read.
        private void NoteRead(string path)
        {
            if (_readOnce.Add(path))
            {
                _read.Add(path);
            }
        }

        public TranslationResult Restore(bool onlyWhereNotTranslated)
        {
            _onlyWhereNotTranslated = onlyWhereNotTranslated;
            TakeProjects();
            CheckSettings();
            if (!HasErrors)
            {
                CheckVariables();
            }

            if (!HasErrors)
            {
                FetchSources();
            }

            return new TranslationResult(_messages, [], [], 0) { Fetched = _fetched };
        }

        private void Error(string text, string? file = null, int line = 0) => _messages.Add(new TranslationMessage(true, file, line, text));

        private void Warning(string text, string? file = null, int line = 0) => _messages.Add(new TranslationMessage(false, file, line, text));

        // Whether this run reads a project's sources: those of one it
        // translates, unless a restore passes over it for its translation.
        private bool ReadsSources(Project project) =>
            IsTranslated(project) && !(_onlyWhereNotTranslated && File.Exists(SummaryPathOf(project)));

        private static string FolderOf(Project project) =>
            Path.GetFullPath(Path.Combine(project.BaseDirectory, Expand(project, project.Settings.Output) ?? string.Empty));

        private static string ProjectFileOf(Project project, string folder) => Path.Combine(folder, project.Name + ProjectFileExtension);

        private static bool HasProjectFile(string folder) => Directory.Exists(folder) && Directory.GetFiles(folder, "*" + ProjectFileExtension).Length > 0;

        // Whether the files around a project's translation are written this
        // time: always, where none is there yet, or never. A project file
        // that is there and is not the translator's to write is somebody's,
        // and the project is given sources and nothing else.
        private static bool WillScaffold(Project project) => project.Settings.Scaffold switch
        {
            ScaffoldMode.Always => true,
            ScaffoldMode.Once => FindProjectFile(project, FolderOf(project)) is null,
            _ => false,
        };

        // The project file that compiles a project's translation: the one in
        // its folder, or else the one of its name beside the settings, where
        // a project keeps it whose translation lies elsewhere, under obj/ for
        // one. Beside the settings only the name is taken, since settings
        // that give the project a name of their own may lie beside
        // somebody's other project.
        private static string? FindProjectFile(Project project, string folder)
        {
            if (HasProjectFile(folder))
            {
                return Directory.GetFiles(folder, "*" + ProjectFileExtension)[0];
            }

            var beside = Path.Combine(project.BaseDirectory, project.Name + ProjectFileExtension);
            return File.Exists(beside) ? beside : null;
        }

        // Each project of the run by the name it goes by.
        private void TakeProjects()
        {
            if (given.Count == 0)
            {
                Error("There are no settings of a project to translate.");
            }

            foreach (var (settings, folder) in given)
            {
                if (FindName(settings, folder) is { } name)
                {
                    _given.Add(new Project(settings, name, folder));
                }
            }
        }

        // A project's name: the one its settings give, or else that of the
        // one project file beside them, which is then the project they are
        // the settings of.
        private string? FindName(TranslationSettings settings, string folder)
        {
            if (settings.Name is { } name)
            {
                return name;
            }

            var files = Directory.Exists(folder) ? Directory.GetFiles(folder, "*" + ProjectFileExtension) : [];
            if (files.Length == 1)
            {
                return Path.GetFileNameWithoutExtension(files[0]);
            }

            Error(
                files.Length == 0
                    ? "The settings give the project no name, and no project file lies beside them to take one from; give it a name."
                    : "The settings give the project no name, and several project files lie beside them; give it a name.",
                folder);
            return null;
        }

        private void CheckSettings()
        {
            var classes = new HashSet<string>(StringComparer.Ordinal);
            var projects = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var project in _given)
            {
                var name = project.Name;
                var space = project.Namespace;
                if (name.Length == 0 || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                {
                    Error($"A project has to have a name that can name a file; '{name}' cannot.");
                    continue;
                }

                if (!projects.Add(name))
                {
                    Error($"Two projects are named {name}.");
                }

                if (string.IsNullOrWhiteSpace(project.Settings.Output))
                {
                    Error($"The settings of the project {name} name no output: say where it is written, \"output\": \".\" for the folder the settings file is in.");
                }

                if (project.Settings.Runtime is { OmitPackageVersion: true, Package: not null } runtime)
                {
                    Error($"The settings of the project {name} give the runtime's package the version {runtime.Package} and say to write none; it is one or the other.");
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
            }
        }

        // Puts in the value of each variable a path of a project's names as
        // $(Name): from its local settings file, then the environment, then
        // its settings. A name with no value is left as it stands;
        // CheckVariables says so.
        [return: System.Diagnostics.CodeAnalysis.NotNullIfNotNull(nameof(path))]
        private static string? Expand(Project project, string? path) =>
            path is null ? null : VariableReference().Replace(path, match => ResolveVariable(project, match.Groups[1].Value) ?? match.Value);

        private static string? ResolveVariable(Project project, string name)
        {
            if (project.Settings.LocalVariables.TryGetValue(name, out var local))
            {
                return local;
            }

            return Environment.GetEnvironmentVariable(name) ?? (project.Settings.Variables.TryGetValue(name, out var given) ? given.Path : null);
        }

        // The variables a project's paths name that this run needs, so that
        // one with no value is reported once and before anything is read. A
        // project read from its summary needs its folder and none of its
        // sources.
        private HashSet<string> VariablesNamed(Project project)
        {
            var paths = new[] { project.Settings.Output, project.Settings.Runtime.Project }
                .Concat(ReadsSources(project) ? project.Sources.SelectMany(source => source.Files.Concat(source.Includes)) : [])
                .OfType<string>();
            return new HashSet<string>(
                paths.SelectMany(path => VariableReference().Matches(path).Select(match => match.Groups[1].Value)),
                StringComparer.OrdinalIgnoreCase);
        }

        private void CheckVariables()
        {
            foreach (var project in _given)
            {
                foreach (var name in VariablesNamed(project).Where(name => ResolveVariable(project, name) is null))
                {
                    var whose = _given.Count == 1 ? string.Empty : $" in the settings of the project {project.Name}";
                    Error($"The variable {name} has no value{whose}. Give it one in the local settings file, in the environment, or under variables in the settings.");
                }
            }
        }

        // A variable that names a folder to download is fetched when the
        // folder is not there, and only for the path the settings give: a
        // value from the local settings file or the environment is somebody's
        // own folder, used as it is. Only what this run reads is fetched: the
        // sources of a project read from its summary are not. Two projects
        // that name one folder fetch it once.
        private void FetchSources()
        {
            var fetcher = new SourceFetcher(options);
            foreach (var project in _given)
            {
                var named = VariablesNamed(project);
                foreach (var (name, variable) in project.Settings.Variables.Where(pair => pair.Value.Url is not null && named.Contains(pair.Key)))
                {
                    if (variable.Sha256 is null)
                    {
                        Error($"The variable {name} says where to download from and not what the archive's SHA-256 hash is; it is not downloaded without one.");
                        continue;
                    }

                    if (ResolveVariable(project, name) != variable.Path)
                    {
                        continue;
                    }

                    var folder = Path.GetFullPath(Path.Combine(project.BaseDirectory, variable.Path));
                    if (Directory.Exists(folder))
                    {
                        continue;
                    }

                    if (fetcher.Fill(variable, folder) is { } problem)
                    {
                        Error($"The variable {name}: {problem}");
                    }
                    else
                    {
                        _fetched.Add(folder);
                    }
                }
            }
        }

        private void CheckCommands()
        {
            foreach (var project in _given)
            {
                var commands = project.Settings.OnSuccess;
                for (var index = 0; index < commands.Count; index++)
                {
                    if (commands[index].Run.Count == 0 || string.IsNullOrWhiteSpace(commands[index].Run[0]))
                    {
                        Error(string.Create(System.Globalization.CultureInfo.InvariantCulture, $"Command {index + 1} of onSuccess of the project {project.Name} names no program to run."));
                    }
                }
            }
        }

        private void Read()
        {
            foreach (var project in _given)
            {
                if (!IsTranslated(project))
                {
                    ReadSummary(project);
                    continue;
                }

                // Offline, a project whose sources are not here is compiled
                // as it was translated last, where there is a translation.
                if (options.Offline && !HasSources(project))
                {
                    if (!ReadSummary(project))
                    {
                        Error($"The sources of the project {project.Name} are not on this machine, and it has no translation to read instead; restore them first.");
                    }

                    continue;
                }

                _projects.Add(project);
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

        // Whether any file a project's sources name is on the machine.
        private static bool HasSources(Project project) => project.Sources
            .SelectMany(source => source.Files)
            .Select(entry => Path.GetFullPath(Path.Combine(project.BaseDirectory, Expand(project, entry))))
            .Any(path => Path.GetDirectoryName(path) is { } folder && Directory.Exists(folder) && Directory.GetFiles(folder, Path.GetFileName(path)).Length > 0);

        private List<string> FilesOf(SourceSettings source, Project project)
        {
            var files = new List<string>();
            foreach (var entry in source.Files)
            {
                var path = Path.GetFullPath(Path.Combine(project.BaseDirectory, Expand(project, entry)));
                if (!_patterns.Contains(path, StringComparer.OrdinalIgnoreCase))
                {
                    _patterns.Add(path);
                }

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
            NoteRead(file);
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
                .Select(folder => Path.GetFullPath(Path.Combine(source.Project.BaseDirectory, Expand(source.Project, folder))))
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
                    NoteRead(path);
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

        // The names a source calls, or passes as procedures, that it does
        // not define itself.
        private static IEnumerable<string> ForeignNames(Source source) => source.Units
            .SelectMany(unit => unit.Summary.Calls.SelectMany(call => call.Arguments
                .Where(argument => argument.Kind == ActualArgumentKind.Procedure && argument.Name is not null)
                .Select(argument => argument.Name!)
                .Prepend(call.Name)))
            .Where(name => source.Catalogue.Find(name) is null)
            .Distinct(StringComparer.Ordinal);

        // The other sources a source reaches: for each name it does not
        // define, the first that does, in the order it looks.
        private List<Source> Used(Source source)
        {
            var owners = ForeignNames(source)
                .Select(name => Visible(source).FirstOrDefault(other => other.Catalogue.Find(name) is not null))
                .Where(owner => owner is not null)
                .ToHashSet();
            return [.. Visible(source).Where(owners.Contains)];
        }

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
                            Warning($"{unit.Name} calls {missing}, which no source of the translation defines.", unit.File);
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
            foreach (var project in _projects)
            {
                // A project read from its summary refers to what it did
                // when it was translated.
                if (_summaries.TryGetValue(project, out var summary))
                {
                    _references[project] = [.. _projects.Where(other => summary.References.Contains(other.Name, StringComparer.Ordinal))];
                    continue;
                }

                var used = _sources
                    .Where(source => source.Project == project)
                    .SelectMany(Used)
                    .Where(owner => owner.Project != project)
                    .Select(owner => owner.Project)
                    .Distinct()
                    .ToList();

                if (project.Settings.References is { } named)
                {
                    // A project an assembly brings is referred to by whoever
                    // passed the assembly, a build by its own references.
                    foreach (var missing in used.Where(other => !named.Contains(other.Name, StringComparer.Ordinal) && !_external.Contains(other)))
                    {
                        Error($"The project {project.Name} calls into {missing.Name} and does not refer to it.");
                    }

                    _references[project] = [.. _projects.Where(other => named.Contains(other.Name, StringComparer.Ordinal) || (used.Contains(other) && _external.Contains(other)))];
                }
                else
                {
                    _references[project] = [.. _projects.Where(used.Contains)];
                }
            }

            // Assemblies cannot refer to each other in a circle.
            foreach (var project in _projects)
            {
                if (Reaches(project, project, []))
                {
                    Error($"The project {project.Name} refers to itself by way of the projects it refers to; sources that call each other both ways belong in one project.");
                    break;
                }
            }
        }

        private bool Reaches(Project from, Project target, HashSet<Project> seen) =>
            _references.GetValueOrDefault(from, []).Any(next => next == target || (seen.Add(next) && Reaches(next, target, seen)));

        // An executable starts with one main program. That is asked only
        // of a project that is being made: in one that exists no Main is
        // written, so there is nothing to choose.
        private void WorkOutEntries()
        {
            foreach (var project in _projects.Where(IsTranslated))
            {
                var mains = _sources
                    .Where(source => source.Project == project)
                    .SelectMany(source => source.Units.Where(unit => unit.Summary.Kind == UnitKind.MainProgram).Select(unit => (Source: source, Unit: unit)))
                    .ToList();

                var kind = project.Settings.Kind ?? (mains.Count > 0 ? ProjectKind.Executable : ProjectKind.Library);
                if (kind != ProjectKind.Executable || !WillScaffold(project))
                {
                    continue;
                }

                if (mains.Count == 0)
                {
                    Error($"The project {project.Name} is to be an executable and has no main program.");
                }
                else if (project.Settings.Entry is { } entry)
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
            foreach (var source in _sources.Where(source => !source.IsSummarized))
            {
                var options = new EmitOptions(source.Namespace, source.ClassName)
                {
                    References = [.. Used(source).Select(other => new LibraryReference(other.Namespace, other.ClassName, other.Catalogue.Find))],
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

                        var emitted = emitter.EmitUnit(unit.Table, unit.File);
                        source.Files[name] = emitted.Text;
                        source.Emitted[name] = emitted;
                        foreach (var method in emitted.Methods)
                        {
                            source.Methods[method.Name] = (unit, method);
                        }

                        _unaligned.AddRange(emitted.UnalignedVariables.Select(variable => new UnalignedPlace(
                            source.Project.Name, source.Settings.Name, unit.Name, unit.File, variable.Name, variable.Offset, variable.Alignment, variable.Line)));
                        _widened.AddRange(emitted.WidenedArguments.Select(argument => new WidenedPlace(
                            source.Project.Name, source.Settings.Name, unit.Name, unit.File, argument.Variable, argument.Procedure, argument.Argument, argument.Line)));
                    }
                    catch (Exception exception) when (IsOfTheSource(exception))
                    {
                        Error(exception.Message, unit.File);
                    }
                }

                source.Files[FileName(source.ClassName)] = emitter.EmitClass();
            }
        }

        // Each unit again with its procedures' buffers where a context says,
        // now that every procedure has its place, and each class with what
        // it asks of its context for them.
        private void WriteWithPlaces()
        {
            foreach (var source in _sources.Where(source => !source.IsSummarized))
            {
                // In the order of the files' names, so that a constant has
                // the same place however the units came to be read.
                var text = source.Project.Settings.InlineTextConstants ? null : new TextBlock();
                foreach (var (name, emitted) in source.Emitted.OrderBy(unit => unit.Key, StringComparer.Ordinal))
                {
                    source.Files[name] = CSharpEmitter.WriteWithPlaces(
                        emitted,
                        procedure => _places.TryGetValue((source, procedure), out var place) ? place : null,
                        text,
                        source.Project.Settings.PlainArrayLoops);
                }

                var region = _workingRegions.Single(region => region.Project == source.Project.Name);
                var options = new EmitOptions(source.Namespace, source.ClassName)
                {
                    References = [.. Used(source).Select(other => new LibraryReference(other.Namespace, other.ClassName, other.Catalogue.Find))],
                };
                var emitter = new CSharpEmitter(options, source.Catalogue.Find);
                source.Files[FileName(source.ClassName)] = emitter.EmitClass(new LibraryRegion(
                    region.Project,
                    region.OwnBytes,
                    region.ReachBytes,
                    [.. _references.GetValueOrDefault(source.Project, []).Select(project => project.Name)]));

                // The constants in a file of their own beside the class's,
                // named as no unit can be, a FORTRAN name having no dot.
                if (text is { IsEmpty: false })
                {
                    source.Files[FileName(source.ClassName + ".Text")] = emitter.EmitTextBlock(text);
                }
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
            var translated = _projects.Where(IsTranslated).ToList();
            var scaffolded = translated.Where(WillScaffold).ToHashSet();

            foreach (var project in translated)
            {
                var first = _written.Count;
                var folder = FolderOf(project);
                foreach (var source in _sources.Where(source => source.Project == project))
                {
                    var generated = Path.Combine(folder, GeneratedFolder, source.Settings.Name);
                    Directory.CreateDirectory(generated);

                    // A file is written when what the translator makes of it
                    // differs from what it made last time, or the file is
                    // gone; a file a later step changed is otherwise kept.
                    var manifestPath = Path.Combine(generated, ManifestName);
                    var before = IsForced(project) ? [] : ReadManifest(manifestPath);
                    var after = new SortedDictionary<string, string>(StringComparer.Ordinal);
                    foreach (var (name, text) in source.Files)
                    {
                        var path = Path.Combine(generated, name);
                        _files.Add(path);
                        var hash = Hash(text);
                        after[name] = hash;
                        if (!File.Exists(path) || before.GetValueOrDefault(name) != hash)
                        {
                            File.WriteAllText(path, text);
                            _written.Add(path);
                        }
                    }

                    var manifest = JsonSerializer.Serialize(new Manifest("Fortrania " + OwnVersion(), after), TranslationJson.Default).ReplaceLineEndings("\n") + "\n";
                    if (!File.Exists(manifestPath) || File.ReadAllText(manifestPath) != manifest)
                    {
                        File.WriteAllText(manifestPath, manifest);
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

                // What the projects that call into this one need of it, for
                // a translation that leaves this one out.
                Directory.CreateDirectory(Path.Combine(folder, GeneratedFolder));
                WriteIfChanged(SummaryPathOf(project), SummarizeProject(project).ToJson());
                _files.Add(SummaryPathOf(project));

                if (scaffolded.Contains(project))
                {
                    WriteIfChanged(ProjectFileOf(project, folder), ProjectFile(project, folder));
                    if (_entries.TryGetValue(project, out var entry))
                    {
                        WriteIfChanged(Path.Combine(folder, "Program.cs"), Program(project, entry));
                        _files.Add(Path.Combine(folder, "Program.cs"));
                    }
                }
                else
                {
                    ReportMissingReferences(project, folder);
                }

                _writtenOf[project] = _written[first..];
            }
        }

        private static Dictionary<string, string> ReadManifest(string path)
        {
            try
            {
                return File.Exists(path)
                    ? new Dictionary<string, string>(JsonSerializer.Deserialize<Manifest>(File.ReadAllText(path), TranslationJson.Default)?.Files ?? new Dictionary<string, string>(), StringComparer.Ordinal)
                    : [];
            }
            catch (JsonException)
            {
                // A manifest that cannot be read only makes every file be
                // written again.
                return [];
            }
        }

        private static string Hash(string text) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));

        // The commands of each project translated, once all of the run is
        // written, the projects in the order given; the first that fails
        // ends them all.
        private void RunCommands()
        {
            foreach (var project in _given.Where(_writtenOf.ContainsKey))
            {
                if (!RunCommands(project, _writtenOf[project]))
                {
                    return;
                }
            }
        }

        private bool RunCommands(Project project, List<string> written)
        {
            if (project.Settings.OnSuccess.Count == 0)
            {
                return true;
            }

            var list = Path.Combine(Path.GetTempPath(), "fortrania-written-" + Guid.NewGuid().ToString("N") + ".txt");
            File.WriteAllLines(list, written);
            try
            {
                return project.Settings.OnSuccess.All(command => Run(project, command, list));
            }
            finally
            {
                File.Delete(list);
            }
        }

        private bool Run(Project project, CommandSettings command, string list)
        {
            var start = new ProcessStartInfo(command.Run[0])
            {
                WorkingDirectory = project.BaseDirectory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            foreach (var argument in command.Run.Skip(1))
            {
                start.ArgumentList.Add(argument);
            }

            start.Environment["FORTRANIA_SETTINGS"] = project.BaseDirectory;
            start.Environment["FORTRANIA_OUTPUT"] = FolderOf(project);
            start.Environment["FORTRANIA_WRITTEN"] = list;
            var shown = string.Join(' ', command.Run);

            try
            {
                using var process = Process.Start(start)!;
                var output = process.StandardOutput.ReadToEndAsync();
                var errors = process.StandardError.ReadToEndAsync();
                process.WaitForExit();
                if (process.ExitCode == 0)
                {
                    return true;
                }

                var said = (errors.Result + output.Result).Trim();
                Error(string.Create(
                    System.Globalization.CultureInfo.InvariantCulture,
                    $"The command '{shown}' ended with status {process.ExitCode}.{(said.Length == 0 ? string.Empty : " It said: " + said)}"));
            }
            catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or InvalidOperationException)
            {
                Error($"The command '{shown}' could not be started: {exception.Message}");
            }

            return false;
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
        private void ReportMissingReferences(Project project, string folder)
        {
            if (FindProjectFile(project, folder) is not { } file)
            {
                Warning($"The project {project.Name} is to have a project file of somebody's own, and neither its folder nor the settings' has one; make one, or say \"scaffold\": \"once\".", folder);
                return;
            }

            var text = File.ReadAllText(file);
            foreach (var reference in _references[project].Where(reference => !_external.Contains(reference)))
            {
                if (!text.Contains(reference.Name + ProjectFileExtension, StringComparison.OrdinalIgnoreCase))
                {
                    Warning($"The project calls into {reference.Name} and its project file does not refer to it.", file);
                }
            }
        }

        private string ProjectFile(Project project, string folder)
        {
            string Relative(string path) => Path.GetRelativePath(folder, path).Replace('\\', '/');

            var settings = project.Settings;
            string runtime;
            if (Expand(project, settings.Runtime.Project) is { } runtimeProject)
            {
                runtime = $"    <ProjectReference Include=\"{Relative(Path.GetFullPath(Path.Combine(project.BaseDirectory, runtimeProject)))}\" />";
            }
            else if (settings.Runtime.OmitPackageVersion)
            {
                runtime = "    <PackageReference Include=\"Dracocephalum.Fortrania.Runtime\" />";
            }
            else
            {
                runtime = $"    <PackageReference Include=\"Dracocephalum.Fortrania.Runtime\" Version=\"{settings.Runtime.Package ?? OwnVersion()}\" />";
            }

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
                $"    <RootNamespace>{project.Namespace}</RootNamespace>",
                "    <Nullable>enable</Nullable>",
                "    <AllowUnsafeBlocks>true</AllowUnsafeBlocks>",
            ]);

            lines.AddRange(settings.Properties.Select(property => $"    <{property.Key}>{System.Security.SecurityElement.Escape(property.Value)}</{property.Key}>"));
            lines.AddRange(
            [
                "  </PropertyGroup>",
                string.Empty,
                "  <ItemGroup>",
                runtime,
            ]);

            // A project an assembly brings has no folder here; it is
            // referred to by whoever passed the assembly.
            lines.AddRange(_references[project].Where(reference => !_external.Contains(reference)).Select(reference =>
                $"    <ProjectReference Include=\"{Relative(ProjectFileOf(reference, FolderOf(reference)))}\" />"));
            lines.AddRange(["  </ItemGroup>", string.Empty, "</Project>", string.Empty]);
            return string.Join('\n', lines);
        }

        private static string Program(Project project, string entry) =>
            $$"""
            using Dracocephalum.Fortrania.Runtime;

            namespace {{project.Namespace}};

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
