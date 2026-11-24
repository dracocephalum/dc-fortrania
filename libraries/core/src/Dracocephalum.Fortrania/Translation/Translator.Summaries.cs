using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text.Json;
using Dracocephalum.Fortrania.Analysis;

namespace Dracocephalum.Fortrania.Translation;

/// <content>
/// Translating a project apart from the projects it calls into: each
/// project translated writes its summary, and a project that is not
/// translated is read from the summary its last translation wrote.
/// </content>
public static partial class Translator
{
    private sealed partial class Translation
    {
        // The projects not translated this time, by the summary each was
        // read from.
        private readonly Dictionary<Project, ProjectSummary> _summaries = [];

        // The projects that take part: those translated, and those read
        // from a summary. One of the run's that says never to translate and
        // has no summary takes no part, and a project that calls into it is
        // told so.
        private readonly List<Project> _projects = [];

        // The projects the options' assemblies bring, which are no project
        // of the run: read, never written, and referred to by whoever passed
        // the assembly.
        private readonly HashSet<Project> _external = [];

        // What each procedure of a project translated this time does with
        // its arguments, counting what is done further down, in other
        // projects as well.
        private readonly Dictionary<Project, ArgumentEffects> _effects = [];

        // Every project of the run is translated but one whose settings say
        // never, unless the run is forced.
        private bool IsTranslated(Project project) =>
            !_summaries.ContainsKey(project) && (options.Force || project.Settings.Translate != TranslateMode.Never);

        // Whether every file of a project translated is written again,
        // changed or not.
        private bool IsForced(Project project) => options.Force || project.Settings.Translate == TranslateMode.Always;

        private static string SummaryPathOf(Project project) => Path.Combine(FolderOf(project), GeneratedFolder, ProjectSummary.FileName);

        // A project not translated is read from its summary where there is one;
        // its sources are not read at all. Says whether there was one.
        private bool ReadSummary(Project project)
        {
            var path = SummaryPathOf(project);
            if (!File.Exists(path))
            {
                return false;
            }

            NoteRead(path);
            ProjectSummary summary;
            try
            {
                summary = ProjectSummary.Load(path);
            }
            catch (Exception exception) when (exception is JsonException or IOException)
            {
                Error(exception.Message, path);
                return true;
            }

            var names = project.Sources.Select(source => source.Name).ToList();
            if (summary.Project != project.Name || !summary.Sources.Select(source => source.Name).SequenceEqual(names, StringComparer.Ordinal))
            {
                Error($"The summary is of the project {summary.Project} with the sources {string.Join(", ", summary.Sources.Select(source => source.Name))}, where the settings have {project.Name} with {string.Join(", ", names)}; translate {project.Name} again.", path);
                return true;
            }

            AddSummarized(project, summary);
            return true;
        }

        private void AddSummarized(Project project, ProjectSummary summary)
        {
            _summaries[project] = summary;
            _projects.Add(project);
            foreach (var (sourceSettings, sourceSummary) in project.Sources.Zip(summary.Sources))
            {
                var source = new Source(project, sourceSettings) { IsSummarized = true };
                _sources.Add(source);
                foreach (var procedure in sourceSummary.Procedures)
                {
                    source.Catalogue.Add(procedure.ToInterface());
                    source.Summarized[procedure.Name] = procedure;
                }
            }
        }

        // Each assembly the options name that carries a summary brings its
        // project, as a project read from its summary is: its sources are
        // its classes, by the names the summary gives them. One of the name
        // of a project of the run, or that defines a procedure another
        // assembly defines too, is refused, since a call could not tell
        // which it reaches.
        private void ReadReferences()
        {
            var definedBy = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var path in (options.References ?? []).Select(Path.GetFullPath).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                ProjectSummary? summary;
                try
                {
                    summary = ReadEmbeddedSummary(path);
                }
                catch (Exception exception) when (exception is JsonException or IOException or BadImageFormatException or UnauthorizedAccessException)
                {
                    Error(exception.Message, path);
                    continue;
                }

                if (summary is null)
                {
                    continue;
                }

                NoteRead(path);
                if (_given.Any(project => project.Name == summary.Project) || _external.Any(project => project.Name == summary.Project))
                {
                    Error($"The assembly holds the project {summary.Project}, which the run or another assembly has as well.", path);
                    continue;
                }

                foreach (var procedure in summary.Sources.SelectMany(source => source.Procedures))
                {
                    if (!definedBy.TryAdd(procedure.Name, summary.Project))
                    {
                        Error($"{procedure.Name} is defined by {definedBy[procedure.Name]} and by {summary.Project}; a call could not tell which it reaches.", path);
                    }
                }

                var settings = new TranslationSettings
                {
                    Namespace = summary.Sources.Count > 0 ? summary.Sources[0].Namespace : null,
                    Sources = [.. summary.Sources.Select(source => new SourceSettings { Name = source.Name, Class = source.Class })],
                };
                var project = new Project(settings, summary.Project, Path.GetDirectoryName(path) ?? path);
                _external.Add(project);
                AddSummarized(project, summary);
            }
        }

        // The summary an assembly carries, read from its metadata without
        // loading it; nothing for one that carries none.
        private static ProjectSummary? ReadEmbeddedSummary(string path)
        {
            using var stream = File.OpenRead(path);
            using var image = new PEReader(stream);
            if (!image.HasMetadata)
            {
                return null;
            }

            var reader = image.GetMetadataReader();
            foreach (var handle in reader.ManifestResources)
            {
                var resource = reader.GetManifestResource(handle);
                if (!resource.Implementation.IsNil || reader.GetString(resource.Name) != ProjectSummary.ResourceName)
                {
                    continue;
                }

                // An embedded resource lies in the image's resources, its
                // length first, at the offset the manifest gives.
                var directory = image.PEHeaders.CorHeader!.ResourcesDirectory;
                var at = image.GetSectionData(directory.RelativeVirtualAddress).GetReader();
                at.Offset = (int)resource.Offset;
                var length = at.ReadInt32();
                return ProjectSummary.Parse(System.Text.Encoding.UTF8.GetString(at.ReadBytes(length)));
            }

            return null;
        }

        // A project translated can refer only to those that take part: the
        // run's own, translated or read from a summary, and those the
        // assemblies bring. A project of the run that has no summary, or a
        // name nothing brings, is an error, since its procedures cannot be
        // known.
        private void CheckReferencesNamed()
        {
            foreach (var project in _projects.Where(IsTranslated))
            {
                foreach (var reference in (project.Settings.References ?? []).Where(reference => !_projects.Any(other => other.Name == reference)))
                {
                    Error(_given.FirstOrDefault(other => other.Name == reference) is { } other
                        ? $"The project {project.Name} refers to {reference}, which is not translated this time and has no summary at {SummaryPathOf(other)}; translate {reference} first."
                        : $"The project {project.Name} refers to {reference}, which is neither translated with it nor in an assembly it is translated against.");
                }
            }
        }

        // What a project translated this time shows to those that call into
        // it, in the order of the settings' sources and of the procedures'
        // names, so that a translation that changes nothing a caller sees
        // writes the same bytes.
        private ProjectSummary SummarizeProject(Project project)
        {
            var region = _workingRegions.Single(region => region.Project == project.Name);
            var sources = _sources
                .Where(source => source.Project == project)
                .Select(source => new SourceSummary(
                    source.Settings.Name,
                    source.Namespace,
                    source.ClassName,
                    [
                        .. source.Units
                            .SelectMany(unit => unit.Summary.EntryPoints)
                            .OrderBy(entry => entry.Name, StringComparer.Ordinal)
                            .Select(entry => new ProcedureSummary(
                                entry.Name,
                                entry.ResultType,
                                entry.Line,
                                [
                                    .. entry.Arguments.Select((argument, index) => new ArgumentSummary(
                                        argument.Name,
                                        argument.Kind,
                                        argument.Type,
                                        argument.Rank,
                                        _effects[project].Find(entry.Name)?[index] ?? argument.Uses,
                                        argument.Shape)),
                                ],
                                source.Methods.ContainsKey(entry.Name) ? ToStackDepth(DepthOf(source, entry.Name)) : null,
                                source.Methods.ContainsKey(entry.Name) && ThroughDepthOf(source, entry.Name) is { } through ? ToStackDepth(through) : null)),
                    ]))
                .ToList();

            return new ProjectSummary(
                ProjectSummary.CurrentFormat,
                project.Name,
                region.OwnBytes,
                region.ReachBytes,
                [.. _references.GetValueOrDefault(project, []).Select(other => other.Name).Order(StringComparer.Ordinal)],
                sources);
        }

        // The effects of each project translated, those it refers to first,
        // so that a call into another project counts what that one does:
        // worked out this time, or read from its summary.
        private void WorkOutEffects()
        {
            var done = new HashSet<Project>(_summaries.Keys);
            void WorkOut(Project project)
            {
                if (!done.Add(project))
                {
                    return;
                }

                foreach (var reference in _references.GetValueOrDefault(project, []))
                {
                    WorkOut(reference);
                }

                var sources = _sources.Where(source => source.Project == project).ToList();
                IReadOnlyList<ArgumentUses>? FindElsewhere(string name)
                {
                    foreach (var other in _sources.Where(other => other.Project != project))
                    {
                        if (other.Summarized.TryGetValue(name, out var summarized))
                        {
                            return [.. summarized.Arguments.Select(argument => argument.Uses)];
                        }

                        if (_effects.TryGetValue(other.Project, out var effects) && effects.Find(name) is { } found)
                        {
                            return found;
                        }
                    }

                    return null;
                }

                _effects[project] = ArgumentEffects.Compute(sources.SelectMany(source => source.Units).Select(unit => unit.Summary), FindElsewhere);
            }

            foreach (var project in _projects)
            {
                try
                {
                    WorkOut(project);
                }
                catch (FortranAnalysisException exception)
                {
                    Error(exception.Message);
                }
            }
        }

        private static StackDepth ToStackDepth(Depth depth) => new(depth.Bytes, string.Join(' ', depth.Chain), depth.IsRecursive);

        private static Depth ToDepth(StackDepth depth) =>
            new(depth.Bytes, depth.Chain.Length == 0 ? [] : depth.Chain.Split(' '), depth.IsRecursive);
    }
}
