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
        private readonly Dictionary<ProjectSettings, ProjectSummary> _summaries = [];

        // The projects that take part: those translated, and those read
        // from a summary. One the options leave out that has no summary
        // takes no part, and a project that calls into it is told so.
        private readonly List<ProjectSettings> _projects = [];

        // What each procedure of a project translated this time does with
        // its arguments, counting what is done further down, in other
        // projects as well.
        private readonly Dictionary<ProjectSettings, ArgumentEffects> _effects = [];

        private bool IsTranslated(ProjectSettings project) => options.Projects is not { } named || named.Contains(project.Name, StringComparer.Ordinal);

        private string SummaryPathOf(ProjectSettings project) => Path.Combine(FolderOf(project), GeneratedFolder, ProjectSummary.FileName);

        private void CheckProjectsNamed()
        {
            foreach (var missing in (options.Projects ?? []).Where(name => !settings.Projects.Any(project => project.Name == name)))
            {
                Error($"The options name the project {missing} to translate, and the settings have no project of that name.");
            }
        }

        // A project left out is read from its summary where there is one;
        // its sources are not read at all.
        private void ReadSummary(ProjectSettings project)
        {
            var path = SummaryPathOf(project);
            if (!File.Exists(path))
            {
                return;
            }

            ProjectSummary summary;
            try
            {
                summary = ProjectSummary.Load(path);
            }
            catch (Exception exception) when (exception is JsonException or IOException)
            {
                Error(exception.Message, path);
                return;
            }

            var names = project.Sources.Select(source => source.Name).ToList();
            if (summary.Project != project.Name || !summary.Sources.Select(source => source.Name).SequenceEqual(names, StringComparer.Ordinal))
            {
                Error($"The summary is of the project {summary.Project} with the sources {string.Join(", ", summary.Sources.Select(source => source.Name))}, where the settings have {project.Name} with {string.Join(", ", names)}; translate {project.Name} again.", path);
                return;
            }

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

        // A project the options leave out and that has no summary can still
        // be named by one that is translated; that is an error, since its
        // procedures cannot be known.
        private void CheckSummarizedReferences()
        {
            foreach (var project in _projects.Where(IsTranslated))
            {
                foreach (var reference in project.References ?? [])
                {
                    if (settings.Projects.FirstOrDefault(other => other.Name == reference) is { } other && !_projects.Contains(other))
                    {
                        Error($"The project {project.Name} refers to {reference}, which is not translated this time and has no summary at {SummaryPathOf(other)}; translate {reference} first.");
                    }
                }
            }
        }

        // What a project translated this time shows to those that call into
        // it, in the order of the settings' sources and of the procedures'
        // names, so that a translation that changes nothing a caller sees
        // writes the same bytes.
        private ProjectSummary SummarizeProject(ProjectSettings project)
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
            var done = new HashSet<ProjectSettings>(_summaries.Keys);
            void WorkOut(ProjectSettings project)
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
