namespace Dracocephalum.Fortrania.Translation;

/// <content>
/// Gives each procedure of a translation a place for its working buffers
/// in one block that the whole program shares, so that procedures that can
/// be running at the same time never share bytes.
/// </content>
public static partial class Translator
{
    private sealed partial class Translation
    {
        // Where each procedure's buffers start in the block. One that has
        // none is given a place all the same, since its callees start
        // after its callers' buffers as well as its own.
        private readonly Dictionary<(Source Source, string Name), long> _places = [];
        private readonly Dictionary<(Source Source, string Name), HashSet<(Source Source, string Name)>> _throughPoints = [];
        private long _workingBlockBytes;

        // A procedure starts where the buffers of its deepest caller end.
        // FORTRAN 77 has no recursion, so the calls form a graph without
        // cycles, and a place is the longest way down to the procedure
        // from any that nothing calls. A procedure passed as an argument
        // is called from wherever the procedure it was passed to, or one
        // that one passed it on to, calls an argument; which argument is
        // called there is not told apart, so it is taken to be any of
        // them. That can only put a procedure further down than it need
        // be, never where a running one's buffers lie.
        private void WorkOutPlaces()
        {
            var callees = new Dictionary<(Source Source, string Name), HashSet<(Source Source, string Name)>>();
            var callers = new Dictionary<(Source Source, string Name), int>();
            foreach (var source in _sources)
            {
                foreach (var name in source.Methods.Keys)
                {
                    callees[(source, name)] = [];
                    callers[(source, name)] = 0;
                }
            }

            void Calls((Source Source, string Name) from, (Source Source, string Name) to)
            {
                if (callees[from].Add(to))
                {
                    callers[to]++;
                }
            }

            foreach (var source in _sources)
            {
                foreach (var (name, (_, method)) in source.Methods)
                {
                    foreach (var call in method.Calls)
                    {
                        if (FindOwner(source, call.Name) is not { } owner)
                        {
                            continue;
                        }

                        Calls((source, name), (owner, call.Name));
                        foreach (var passed in call.Passes)
                        {
                            if (FindOwner(source, passed) is not { } passedOwner)
                            {
                                continue;
                            }

                            foreach (var point in ThroughPointsOf(owner, call.Name))
                            {
                                Calls(point, (passedOwner, passed));
                            }
                        }
                    }
                }
            }

            // Each procedure once all that call it are placed. What is left
            // when no such procedure remains is on a chain that comes back
            // to itself, or beneath one, and is given no place.
            var ready = new Queue<(Source Source, string Name)>(callers.Where(pair => pair.Value == 0).Select(pair => pair.Key));
            foreach (var root in ready)
            {
                _places[root] = 0;
            }

            while (ready.TryDequeue(out var procedure))
            {
                var end = _places[procedure] + procedure.Source.Methods[procedure.Name].Method.StackBytes;
                _workingBlockBytes = Math.Max(_workingBlockBytes, end);
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
        }

        // The procedures in which an argument passed to a procedure may be
        // called: the procedure itself where it calls one, and those of
        // every procedure it passes an argument on to.
        private HashSet<(Source Source, string Name)> ThroughPointsOf(Source source, string name)
        {
            var key = (source, name);
            if (_throughPoints.TryGetValue(key, out var known))
            {
                return known;
            }

            // Set before the walk goes on, so that a chain that comes back
            // here finds it and stops.
            var points = new HashSet<(Source Source, string Name)>();
            _throughPoints[key] = points;

            var method = source.Methods[name].Method;
            if (method.CallsThroughArgument)
            {
                points.Add(key);
            }

            foreach (var call in method.Calls.Where(call => call.PassesOnArgument))
            {
                if (FindOwner(source, call.Name) is { } owner)
                {
                    points.UnionWith(ThroughPointsOf(owner, call.Name));
                }
            }

            return points;
        }
    }
}
