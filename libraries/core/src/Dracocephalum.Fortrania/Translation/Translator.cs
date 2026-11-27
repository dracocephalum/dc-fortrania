using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using Dracocephalum.Fortrania.Analysis;
using Dracocephalum.Fortrania.Emit;
using Dracocephalum.Fortrania.Plugins;
using Dracocephalum.Fortrania.Syntax;

namespace Dracocephalum.Fortrania.Translation;

/// <summary>
/// Does a whole translation as its settings describe it: reads the FORTRAN
/// of every source, checks it, and writes the C#, and where the options
/// say so a project around it. Either all of it is written or none: with an
/// error in the settings or in any source, nothing on disk is touched.
/// </summary>
public static partial class Translator
{
    /// <summary>The folder of a settings file's output that its translated sources are written to, a folder per source inside it.</summary>
    public const string GeneratedFolder = "Generated";

    /// <summary>What the name of a translated file ends in.</summary>
    public const string GeneratedExtension = ".g.cs";

    /// <summary>
    /// The file in each settings file's folder of translated sources that
    /// records what the translator wrote there, so that a file a later step
    /// has changed is left alone until what the translator would write is
    /// different.
    /// </summary>
    public const string ManifestName = ".fortrania-manifest.json";

    private const string ProjectFileExtension = ".csproj";

    /// <summary>Translates one project.</summary>
    /// <param name="project">The project, and the settings of what it is made of with the folder each lies in.</param>
    /// <returns>What was done, and what there is to say about it.</returns>
    /// <exception cref="ArgumentException">Settings do not say what folder they lie in.</exception>
    public static TranslationResult Run(TranslationProject project) => Run([project], new TranslationOptions());

    /// <summary>Translates one project, in the way the options say.</summary>
    /// <param name="project">The project, and the settings of what it is made of with the folder each lies in.</param>
    /// <param name="options">How this run is to go.</param>
    /// <returns>What was done, and what there is to say about it.</returns>
    /// <exception cref="ArgumentException">Settings do not say what folder they lie in.</exception>
    public static TranslationResult Run(TranslationProject project, TranslationOptions options) => Run([project], options);

    /// <summary>
    /// Translates several projects in one run, in the way the options say.
    /// They call into each other as they would apart: one translated with
    /// those it calls into is translated as it would be against their
    /// summaries.
    /// </summary>
    /// <param name="projects">Each project, and the settings of what it is made of with the folder each lies in.</param>
    /// <param name="options">How this run is to go.</param>
    /// <returns>What was done, and what there is to say about it.</returns>
    /// <exception cref="ArgumentException">Settings do not say what folder they lie in.</exception>
    public static TranslationResult Run(IReadOnlyList<TranslationProject> projects, TranslationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return new Translation(CheckFolders(projects), options).Run();
    }

    private static IReadOnlyList<TranslationProject> CheckFolders(IReadOnlyList<TranslationProject> projects)
    {
        ArgumentNullException.ThrowIfNull(projects);
        foreach (var project in projects)
        {
            ArgumentNullException.ThrowIfNull(project, nameof(projects));
            if (project.Settings.Any(settings => settings?.Folder is not { Length: > 0 }))
            {
                throw new ArgumentException("Settings to translate say what folder they lie in; Load gives them the folder of their file.", nameof(projects));
            }
        }

        return projects;
    }

    [GeneratedRegex(@"^[A-Za-z_][A-Za-z0-9_]*$")]
    private static partial Regex Identifier();

    [GeneratedRegex(@"\$\(([A-Za-z_][A-Za-z0-9_.]*)\)")]
    private static partial Regex VariableReference();

    // What the translator wrote to one settings file's folder of translated
    // sources: for each source the names of its files. It is what says a
    // translation is all there, to be compiled where its sources are not.
    private sealed record Manifest(IReadOnlyDictionary<string, IReadOnlyList<string>> Sources);

    // One program unit as it was read.
    private sealed record Unit(string File, SymbolTable Table, UnitSummary Summary)
    {
        public string Name => Summary.Name ?? CSharpEmitter.MainProgramName;

        // The platform the unit stands in for its base on, where it was read
        // from a variant's files; nothing for one of the source's own.
        public PlatformVariant? Variant { get; init; }

        // The procedures the unit is called by: its entry points, or the
        // unit itself for a main program, which has none.
        public IReadOnlyList<string> Procedures => Summary.Kind == UnitKind.MainProgram ? [Name] : [.. Summary.EntryPoints.Select(entry => entry.Name)];
    }

    // One settings file of a project: the folder its paths are taken from,
    // and the namespace its sources are translated to.
    private sealed class Part(TranslationSettings settings, string folder, string @namespace)
    {
        public TranslationSettings Settings { get; } = settings;

        public string Folder { get; } = folder;

        public string Namespace { get; } = @namespace;
    }

    // A project that takes part in a run: one of the run's, or one a
    // reference brings. Its summary is where it is kept, or for one an
    // assembly brings the assembly; its folder, where its project file lies,
    // is that of its summary, and none for one an assembly brings.
    private sealed class Project(string name, IReadOnlyList<Part> parts, string summaryPath, string? folder)
    {
        public string Name { get; } = name;

        public IReadOnlyList<Part> Parts { get; } = parts;

        public string SummaryPath { get; } = summaryPath;

        public string? Folder { get; } = folder;

        // The namespace of its project file and its program: the options',
        // or the first settings file's, or its name.
        public string Namespace => Parts.Count > 0 ? Parts[0].Namespace : Name;

        // Every source of every settings file, in the order given.
        public IEnumerable<(Part Part, SourceSettings Settings)> Sources => Parts.SelectMany(part => part.Settings.Sources.Select(source => (part, source)));
    }

    // One source with what was read of it.
    private sealed class Source(Project project, Part part, SourceSettings settings)
    {
        public Project Project { get; } = project;

        public Part Part { get; } = part;

        public SourceSettings Settings { get; } = settings;

        // Whether the source was read from its project's summary and not
        // from its files; it is then not translated or written.
        public bool IsSummarized { get; init; }

        // The procedures of a source read from a summary, by the name each
        // is called by; empty for a source that is translated.
        public Dictionary<string, ProcedureSummary> Summarized { get; } = new(StringComparer.Ordinal);

        // The namespace its class is in; one read from a summary has the
        // one the summary gives.
        public string Namespace { get; init; } = part.Namespace;

        public string ClassName { get; } = settings.Class ?? settings.Name;

        // Every unit read, the variants among them.
        public List<Unit> Units { get; } = [];

        // The variants of each unit that has them, by the unit's name, in
        // the order its library tries them, the base not among them.
        public Dictionary<string, List<Unit>> Variants { get; } = new(StringComparer.Ordinal);

        // The platform a unit is written for: the variant it is, the base
        // for a unit of the source's own that has variants, and nothing for
        // one written once.
        public PlatformVariant? PlatformOf(Unit unit) => unit.Variant ?? (Variants.ContainsKey(unit.Name) ? PlatformVariant.Base : null);

        public ProcedureCatalogue Catalogue { get; } = new();

        // The text of each file to write, by its name.
        public Dictionary<string, string> Files { get; } = new(StringComparer.OrdinalIgnoreCase);

        // Each unit as it was emitted, by the name of its file, to be
        // written again once every procedure has its place.
        public Dictionary<string, EmittedUnit> Emitted { get; } = new(StringComparer.OrdinalIgnoreCase);

        // Each method written, by the name it is called by, with its unit.
        public Dictionary<string, (Unit Unit, EmittedMethod Method)> Methods { get; } = new(StringComparer.Ordinal);

        // The unit each file translates, by the file's name; a file of the
        // library's own translates none.
        public Dictionary<string, Unit> UnitOfFile { get; } = new(StringComparer.OrdinalIgnoreCase);
    }

    private sealed partial class Translation(IReadOnlyList<TranslationProject> given, TranslationOptions options)
    {
        private readonly List<TranslationMessage> _messages = [];

        // The projects of the run, in the order they were given.
        private readonly List<Project> _given = [];
        private readonly List<Source> _sources = [];
        private readonly Dictionary<Project, List<Project>> _references = [];
        private readonly Dictionary<Project, string> _entries = [];
        private readonly Dictionary<string, IncludeFile?> _includes = new(StringComparer.Ordinal);
        private readonly List<string> _written = [];
        private readonly List<string> _fetched = [];
        private readonly List<string> _read = [];
        private readonly HashSet<string> _readOnce = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<string> _patterns = [];
        private readonly List<string> _files = [];
        private readonly List<string> _removed = [];
        private readonly List<StackUse> _stackUse = [];
        private readonly List<UnalignedPlace> _unaligned = [];
        private readonly List<WidenedPlace> _widened = [];

        private readonly List<ITranslationPlugin> _plugins = [];
        private TranslationContext? _context;

        private bool HasErrors => _messages.Any(message => message.IsError);

        public TranslationResult Run()
        {
            _context = new TranslationContext(given, _messages);
            TakeProjects();
            CheckSettings();
            CheckVariables();
            if (!HasErrors && options.Fetch != FetchPolicy.Never)
            {
                FetchSources();
            }

            if (!HasErrors)
            {
                LoadPlugins();
            }

            Call(plugin => plugin.BeforeRead, nameof(ITranslationPlugin.BeforeRead));
            if (!HasErrors)
            {
                Read();
                ReadReferences();
            }

            Call(plugin => plugin.AfterRead, nameof(ITranslationPlugin.AfterRead));
            Call(plugin => plugin.BeforeAnalysis, nameof(ITranslationPlugin.BeforeAnalysis));
            if (!HasErrors)
            {
                CheckCalls();
                WorkOutReferences();
                WorkOutEffects();
                WorkOutEntries();
            }

            Call(plugin => plugin.AfterAnalysis, nameof(ITranslationPlugin.AfterAnalysis));
            if (!HasErrors)
            {
                Emit();
            }

            if (!HasErrors)
            {
                WorkOutStackUse();
                WriteWithPlaces();
            }

            if (!HasErrors && _plugins.Count > 0)
            {
                _context.GeneratedFiles = [.. GatherGeneratedFiles()];
                Call(plugin => plugin.BeforeWrite, nameof(ITranslationPlugin.BeforeWrite));
                TakeGeneratedFiles();
            }

            if (!HasErrors)
            {
                Write();
            }

            if (!HasErrors && _plugins.Count > 0)
            {
                _context.Result = Result();
                Call(plugin => plugin.AfterWrite, nameof(ITranslationPlugin.AfterWrite));
            }

            return Result();
        }

        private TranslationResult Result() => HasErrors
            ? new TranslationResult([.. _messages], _written, _removed, 0)
            : new TranslationResult([.. _messages], _written, _removed, _sources.Where(source => !source.IsSummarized).Sum(source => source.Units.Count))
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

        // The plugins of the run: those the options give, then those of the
        // assemblies the options name and then the settings, each assembly
        // once, in the order each plugin gives. What a plugin is made from
        // is among what the run read, so that a build tells when one changed:
        // its assembly, and what says where its dependencies are.
        private void LoadPlugins()
        {
            var paths = (options.PluginAssemblies ?? []).Select(path => Path.GetFullPath(path))
                .Concat(_given.SelectMany(project => project.Parts).SelectMany(part => part.Settings.Plugins.Select(path => Path.GetFullPath(Path.Combine(part.Folder, Expand(part, path) ?? path)))))
                .Distinct(StringComparer.OrdinalIgnoreCase);

            var found = new List<ITranslationPlugin>(options.Plugins);
            foreach (var path in paths)
            {
                foreach (var read in new[] { path, Path.ChangeExtension(path, ".deps.json") }.Where(File.Exists))
                {
                    NoteRead(read);
                }

                if (PluginLoader.Load(path, found) is { } problem)
                {
                    Error(problem);
                }
            }

            // A plugin is code the run knows nothing of, its order too.
            try
            {
                _plugins.AddRange(found.OrderBy(plugin => plugin.Order));
            }
            catch (Exception exception)
            {
                Error($"The order of a plugin cannot be read: {exception.Message}");
            }
        }

        // Calls each plugin at one point of the run, one after another, and
        // none at a point the run does not reach. A plugin is code the run
        // knows nothing of, so whatever it throws is caught here and becomes
        // an error naming it, which stops the run as one it reported would.
        private void Call(Func<ITranslationPlugin, Action<TranslationContext>> point, string name)
        {
            if (_context is null)
            {
                return;
            }

            foreach (var plugin in _plugins)
            {
                if (HasErrors)
                {
                    return;
                }

                try
                {
                    point(plugin)(_context);
                }
                catch (Exception exception)
                {
                    Error($"The plugin {plugin.GetType().FullName} failed in {name}: {exception.Message}");
                }
            }
        }

        // Every file of the translation as a plugin sees it, in the order
        // they are written.
        private IEnumerable<GeneratedFile> GatherGeneratedFiles() =>
            from project in _projects.Where(IsTranslated)
            from part in project.Parts
            from source in _sources.Where(source => source.Part == part)
            from file in source.Files
            let unit = source.UnitOfFile.GetValueOrDefault(file.Key)
            select new GeneratedFile(
                project.Name,
                source.Settings.Name,
                GetGeneratedPath(part, source, file.Key),
                unit?.Name,
                unit?.File,
                file.Value);

        // What the plugins made of each file is what is written, and what
        // its hash is taken of.
        private void TakeGeneratedFiles()
        {
            if (HasErrors || _context is null)
            {
                return;
            }

            var byPath = _context.GeneratedFiles.ToDictionary(file => file.Path, StringComparer.OrdinalIgnoreCase);
            foreach (var project in _projects.Where(IsTranslated))
            {
                foreach (var part in project.Parts)
                {
                    foreach (var source in _sources.Where(source => source.Part == part))
                    {
                        foreach (var name in source.Files.Keys.ToList())
                        {
                            source.Files[name] = byPath[GetGeneratedPath(part, source, name)].Text;
                        }
                    }
                }
            }
        }

        // Where a file of a source's translation is written.
        private static string GetGeneratedPath(Part part, Source source, string name) =>
            Path.Combine(GeneratedFolderOf(part), source.Settings.Name, name);

        // Notes a file read, once and in the order first read.
        private void NoteRead(string path)
        {
            if (_readOnce.Add(path))
            {
                _read.Add(path);
            }
        }

        private void Error(string text, string? file = null, int line = 0) => _messages.Add(new TranslationMessage(true, file, line, text));

        private void Warning(string text, string? file = null, int line = 0) => _messages.Add(new TranslationMessage(false, file, line, text));

        // Whether what a project's translation wrote is all there: its
        // summary, and in each of its settings' folders a manifest that can
        // be read and every file it lists, which for settings with no
        // sources is none. Its summary alone, which is committed with the
        // project, is not a translation to compile.
        private static bool IsAllThere(Project project) =>
            File.Exists(project.SummaryPath)
            && project.Parts.All(part => ReadManifest(ManifestPathOf(part)) is { } sources
                && sources.All(source => source.Value.All(name => File.Exists(Path.Combine(GeneratedFolderOf(part), source.Key, name)))));

        // The folder a settings file's sources are translated to.
        private static string OutputOf(Part part) =>
            Path.GetFullPath(Path.Combine(part.Folder, Expand(part, part.Settings.Output) ?? string.Empty));

        private static string GeneratedFolderOf(Part part) => Path.Combine(OutputOf(part), GeneratedFolder);

        private static string ManifestPathOf(Part part) => Path.Combine(GeneratedFolderOf(part), ManifestName);

        private static string ProjectFileOf(Project project, string folder) => Path.Combine(folder, project.Name + ProjectFileExtension);

        // Whether the files around a project are written this time: when the
        // options say to scaffold and its folder has no project file yet. A
        // project file that is there is somebody's, and the project is given
        // sources and nothing else.
        private bool WillScaffold(Project project) => options.Scaffold && FindProjectFile(project) is null;

        // The project file in a project's folder; none for one an assembly
        // brings, which has no folder here.
        private static string? FindProjectFile(Project project) =>
            project.Folder is { } folder && Directory.Exists(folder) && Directory.GetFiles(folder, "*" + ProjectFileExtension) is [var first, ..]
                ? first
                : null;

        // Each project of the run, with its settings files.
        private void TakeProjects()
        {
            if (given.Count == 0)
            {
                Error("There is no project to translate.");
            }

            foreach (var project in given)
            {
                if (string.IsNullOrWhiteSpace(project.Name))
                {
                    Error("The project to translate has no name; give it the name of its assembly.");
                    continue;
                }

                var name = project.Name;
                if (project.Settings.Count == 0)
                {
                    Error($"The project {name} has no settings to translate.");
                    continue;
                }

                var parts = project.Settings
                    .Select(settings => new Part(settings, Path.GetFullPath(settings.Folder ?? string.Empty), options.Namespace ?? settings.Namespace ?? name))
                    .ToList();
                var summary = project.Summary is { } given ? Path.GetFullPath(given) : Path.Combine(parts[0].Folder, ProjectSummary.GetFileName(name));
                _given.Add(new Project(name, parts, summary, Path.GetDirectoryName(summary)));
            }
        }

        private void CheckSettings()
        {
            var classes = new HashSet<string>(StringComparer.Ordinal);
            var projects = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var outputs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (options.Runtime is { OmitPackageVersion: true, Package: not null } runtime)
            {
                Error($"The options give the runtime's package the version {runtime.Package} and say to write none; it is one or the other.");
            }

            foreach (var project in _given)
            {
                var name = project.Name;
                if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                {
                    Error($"A project has to have a name that can name a file; '{name}' cannot.");
                    continue;
                }

                if (!projects.Add(name))
                {
                    Error($"Two projects are named {name}.");
                }

                if (!project.Sources.Any())
                {
                    Error($"The project {name} has no sources.");
                }

                foreach (var part in project.Parts)
                {
                    var space = part.Namespace;
                    if (string.IsNullOrWhiteSpace(part.Settings.Output))
                    {
                        Error($"Settings of the project {name} name no output: say where they are written, \"output\": \".\" for the folder the settings file is in.", part.Folder);
                    }
                    else if (!outputs.TryAdd(OutputOf(part), name))
                    {
                        // Each keeps a manifest of its own there.
                        Error($"Two settings files, of {outputs[OutputOf(part)]} and of {name}, are both written to {OutputOf(part)}; give each an output of its own.");
                    }

                    var parts = space.Split('.');
                    if (parts.Any(item => !Identifier().IsMatch(item)))
                    {
                        Error($"The namespace {space} of the project {name} is not a namespace; give it one.");
                    }

                    foreach (var source in part.Settings.Sources)
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

                        foreach (var variant in source.Variants)
                        {
                            if (variant.OSPlatform is null && variant.ProcessArchitecture is null)
                            {
                                Error($"A variant of the source {source.Name} of the project {name} says no platform; give it an osPlatform, a processArchitecture or both.");
                            }
                            else if (variant.OSPlatform is { } system && string.IsNullOrWhiteSpace(system))
                            {
                                Error($"A variant of the source {source.Name} of the project {name} has an osPlatform with no name.");
                            }

                            if (variant.Files.Count == 0)
                            {
                                Error($"A variant of the source {source.Name} of the project {name} names no files.");
                            }
                        }
                    }
                }
            }
        }

        // Puts in the value of each variable a path of a settings file names
        // as $(Name): from its local settings file, then the environment,
        // then its settings. A name with no value is left as it stands;
        // CheckVariables says so.
        [return: System.Diagnostics.CodeAnalysis.NotNullIfNotNull(nameof(path))]
        private static string? Expand(Part part, string? path) =>
            path is null ? null : VariableReference().Replace(path, match => ResolveVariable(part, match.Groups[1].Value) ?? match.Value);

        private static string? ResolveVariable(Part part, string name)
        {
            if (part.Settings.LocalVariables.TryGetValue(name, out var local))
            {
                return local;
            }

            return Environment.GetEnvironmentVariable(name) ?? (part.Settings.Variables.TryGetValue(name, out var given) ? given.Path : null);
        }

        // The variables a settings file's paths name that this run needs, so
        // that one with no value is reported once and before anything is
        // read. A project read from its summary needs its folders and none
        // of its sources.
        private HashSet<string> VariablesNamed(Project project, Part part)
        {
            var paths = new[] { part.Settings.Output }
                .Concat(part.Settings.Plugins)
                .Concat(IsTranslated(project) ? part.Settings.Sources.SelectMany(source => source.Files.Concat(source.Variants.SelectMany(variant => variant.Files)).Concat(source.Includes)) : [])
                .OfType<string>();
            return new HashSet<string>(
                paths.SelectMany(path => VariableReference().Matches(path).Select(match => match.Groups[1].Value)),
                StringComparer.OrdinalIgnoreCase);
        }

        private void CheckVariables()
        {
            foreach (var project in _given)
            {
                foreach (var part in project.Parts)
                {
                    foreach (var name in VariablesNamed(project, part).Where(name => ResolveVariable(part, name) is null))
                    {
                        Error($"The variable {name} has no value. Give it one in the local settings file, in the environment, or under variables in the settings.", part.Folder);
                    }
                }
            }
        }

        // A variable that names a folder to download is fetched when the
        // folder is not there, or on every run where the options say always,
        // and only for the path the settings give: a value from the local
        // settings file or the environment is somebody's own folder, used as
        // it is. Only what this run reads is fetched: the sources of a
        // project read from its summary are not. Two settings files that
        // name one folder fetch it once.
        private void FetchSources()
        {
            var fetcher = new SourceFetcher(options);
            foreach (var project in _given)
            {
                foreach (var part in project.Parts)
                {
                    var named = VariablesNamed(project, part);
                    foreach (var (name, variable) in part.Settings.Variables.Where(pair => pair.Value.Url is not null && named.Contains(pair.Key)))
                    {
                        if (variable.Sha256 is null)
                        {
                            Error($"The variable {name} says where to download from and not what the archive's SHA-256 hash is; it is not downloaded without one.");
                            continue;
                        }

                        if (ResolveVariable(part, name) != variable.Path)
                        {
                            continue;
                        }

                        var folder = Path.GetFullPath(Path.Combine(part.Folder, variable.Path));
                        if (_fetched.Contains(folder, StringComparer.OrdinalIgnoreCase)
                            || (Directory.Exists(folder) && options.Fetch != FetchPolicy.Always))
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
        }

        private void Read()
        {
            foreach (var project in _given)
            {
                // Told never to fetch, a project whose sources are not here
                // is compiled as it was translated last, where that is all
                // there.
                if (options.Fetch == FetchPolicy.Never && !HasSources(project))
                {
                    if (!IsAllThere(project) || !ReadSummary(project))
                    {
                        Error($"The sources of the project {project.Name} are not on this machine, and its translation is not all there to be compiled instead; fetch them first, translating with --fetch once.");
                    }

                    continue;
                }

                _projects.Add(project);
                foreach (var (part, sourceSettings) in project.Sources)
                {
                    var source = new Source(project, part, sourceSettings);
                    _sources.Add(source);

                    foreach (var file in FilesOf(project, part, sourceSettings, sourceSettings.Files))
                    {
                        Read(source, file, null);
                    }

                    foreach (var variant in sourceSettings.Variants)
                    {
                        var platform = new PlatformVariant(variant.OSPlatform, variant.ProcessArchitecture);
                        foreach (var file in FilesOf(project, part, sourceSettings, variant.Files))
                        {
                            Read(source, file, platform);
                        }
                    }

                    GatherVariants(source);
                }
            }
        }

        // Each unit read from a variant's files stands in for the unit of its
        // name among the source's own on its platform, and has to be callable
        // as that one is: the same procedures, each with as many arguments.
        // Its library tries the most specific variant first, and of two
        // equally specific the one given first.
        private void GatherVariants(Source source)
        {
            var bases = source.Units.Where(unit => unit.Variant is null).ToDictionary(unit => unit.Name, StringComparer.Ordinal);
            foreach (var group in source.Units.Where(unit => unit.Variant is not null).GroupBy(unit => unit.Name, StringComparer.Ordinal))
            {
                if (!bases.TryGetValue(group.Key, out var baseUnit))
                {
                    Error($"The source {source.Settings.Name} has a variant of {group.Key} for {group.First().Variant!.Description} and no {group.Key} of its own for every other platform.", group.First().File);
                    continue;
                }

                var variants = group.OrderBy(unit => unit.Variant!.Rank).ToList();
                var keys = new HashSet<string>(StringComparer.Ordinal) { PlatformVariant.BaseKey };
                foreach (var variant in variants)
                {
                    var platform = variant.Variant!;
                    if (!keys.Add(platform.Key))
                    {
                        Error($"The source {source.Settings.Name} has two variants of {group.Key} that are both named for {platform.Key}; give each a platform of its own.", variant.File);
                    }

                    if (!variant.Procedures.SequenceEqual(baseUnit.Procedures, StringComparer.Ordinal)
                        || variant.Summary.EntryPoints.Zip(baseUnit.Summary.EntryPoints).Any(pair => pair.First.Arguments.Count != pair.Second.Arguments.Count))
                    {
                        Error($"The variant of {group.Key} for {platform.Description} is not called as {group.Key} is: it has to have the same procedures, each with the same arguments.", variant.File);
                    }
                }

                source.Variants[group.Key] = variants;
            }
        }

        // Whether any file a project's sources name is on the machine.
        private static bool HasSources(Project project) => project.Sources
            .SelectMany(source => source.Settings.Files.Select(entry => Path.GetFullPath(Path.Combine(source.Part.Folder, Expand(source.Part, entry)))))
            .Any(path => Path.GetDirectoryName(path) is { } folder && Directory.Exists(folder) && Directory.GetFiles(folder, Path.GetFileName(path)).Length > 0);

        private List<string> FilesOf(Project project, Part part, SourceSettings source, IEnumerable<string> entries)
        {
            var files = new List<string>();
            foreach (var entry in entries)
            {
                var path = Path.GetFullPath(Path.Combine(part.Folder, Expand(part, entry)));
                if (!_patterns.Contains(path, StringComparer.OrdinalIgnoreCase))
                {
                    _patterns.Add(path);
                }

                var folder = Path.GetDirectoryName(path) ?? path;
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

        // A unit of a variant is not a procedure of the catalogue: callers
        // reach the unit it stands in for, whose name it has.
        private void Read(Source source, string file, PlatformVariant? variant)
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
                    if (variant is null)
                    {
                        source.Catalogue.Add(summary);
                    }

                    source.Units.Add(new Unit(file, table, summary) { Variant = variant });
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
                .Select(folder => Path.GetFullPath(Path.Combine(source.Part.Folder, Expand(source.Part, folder))))
                .Prepend(Path.GetDirectoryName(file) ?? file);

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
                // when it was made.
                if (_summaries.TryGetValue(project, out var summary))
                {
                    _references[project] = [.. _projects.Where(other => summary.References.Contains(other.Name, StringComparer.Ordinal))];
                    continue;
                }

                // One made now refers to what its calls reach.
                var used = _sources
                    .Where(source => source.Project == project)
                    .SelectMany(Used)
                    .Where(owner => owner.Project != project)
                    .Select(owner => owner.Project)
                    .ToHashSet();
                _references[project] = [.. _projects.Where(used.Contains)];
            }

            // Translations cannot refer to each other in a circle: a region
            // of working buffers is laid out after those of what it refers
            // to, and a scaffold's projects could not refer to each other so.
            foreach (var project in _projects)
            {
                if (Reaches(project, project, []))
                {
                    Error($"The project {project.Name} calls into itself by way of the projects it calls into; sources that call each other both ways belong in one project.");
                    break;
                }
            }
        }

        private bool Reaches(Project from, Project target, HashSet<Project> seen) =>
            _references.GetValueOrDefault(from, []).Any(next => next == target || (seen.Add(next) && Reaches(next, target, seen)));

        // A project with a main program is scaffolded as an executable, which
        // starts with one of them. That is asked only of one that is
        // scaffolded: elsewhere no Main is written, so there is nothing to
        // choose.
        private void WorkOutEntries()
        {
            foreach (var project in _projects.Where(project => IsTranslated(project) && WillScaffold(project)))
            {
                var mains = _sources
                    .Where(source => source.Project == project)
                    .SelectMany(source => source.Units.Where(unit => unit.Summary.Kind == UnitKind.MainProgram && unit.Variant is null).Select(unit => (Source: source, Unit: unit)))
                    .ToList();
                var names = string.Join(", ", mains.Select(main => main.Unit.Name));

                if (mains.Count == 0)
                {
                    continue;
                }

                if (options.Entry is { } entry)
                {
                    var chosen = mains.Where(main => string.Equals(main.Unit.Name, entry, StringComparison.OrdinalIgnoreCase)).ToList();
                    if (chosen.Count == 1)
                    {
                        _entries[project] = StartWith(project, chosen[0].Source, chosen[0].Unit);
                    }
                    else
                    {
                        Error($"The entry {entry} is not one of the main programs of the project {project.Name}: {names}.");
                    }
                }
                else if (mains.Count == 1)
                {
                    _entries[project] = StartWith(project, mains[0].Source, mains[0].Unit);
                }
                else
                {
                    Error($"The project {project.Name} has {mains.Count} main programs, {names}; name the one its program starts with as the entry.");
                }
            }
        }

        // The call a program makes of its main program, its class named in
        // full where it lies in a namespace other than the program's.
        private static string StartWith(Project project, Source source, Unit unit) =>
            $"context.Get<{(source.Namespace == project.Namespace ? source.ClassName : $"global::{source.Namespace}.{source.ClassName}")}>().{unit.Name}();";

        private void Emit()
        {
            foreach (var source in _sources.Where(source => !source.IsSummarized))
            {
                var options = new EmitOptions(source.Namespace, source.ClassName)
                {
                    References = [.. Used(source).Select(other => new LibraryReference(other.Namespace, other.ClassName, other.Catalogue.Find))],
                };

                var emitter = new CSharpEmitter(options, source.Catalogue.Find);
                var emittedUnits = new Dictionary<Unit, EmittedUnit>();
                foreach (var unit in source.Units)
                {
                    try
                    {
                        // A unit written for several platforms is a file for
                        // each, named as no unit can be, a FORTRAN name having
                        // no dot, and the file of its name calls them.
                        var platform = source.PlatformOf(unit);
                        var name = FileName(platform is null ? unit.Name : $"{unit.Name}.{platform.Key}");
                        for (var number = 2; source.Files.ContainsKey(name); number++)
                        {
                            name = FileName(string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{unit.Name}_{number}"));
                        }

                        var emitted = emitter.EmitUnit(unit.Table, unit.File, platform);
                        source.Files[name] = emitted.Text;
                        source.Emitted[name] = emitted;
                        source.UnitOfFile[name] = unit;
                        foreach (var method in emitted.Methods)
                        {
                            source.Methods[method.Name] = (unit, method);
                        }

                        emittedUnits[unit] = emitted;
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

                foreach (var (unitName, variants) in source.Variants)
                {
                    var baseUnit = source.Units.Single(unit => unit.Variant is null && unit.Name == unitName);
                    if (emittedUnits.ContainsKey(baseUnit) && variants.All(emittedUnits.ContainsKey))
                    {
                        EmitDispatch(source, emitter, emittedUnits, baseUnit, variants);
                    }
                }

                source.Files[FileName(source.ClassName)] = emitter.EmitClass(null, ChoicesOf(source));
            }
        }

        // The file of a unit written for several platforms that has its
        // procedures' names, each calling the variant its library chose. The
        // variants are called the same way as the base, so each of their
        // methods has to have the base's parameters and result.
        private void EmitDispatch(Source source, CSharpEmitter emitter, Dictionary<Unit, EmittedUnit> emittedUnits, Unit baseUnit, List<Unit> variants)
        {
            var emittedBase = emittedUnits[baseUnit];
            foreach (var variant in variants)
            {
                foreach (var procedure in baseUnit.Procedures)
                {
                    var heading = HeadingOf(emittedBase, PlatformVariant.Base.GetMethodName(procedure));
                    var other = HeadingOf(emittedUnits[variant], variant.Variant!.GetMethodName(procedure));
                    if (heading is not null && other is not null
                        && (heading.ReturnType != other.ReturnType || !heading.Parameters.SequenceEqual(other.Parameters, StringComparer.Ordinal)))
                    {
                        Error(
                            $"The variant of {procedure} for {variant.Variant!.Description} is called as {other.ReturnType} {procedure}({string.Join(", ", other.Parameters)}), where {procedure} is called as {heading.ReturnType} {procedure}({string.Join(", ", heading.Parameters)}); a variant has to be called as the procedure it stands in for is.",
                            variant.File);
                    }
                }
            }

            var dispatch = emitter.EmitDispatch(
                emittedBase,
                baseUnit.Procedures,
                [.. variants.Select(variant => variant.Variant!)],
                procedure => baseUnit.Summary.EntryPoints.FirstOrDefault(entry => entry.Name == procedure)?.Arguments.Any(argument =>
                    argument.Kind is DummyArgumentKind.Function or DummyArgumentKind.Subroutine or DummyArgumentKind.Procedure) == true);
            var name = FileName(baseUnit.Name);
            source.Files[name] = dispatch.Text;
            source.Emitted[name] = dispatch;
            foreach (var method in dispatch.Methods)
            {
                source.Methods[method.Name] = (baseUnit, method);
            }
        }

        private static TreeMethod? HeadingOf(EmittedUnit unit, string procedure) =>
            unit.File?.Members.OfType<TreeMethod>().FirstOrDefault(method => method.Procedure == procedure);

        // What the constructor of a source's library chooses between: each
        // unit that has variants, with them in the order they are tried.
        private static List<(string Unit, IReadOnlyList<PlatformVariant> Variants)> ChoicesOf(Source source) =>
        [
            .. source.Variants
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => (pair.Key, (IReadOnlyList<PlatformVariant>)[.. pair.Value.Select(unit => unit.Variant!)])),
        ];

        // Each unit again with its procedures' buffers where a context says,
        // now that every procedure has its place, and each class with what
        // it asks of its context for them.
        private void WriteWithPlaces()
        {
            // The procedures of each source that are written as an entry and
            // a core, by the names they are called by.
            var cores = _sources
                .Where(source => !source.IsSummarized)
                .ToDictionary(
                    source => source,
                    source => source.Emitted.Values
                        .SelectMany(emitted => CSharpEmitter.FindCores(emitted, procedure => _places.TryGetValue((source, procedure), out var place) ? place : null))
                        .ToHashSet(StringComparer.Ordinal));

            foreach (var source in _sources.Where(source => !source.IsSummarized))
            {
                // A call as written reaches a procedure of this source, or of
                // another of the project by the member that gives its library;
                // a library of another project is another assembly, and its
                // core not to be reached.
                bool HasCore(string called)
                {
                    var dot = called.LastIndexOf('.');
                    if (dot < 0)
                    {
                        return cores[source].Contains(called);
                    }

                    var accessor = called[..dot];
                    return cores.Any(pair => pair.Key.Project == source.Project
                        && "_" + pair.Key.ClassName == accessor
                        && pair.Value.Contains(called[(dot + 1)..]));
                }

                // In the order of the files' names, so that a constant has
                // the same place however the units came to be read.
                var text = source.Part.Settings.InlineTextConstants ? null : new TextBlock();
                foreach (var (name, emitted) in source.Emitted.OrderBy(unit => unit.Key, StringComparer.Ordinal))
                {
                    source.Files[name] = CSharpEmitter.WriteWithPlaces(
                        emitted,
                        procedure => _places.TryGetValue((source, procedure), out var place) ? place : null,
                        text,
                        source.Part.Settings.PlainArrayLoops,
                        procedure => _whyOnStack.TryGetValue((source, procedure), out var why) && why.Count > 0 ? why : null,
                        HasCore);
                }

                var region = _workingRegions.Single(region => region.Project == source.Project.Name);
                var (own, reach) = WorkOutExtent(source);
                var options = new EmitOptions(source.Namespace, source.ClassName)
                {
                    References = [.. Used(source).Select(other => new LibraryReference(other.Namespace, other.ClassName, other.Catalogue.Find))],
                };
                var emitter = new CSharpEmitter(options, source.Catalogue.Find);
                source.Files[FileName(source.ClassName)] = emitter.EmitClass(
                    new LibraryRegion(
                        region.Project,
                        own,
                        reach,
                        [.. _references.GetValueOrDefault(source.Project, []).Select(project => project.Name)]),
                    ChoicesOf(source));

                // The constants in a file of their own beside the class's,
                // named as no unit can be, a FORTRAN name having no dot.
                if (text is { IsEmpty: false })
                {
                    source.Files[FileName(source.ClassName + ".Text")] = emitter.EmitTextBlock(text);
                }
            }
        }

        // A file named for a routine, which on some systems may not have
        // the name of a device whatever follows the name, a dot and more
        // among it.
        private static string FileName(string name)
        {
            string[] devices = ["CON", "PRN", "AUX", "NUL"];
            var stem = name.Split('.')[0];
            var isDevice = devices.Contains(stem, StringComparer.OrdinalIgnoreCase)
                || (stem.Length == 4 && char.IsAsciiDigit(stem[3]) && (stem.StartsWith("COM", StringComparison.OrdinalIgnoreCase) || stem.StartsWith("LPT", StringComparison.OrdinalIgnoreCase)));
            return (isDevice ? "_" + name : name) + GeneratedExtension;
        }

        private void Write()
        {
            // What a project is, is decided before any is written: writing a
            // project file makes one that exists.
            var translated = _projects.Where(IsTranslated).ToList();
            var scaffolded = translated.Where(WillScaffold).ToHashSet();

            foreach (var project in translated)
            {
                foreach (var part in project.Parts)
                {
                    var generatedFolder = GeneratedFolderOf(part);
                    Directory.CreateDirectory(generatedFolder);

                    var manifestPath = ManifestPathOf(part);
                    var after = new SortedDictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
                    foreach (var source in _sources.Where(source => source.Part == part))
                    {
                        var generated = Path.Combine(generatedFolder, source.Settings.Name);
                        Directory.CreateDirectory(generated);

                        // A file is written where its text is not already
                        // what is there, so that one the translation does not
                        // change keeps its time and is not compiled again,
                        // and one changed by hand is put right. Line endings
                        // are not compared: a checkout may have changed them.
                        foreach (var (name, text) in source.Files)
                        {
                            var path = Path.Combine(generated, name);
                            _files.Add(path);
                            if (options.Force || !File.Exists(path) || File.ReadAllText(path).ReplaceLineEndings("\n") != text.ReplaceLineEndings("\n"))
                            {
                                File.WriteAllText(path, text);
                                _written.Add(path);
                            }
                        }

                        after[source.Settings.Name] = [.. source.Files.Keys.Order(StringComparer.Ordinal)];

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

                    var manifest = JsonSerializer.Serialize(new Manifest(after), TranslationJson.Default).ReplaceLineEndings("\n") + "\n";
                    if (!File.Exists(manifestPath) || File.ReadAllText(manifestPath) != manifest)
                    {
                        File.WriteAllText(manifestPath, manifest);
                    }

                    _files.Add(manifestPath);
                }

                // What the projects that call into this one need of it, for a
                // run that leaves this one out: beside its project file, and
                // committed with it.
                var summaryFolder = project.Folder ?? Path.GetDirectoryName(project.SummaryPath) ?? project.SummaryPath;
                Directory.CreateDirectory(summaryFolder);
                WriteIfChanged(project.SummaryPath, SummarizeProject(project).ToJson());
                _files.Add(project.SummaryPath);

                if (scaffolded.Contains(project))
                {
                    WriteIfChanged(ProjectFileOf(project, summaryFolder), ProjectFile(project, summaryFolder));
                    if (_entries.TryGetValue(project, out var entry))
                    {
                        WriteIfChanged(Path.Combine(summaryFolder, "Program.cs"), Program(project, entry));
                        _files.Add(Path.Combine(summaryFolder, "Program.cs"));
                    }
                }
            }

            // A project of the run read from its summary is still what its
            // folders hold, for a build to compile: what its manifests say
            // was written there.
            foreach (var project in _given.Where(_summaries.ContainsKey))
            {
                _files.Add(project.SummaryPath);
                foreach (var part in project.Parts.Where(part => File.Exists(ManifestPathOf(part))))
                {
                    _files.Add(ManifestPathOf(part));
                    foreach (var (source, names) in ReadManifest(ManifestPathOf(part)) ?? [])
                    {
                        _files.AddRange(names
                            .Select(name => Path.Combine(GeneratedFolderOf(part), source, name))
                            .Where(File.Exists));
                    }
                }
            }
        }

        // The files a manifest lists, by source; nothing where there is no
        // manifest or it cannot be read, which says nothing of what was
        // written, as against one of settings with no sources, which lists
        // none.
        private static Dictionary<string, IReadOnlyList<string>>? ReadManifest(string path)
        {
            try
            {
                return File.Exists(path) && JsonSerializer.Deserialize<Manifest>(File.ReadAllText(path), TranslationJson.Default)?.Sources is { } sources
                    ? new Dictionary<string, IReadOnlyList<string>>(sources, StringComparer.Ordinal)
                    : null;
            }
            catch (JsonException)
            {
                return null;
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

        // The project file a scaffold writes: what the options say of the
        // runtime and the framework, what of the translation lies outside
        // its folder, which the SDK does not find by itself, and a reference
        // to the project of each the translation calls into that has one.
        private string ProjectFile(Project project, string folder)
        {
            string Relative(string path) => Path.GetRelativePath(folder, path).Replace('\\', '/');

            var runtimeSettings = options.Runtime;
            string runtime;
            if (runtimeSettings.Project is { } runtimeProject)
            {
                runtime = $"    <ProjectReference Include=\"{Relative(Path.GetFullPath(runtimeProject))}\" />";
            }
            else if (runtimeSettings.OmitPackageVersion)
            {
                runtime = "    <PackageReference Include=\"Dracocephalum.Fortrania.Runtime\" />";
            }
            else
            {
                runtime = $"    <PackageReference Include=\"Dracocephalum.Fortrania.Runtime\" Version=\"{runtimeSettings.Package ?? OwnVersion()}\" />";
            }

            var lines = new List<string>
            {
                "<Project Sdk=\"Microsoft.NET.Sdk\">",
                string.Empty,
                "  <PropertyGroup>",
                $"    <TargetFramework>{options.Framework}</TargetFramework>",
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
                "  </PropertyGroup>",
                string.Empty,
                "  <ItemGroup>",
                runtime,
            ]);

            var inside = folder.EndsWith(Path.DirectorySeparatorChar) ? folder : folder + Path.DirectorySeparatorChar;
            lines.AddRange(project.Parts
                .Select(GeneratedFolderOf)
                .Where(generated => !generated.StartsWith(inside, StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(generated => $"    <Compile Include=\"{Relative(generated)}/**/*{GeneratedExtension}\" />"));

            // A project an assembly brings has no folder here, and is
            // referred to by whoever passed the assembly. Any other is
            // referred to by its project file: its own, or the one a scaffold
            // writes for it in this run.
            lines.AddRange(_references[project]
                .Select(reference => FindProjectFile(reference) ?? (reference.Folder is { } other && _given.Contains(reference) ? ProjectFileOf(reference, other) : null))
                .OfType<string>()
                .Select(file => $"    <ProjectReference Include=\"{Relative(file)}\" />"));
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
