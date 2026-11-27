namespace Dracocephalum.Fortrania.Translation;

/// <content>
/// Tells whether a call may come back into the program unit that makes it:
/// reach another of the unit's entry points, or the unit again. A method
/// keeps in locals what its unit keeps between calls, and has to store and
/// read them around such a call, since what comes back reads and stores
/// the same variables.
/// </content>
public static partial class Translator
{
    private sealed partial class Translation
    {
        // What each procedure of a project reaches inside the project, by
        // the source that defines it and its name, worked out when first
        // asked for.
        private readonly Dictionary<(Source Source, string Name), Reach> _reaches = [];

        // FORTRAN 77 forbids a procedure to call itself by any way round,
        // and programs do it all the same where an error is reported from
        // inside what reports errors; so the calls are followed, and
        // nothing is taken on trust. A project is translated the same
        // whichever others are translated with it, so only its own calls
        // are followed:
        //
        // - A procedure of another project cannot call into this one,
        //   which it does not know of, unless it is passed a procedure to
        //   call.
        // - A call through a procedure argument reaches whatever was
        //   passed, of which nothing is known here: another project may
        //   pass any procedure of this one.
        // - A procedure the translation does not have may do anything.
        private bool MayReenter(Source source, string caller, string callee)
        {
            if (!source.Methods.TryGetValue(caller, out var calling))
            {
                return true;
            }

            if (FindOwner(source, callee) is not { } owner)
            {
                return true;
            }

            if (IsOutside(source, owner))
            {
                return calling.Method.Calls.Any(call => call.Name == callee && (call.Passes.Count > 0 || call.PassesOnArgument));
            }

            var reach = ReachOf(owner, callee);
            return reach.IsOpen || reach.Units.Contains(calling.Unit);
        }

        private static bool IsOutside(Source source, Source owner) => owner.Project != source.Project || owner.IsSummarized;

        // Every unit a procedure can reach by calls inside its project,
        // its own among them, and whether one of those calls goes where
        // it cannot be followed.
        private Reach ReachOf(Source source, string name)
        {
            if (_reaches.TryGetValue((source, name), out var known))
            {
                return known;
            }

            var units = new HashSet<Unit>(ReferenceEqualityComparer.Instance);
            var isOpen = false;
            var seen = new HashSet<(Source Source, string Name)> { (source, name) };
            var waiting = new Queue<(Source Source, string Name)>(seen);
            while (waiting.TryDequeue(out var next) && !isOpen)
            {
                if (!next.Source.Methods.TryGetValue(next.Name, out var reached))
                {
                    isOpen = true;
                    break;
                }

                units.Add(reached.Unit);
                isOpen |= reached.Method.CallsThroughArgument;
                foreach (var call in reached.Method.Calls)
                {
                    if (FindOwner(next.Source, call.Name) is not { } owner)
                    {
                        isOpen = true;
                    }
                    else if (IsOutside(source, owner))
                    {
                        isOpen |= call.Passes.Count > 0 || call.PassesOnArgument;
                    }
                    else if (seen.Add((owner, call.Name)))
                    {
                        waiting.Enqueue((owner, call.Name));
                    }
                }
            }

            return _reaches[(source, name)] = new Reach(units, isOpen);
        }

        // What a procedure reaches inside its project.
        private sealed record Reach(HashSet<Unit> Units, bool IsOpen);
    }
}
