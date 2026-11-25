using Dracocephalum.Fortrania.Emit;

namespace Dracocephalum.Fortrania.Translation;

/// <content>
/// Gives each procedure of a translation a place for its working buffers
/// in a region of its project's own, so that procedures of the project
/// that can be running at the same time never share bytes.
/// </content>
public static partial class Translator
{
    private sealed partial class Translation
    {
        // Where each procedure's buffers start in its project's region. One
        // that has none is given a place all the same, since its callees
        // start after its callers' buffers as well as its own.
        private readonly Dictionary<(Source Source, string Name), long> _places = [];
        private readonly List<WorkingRegion> _workingRegions = [];

        // Why each procedure that has no place keeps the stack, as lines of a
        // comment for its buffers.
        private readonly Dictionary<(Source Source, string Name), List<string>> _whyOnStack = [];

        // What one library of a project asks of the project's region when it
        // is made: the furthest the buffers of any procedure of it reach, or
        // of a library of the project it calls into, however far down; and
        // after that the deepest chain of the projects those call into. A
        // library is made on its first call, maybe while its caller runs; the
        // caller asked for all of it already, so it never asks for more.
        // Libraries that never call each other ask for their own, which
        // overlap, and a program that makes only one has the arena that one
        // needs.
        private (long Own, long Reach) WorkOutExtent(Source source)
        {
            var project = source.Project;
            var reached = new HashSet<Source> { source };
            var pending = new Stack<Source>();
            pending.Push(source);
            while (pending.TryPop(out var next))
            {
                foreach (var other in Used(next).Where(other => other.Project == project && reached.Add(other)))
                {
                    pending.Push(other);
                }
            }

            var own = reached
                .SelectMany(member => member.Methods.Keys.Where(name => _places.ContainsKey((member, name))).Select(name => EndOf((member, name))))
                .DefaultIfEmpty(0)
                .Max();
            var chain = reached
                .SelectMany(Used)
                .Where(other => other.Project != project)
                .Select(other => _workingRegions.Single(region => region.Project == other.Project.Name).ReachBytes)
                .DefaultIfEmpty(0)
                .Max();
            return (own, own + chain);
        }

        // Where the buffers of a procedure that has a place end.
        private long EndOf((Source Source, string Name) procedure) =>
            _places[procedure] + WorkingBuffersPass.PieceBytes(procedure.Source.Methods[procedure.Name].Method.StackBytes);

        // A procedure starts where the buffers of its deepest caller in its
        // own project end. FORTRAN 77 has no recursion, so the calls form a
        // graph without cycles, and a place is the longest way down to the
        // procedure from any that nothing of the project calls.
        //
        // Only calls by name within one project count. A call into another
        // project leaves this region for that project's, which lies after
        // it. A procedure called through an argument runs in a copy of the
        // arena of its own, since the runtime opens one for every such call,
        // so neither it nor what it calls can meet a buffer of the chain that
        // led to it.
        private void WorkOutPlaces()
        {
            // A project read from its summary brings its sizes with it.
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

            // What a project can need when it is the first to be called: its
            // own region and, after it, the most any chain of the projects
            // it refers to needs.
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
                var end = EndOf(procedure);
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

            var unplaced = callers.Where(pair => pair.Value > 0).Select(pair => pair.Key).ToList();
            foreach (var procedure in unplaced)
            {
                _places.Remove(procedure);
            }

            foreach (var procedure in unplaced)
            {
                _whyOnStack[procedure] = ExplainStack(procedure, unplaced, callees);
            }

            return size;
        }

        // Why a procedure that has no place keeps its buffers on the stack,
        // for a comment above them: the chain of calls that comes back to it,
        // or, for one beneath such a chain, the chain it is called from.
        private static List<string> ExplainStack(
            (Source Source, string Name) procedure,
            List<(Source Source, string Name)> unplaced,
            Dictionary<(Source Source, string Name), HashSet<(Source Source, string Name)>> callees)
        {
            if (FindCycle(procedure, callees) is { } cycle)
            {
                return
                [
                    $"{procedure.Name} lies on a chain of calls that comes back to itself, {string.Join(" → ", cycle)},",
                    "so its buffers are on the stack whatever the context chooses: a place in the",
                    "arena could not serve two calls of it running at once.",
                ];
            }

            foreach (var other in unplaced.OrderBy(other => other.Name, StringComparer.Ordinal))
            {
                if (FindCycle(other, callees) is { } around && FindPath(other, procedure, callees) is not null)
                {
                    return
                    [
                        $"{procedure.Name} is called from a chain of calls that comes back to itself, {string.Join(" → ", around)},",
                        "so its buffers are on the stack whatever the context chooses, as that chain's are.",
                    ];
                }
            }

            return [];
        }

        // The shortest chain of calls from a procedure back to itself, by the
        // names of those on it, the first again at the end; nothing where it
        // is on none.
        private static List<string>? FindCycle(
            (Source Source, string Name) procedure,
            Dictionary<(Source Source, string Name), HashSet<(Source Source, string Name)>> callees) =>
            FindPath(procedure, procedure, callees);

        // The shortest chain of calls from one procedure to another, by the
        // names of those on it, both ends among them; nothing where none
        // reaches it. The callees are taken in the order of their names, so
        // that the same program gives the same chain.
        private static List<string>? FindPath(
            (Source Source, string Name) from,
            (Source Source, string Name) to,
            Dictionary<(Source Source, string Name), HashSet<(Source Source, string Name)>> callees)
        {
            var cameFrom = new Dictionary<(Source Source, string Name), (Source Source, string Name)>();
            var pending = new Queue<(Source Source, string Name)>();
            pending.Enqueue(from);
            while (pending.TryDequeue(out var next))
            {
                foreach (var callee in callees[next].OrderBy(callee => callee.Name, StringComparer.Ordinal))
                {
                    if (callee == to)
                    {
                        var chain = new List<string> { to.Name };
                        for (var at = next; at != from; at = cameFrom[at])
                        {
                            chain.Add(at.Name);
                        }

                        chain.Add(from.Name);
                        chain.Reverse();
                        return chain;
                    }

                    if (callee != from && cameFrom.TryAdd(callee, next))
                    {
                        pending.Enqueue(callee);
                    }
                }
            }

            return null;
        }
    }
}
