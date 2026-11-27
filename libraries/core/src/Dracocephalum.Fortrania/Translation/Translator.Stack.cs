using Dracocephalum.Fortrania.Analysis;

namespace Dracocephalum.Fortrania.Translation;

/// <content>
/// Works out the most each procedure of a translation can take from the
/// stack, from what the emitter says each method takes and calls.
/// </content>
public static partial class Translator
{
    private sealed partial class Translation
    {
        // FORTRAN 77 has no recursion, so the calls of a program form a graph
        // without cycles, and the deepest chain beneath a procedure is its own
        // bytes and its deepest callee's, each worked out once. The procedures
        // on the chain being followed are kept, so that a program that does
        // recurse is told apart rather than followed for ever.
        private readonly Dictionary<(Source Source, string Name), Depth> _depths = [];
        private readonly Dictionary<(Source Source, string Name), Depth?> _throughDepths = [];
        private readonly HashSet<(Source Source, string Name)> _onChain = [];
        private readonly HashSet<(Source Source, string Name)> _onThroughChain = [];

        private void WorkOutStackUse()
        {
            WorkOutPlaces();
            foreach (var source in _sources)
            {
                foreach (var (name, (unit, method)) in source.Methods)
                {
                    var depth = DepthOf(source, name);

                    // A variant's method, or the base's, is known by a name
                    // derived from its procedure's.
                    var platform = source.PlatformOf(unit);
                    var heading = unit.Summary.EntryPoints.FirstOrDefault(entry => entry.Name == name || platform?.GetMethodName(entry.Name) == name);
                    var takesProcedures = heading is not null && heading.Arguments.Any(argument =>
                        argument.Kind is DummyArgumentKind.Function or DummyArgumentKind.Subroutine or DummyArgumentKind.Procedure);
                    _stackUse.Add(new StackUse(
                        source.Project.Name,
                        source.Settings.Name,
                        name,
                        method.StackBytes,
                        depth.Bytes,
                        depth.Chain,
                        takesProcedures,
                        depth.IsRecursive)
                    {
                        Place = _places.TryGetValue((source, name), out var place) ? place : null,
                    });
                }
            }

            _stackUse.Sort((left, right) =>
                (string.CompareOrdinal(left.Project, right.Project), string.CompareOrdinal(left.Source, right.Source), string.CompareOrdinal(left.Procedure, right.Procedure)) switch
                {
                    (not 0 and var project, _, _) => project,
                    (_, not 0 and var source, _) => source,
                    (_, _, var procedure) => procedure,
                });
        }

        // A call reaches as deep as its callee does, or, where it passes
        // procedures, as deep as the point beneath the callee where one it
        // was passed is called, with the deepest of those passed on top.
        // Which of its procedure arguments the callee calls there is not
        // told apart, so the figure is an upper bound.
        private Depth DepthOf(Source source, string name)
        {
            var key = (source, name);
            if (_depths.TryGetValue(key, out var known))
            {
                return known;
            }

            if (source.Summarized.TryGetValue(name, out var summarized))
            {
                return _depths[key] = summarized.Deepest is { } deepestSummarized ? ToDepth(deepestSummarized) : new Depth(0, [], false);
            }

            if (!_onChain.Add(key))
            {
                return new Depth(0, [], IsRecursive: true);
            }

            var method = source.Methods[name].Method;
            Depth? deepest = null;
            var isRecursive = false;
            foreach (var call in method.Calls)
            {
                if (FindOwner(source, call.Name) is not { } owner)
                {
                    continue;
                }

                var reached = DepthOf(owner, call.Name);
                isRecursive |= reached.IsRecursive;
                if (call.Passes.Count > 0 && ThroughDepthOf(owner, call.Name) is { } through && FindDeepest(source, call.Passes) is { } passed)
                {
                    isRecursive |= through.IsRecursive || passed.IsRecursive;
                    if (through.Bytes + passed.Bytes > reached.Bytes)
                    {
                        reached = new Depth(through.Bytes + passed.Bytes, [.. through.Chain, .. passed.Chain], false);
                    }
                }

                if (deepest is null || reached.Bytes > deepest.Bytes)
                {
                    deepest = reached;
                }
            }

            _onChain.Remove(key);
            var depth = new Depth(method.StackBytes + (deepest?.Bytes ?? 0), [name, .. deepest?.Chain ?? []], isRecursive);
            _depths[key] = depth;
            return depth;
        }

        // How deep beneath a procedure one it was passed can be called: at
        // its own level where it calls one, and beneath a callee it passes
        // one on to. Null where it never calls what it is passed.
        private Depth? ThroughDepthOf(Source source, string name)
        {
            var key = (source, name);
            if (_throughDepths.TryGetValue(key, out var known))
            {
                return known;
            }

            if (source.Summarized.TryGetValue(name, out var summarized))
            {
                return _throughDepths[key] = summarized.Through is { } through ? ToDepth(through) : null;
            }

            if (!_onThroughChain.Add(key))
            {
                return null;
            }

            var method = source.Methods[name].Method;
            var deepest = method.CallsThroughArgument ? new Depth(0, [], false) : null;
            foreach (var call in method.Calls.Where(call => call.PassesOnArgument))
            {
                if (FindOwner(source, call.Name) is { } owner && ThroughDepthOf(owner, call.Name) is { } through && (deepest is null || through.Bytes > deepest.Bytes))
                {
                    deepest = through;
                }
            }

            _onThroughChain.Remove(key);
            var depth = deepest is null ? null : new Depth(method.StackBytes + deepest.Bytes, [name, .. deepest.Chain], deepest.IsRecursive);
            _throughDepths[key] = depth;
            return depth;
        }

        // The deepest of the procedures named that the translation has, or
        // null where it has none of them.
        private Depth? FindDeepest(Source from, IEnumerable<string> names)
        {
            Depth? deepest = null;
            foreach (var name in names)
            {
                if (FindOwner(from, name) is not { } owner)
                {
                    continue;
                }

                var depth = DepthOf(owner, name);
                if (deepest is null || depth.Bytes > deepest.Bytes)
                {
                    deepest = depth;
                }
            }

            return deepest;
        }

        // The source whose procedure a call reaches, in the order the
        // emitter looks: the calling source, then the others, its own
        // project's first. None for the compiler's library, which is the
        // runtime's, or a procedure no source defines, warned of already.
        private Source? FindOwner(Source from, string name) =>
            HasMethod(from, name) ? from : Visible(from).FirstOrDefault(other => HasMethod(other, name));

        // A procedure translated this time has a method; one read from a
        // summary has what its method took, where it had one.
        private static bool HasMethod(Source source, string name) =>
            source.Methods.ContainsKey(name) || (source.Summarized.TryGetValue(name, out var summarized) && summarized.Deepest is not null);

        private sealed record Depth(long Bytes, IReadOnlyList<string> Chain, bool IsRecursive);
    }
}
