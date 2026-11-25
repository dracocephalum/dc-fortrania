using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text.Json;
using Dracocephalum.Fortrania.Analysis;

namespace Dracocephalum.Fortrania.Translation;

/// <content>
/// Making a translation apart from those it calls into: each translation
/// made writes its summary, and one that is not made is read from the
/// summary it wrote last.
/// </content>
public static partial class Translator
{
    private sealed partial class Translation
    {
        // The translations not made this time, by the summary each was read
        // from.
        private readonly Dictionary<Project, ProjectSummary> _summaries = [];

        // The translations that take part: those made, and those read from
        // a summary.
        private readonly List<Project> _projects = [];

        // The translations the options' assemblies bring, which are none of
        // the run's: read, never written, and referred to by whoever passed
        // the assembly.
        private readonly HashSet<Project> _external = [];

        // What each procedure of a translation made this time does with its
        // arguments, counting what is done further down, in other
        // translations as well.
        private readonly Dictionary<Project, ArgumentEffects> _effects = [];

        // Every translation of the run is made but one whose settings say
        // not to, unless the run is forced.
        private bool IsTranslated(Project project) =>
            !_summaries.ContainsKey(project) && (options.Force || project.Settings.Translate);

        private static string SummaryPathOf(Project project) => Path.Combine(FolderOf(project), GeneratedFolder, ProjectSummary.GetFileName(project.Name));

        // A translation not made is read from its summary where there is
        // one; its sources are not read at all. Says whether there was one.
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
                Error($"The summary is of the translation {summary.Project} with the sources {string.Join(", ", summary.Sources.Select(source => source.Name))}, where the settings have {project.Name} with {string.Join(", ", names)}; make {project.Name} again.", path);
                return true;
            }

            AddSummarized(project, summary);
            return true;
        }

        // The sources of a translation read from its summary keep the
        // namespaces it was made with, whatever this run would give them.
        private void AddSummarized(Project project, ProjectSummary summary)
        {
            _summaries[project] = summary;
            _projects.Add(project);
            foreach (var (sourceSettings, sourceSummary) in project.Sources.Zip(summary.Sources))
            {
                var source = new Source(project, sourceSettings) { IsSummarized = true, Namespace = sourceSummary.Namespace };
                _sources.Add(source);
                foreach (var procedure in sourceSummary.Procedures)
                {
                    source.Catalogue.Add(procedure.ToInterface());
                    source.Summarized[procedure.Name] = procedure;
                }
            }
        }

        // Each summary an assembly the options name carries brings its
        // translation, as one read from its summary is: its sources are its
        // classes, by the names the summary gives them. One of the name of
        // another that takes part, or that defines a procedure another
        // assembly defines too, is refused, since a call could not tell
        // which it reaches.
        private void ReadReferences()
        {
            var definedBy = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var path in (options.References ?? []).Select(Path.GetFullPath).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                List<ProjectSummary> summaries;
                try
                {
                    summaries = ReadEmbeddedSummaries(path);
                }
                catch (Exception exception) when (exception is JsonException or IOException or BadImageFormatException or UnauthorizedAccessException)
                {
                    Error(exception.Message, path);
                    continue;
                }

                if (summaries.Count > 0)
                {
                    NoteRead(path);
                }

                foreach (var summary in summaries)
                {
                    if (_given.Any(project => project.Name == summary.Project) || _external.Any(project => project.Name == summary.Project))
                    {
                        Error($"The assembly holds the translation {summary.Project}, which the run or another assembly has as well.", path);
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
                        Sources = [.. summary.Sources.Select(source => new SourceSettings { Name = source.Name, Class = source.Class })],
                    };
                    var project = new Project(settings, summary.Project, summary.Project, Path.GetDirectoryName(path) ?? path);
                    _external.Add(project);
                    AddSummarized(project, summary);
                }
            }
        }

        // The summaries an assembly carries, one for each translation
        // compiled into it, read from its metadata without loading it; none
        // for one that carries none.
        private static List<ProjectSummary> ReadEmbeddedSummaries(string path)
        {
            using var stream = File.OpenRead(path);
            using var image = new PEReader(stream);
            if (!image.HasMetadata)
            {
                return [];
            }

            var summaries = new List<ProjectSummary>();
            var reader = image.GetMetadataReader();
            foreach (var handle in reader.ManifestResources)
            {
                var resource = reader.GetManifestResource(handle);
                var name = reader.GetString(resource.Name);
                if (!resource.Implementation.IsNil
                    || !name.StartsWith(ProjectSummary.ResourcePrefix, StringComparison.Ordinal)
                    || !name.EndsWith(ProjectSummary.FileSuffix, StringComparison.Ordinal))
                {
                    continue;
                }

                // An embedded resource lies in the image's resources, its
                // length first, at the offset the manifest gives.
                var directory = image.PEHeaders.CorHeader!.ResourcesDirectory;
                var at = image.GetSectionData(directory.RelativeVirtualAddress).GetReader();
                at.Offset = (int)resource.Offset;
                var length = at.ReadInt32();
                summaries.Add(ProjectSummary.Parse(System.Text.Encoding.UTF8.GetString(at.ReadBytes(length))));
            }

            return summaries;
        }

        // What a translation made this time shows to those that call into
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

        // The effects of each translation made, those it refers to first,
        // so that a call into another one counts what that one does:
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
