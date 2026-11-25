using Dracocephalum.Fortrania.Emit;

namespace Dracocephalum.Fortrania.Translation;

/// <content>
/// Gives each procedure of a translation a place for its working buffers
/// in a region of the translation's own, so that procedures of it
/// that can be running at the same time never share bytes.
/// </content>
public static partial class Translator
{
    private sealed partial class Translation
    {
        // Where each procedure's buffers start in its translation's region. One
        // that has none is given a place all the same, since its callees
        // start after its callers' buffers as well as its own.
        private readonly Dictionary<(Source Source, string Name), long> _places = [];
        private readonly List<WorkingRegion> _workingRegions = [];

        // A procedure starts where the buffers of its deepest caller in its
        // own translation end. FORTRAN 77 has no recursion, so the calls form a
        // graph without cycles, and a place is the longest way down to the
        // procedure from any that nothing of the translation calls.
        //
        // Only calls by name within one translation count. A call into
        // another leaves this region for that one's, which lies after
        // it. A procedure called through an argument runs in a region of
        // its own, since the runtime gives every such call the next level's,
        // so neither it nor what it calls can meet a buffer of the chain
        // that led to it.
        private void WorkOutPlaces()
        {
            // A translation read from its summary brings its sizes with it.
            var sizes = new Dictionary<Project, long>();
            var reaches = new Dictionary<Project, long>();
            foreach (var project in _projects)
            {
                if (_summaries.TryGetValue(project, out var summary))
                {
                    sizes[project] = summary.OwnBytes;
                    reaches[project] = summary.ReachBytes;
                }
                else
                {
                    sizes[project] = PlaceProceduresOf(project);
                }
            }

            // What a translation can need when it is the first to be called:
            // its own region and, after it, the most any chain of those it
            // refers to needs.
            long ReachOf(Project project) => reaches.TryGetValue(project, out var known)
                ? known
                : reaches[project] = sizes[project] + _references.GetValueOrDefault(project, []).Select(ReachOf).DefaultIfEmpty(0).Max();

            _workingRegions.AddRange(_projects
                .OrderBy(project => project.Name, StringComparer.Ordinal)
                .Select(project => new WorkingRegion(project.Name, sizes[project], ReachOf(project))));
        }

        private long PlaceProceduresOf(Project project)
        {
            var callees = new Dictionary<(Source Source, string Name), HashSet<(Source Source, string Name)>>();
            var callers = new Dictionary<(Source Source, string Name), int>();
            foreach (var source in _sources.Where(source => source.Project == project))
            {
                foreach (var name in source.Methods.Keys)
                {
                    callees[(source, name)] = [];
                    callers[(source, name)] = 0;
                }
            }

            foreach (var procedure in callees.Keys)
            {
                foreach (var call in procedure.Source.Methods[procedure.Name].Method.Calls)
                {
                    if (FindOwner(procedure.Source, call.Name) is { } owner && owner.Project == project && callees[procedure].Add((owner, call.Name)))
                    {
                        callers[(owner, call.Name)]++;
                    }
                }
            }

            // Each procedure once all that call it are placed. What is left
            // when no such procedure remains is on a chain that comes back
            // to itself, or beneath one, and is given no place.
            long size = 0;
            var ready = new Queue<(Source Source, string Name)>(callers.Where(pair => pair.Value == 0).Select(pair => pair.Key));
            foreach (var root in ready)
            {
                _places[root] = 0;
            }

            while (ready.TryDequeue(out var procedure))
            {
                var end = _places[procedure] + WorkingBuffersPass.PieceBytes(procedure.Source.Methods[procedure.Name].Method.StackBytes);
                size = Math.Max(size, end);
                foreach (var callee in callees[procedure])
                {
                    _places[callee] = Math.Max(_places.GetValueOrDefault(callee), end);
                    if (--callers[callee] == 0)
                    {
                        ready.Enqueue(callee);
                    }
                }
            }

            foreach (var unplaced in callers.Where(pair => pair.Value > 0).Select(pair => pair.Key))
            {
                _places.Remove(unplaced);
            }

            return size;
        }
    }
}
