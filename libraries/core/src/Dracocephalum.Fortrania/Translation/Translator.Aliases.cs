using Dracocephalum.Fortrania.Analysis;
using Dracocephalum.Fortrania.Emit;
using Dracocephalum.Fortrania.Syntax;

namespace Dracocephalum.Fortrania.Translation;

/// <content>
/// Finds the arguments of a translation's procedures that a call may pass
/// the same storage as another, which FORTRAN 77 forbids where either is
/// given a value. A method keeps its scalar arguments in locals by that
/// rule, so an argument found here stays behind its address.
/// </content>
public static partial class Translator
{
    private sealed partial class Translation
    {
        // The arguments left behind their addresses, by the source that
        // defines the procedure, the name it is called by and the
        // argument's place counted from zero.
        private readonly HashSet<(Source Source, string Name, int Position)> _aliasedArguments = [];

        // The procedures of each project that are passed as arguments in
        // it, and the projects in which a call through an argument passes
        // one storage twice. What such a call reaches is whichever
        // procedure was passed, so in such a project every argument of
        // every procedure that is passed is left behind its address.
        private readonly HashSet<(Source Source, string Name)> _passedProcedures = [];
        private readonly HashSet<Project> _aliasingThroughArguments = [];
        private readonly List<AliasedPlace> _aliased = [];

        private bool IsAliased(Source source, string name, int position) =>
            _aliasedArguments.Contains((source, name, position))
            || (_aliasingThroughArguments.Contains(source.Project) && _passedProcedures.Contains((source, name)));

        // Every call of the translation is looked at once: two of its
        // arguments that are one variable, an array and an element of it,
        // or names an EQUIVALENCE lays over each other, where the procedure
        // called gives either a value, itself or by what it passes it on
        // to, and either is an argument a method may keep in a local.
        //
        // Only a procedure of the calling unit's own project is written
        // otherwise for it. A project is translated the same whichever
        // others are translated with it, so a call from another project
        // changes nothing in the one called, and is warned of.
        private void WorkOutAliases()
        {
            foreach (var source in _sources.Where(source => !source.IsSummarized))
            {
                foreach (var passed in source.Methods.Values.SelectMany(method => method.Method.Calls).SelectMany(call => call.Passes).Distinct(StringComparer.Ordinal))
                {
                    if (FindOwner(source, passed) is { } owner && owner.Project == source.Project)
                    {
                        _passedProcedures.Add((owner, passed));
                    }
                }

                foreach (var unit in source.Units)
                {
                    var storage = StorageOf(unit.Table);

                    // A call of a procedure the unit was passed reaches
                    // whatever its caller passed, of which nothing is known:
                    // it may give any argument a value.
                    foreach (var call in unit.Summary.CallsThroughArguments)
                    {
                        foreach (var (first, second) in FindPairs(call, storage, (_, _) => true))
                        {
                            _aliasingThroughArguments.Add(source.Project);
                            Note(source, unit, call, first, second);
                        }
                    }

                    foreach (var call in unit.Summary.Calls)
                    {
                        if (FindOwner(source, call.Name) is { } owner && EffectsOf(owner, call.Name) is { } effects)
                        {
                            foreach (var (first, second) in FindPairs(call, storage, (first, second) =>
                                IsWritten(effects, first, second) && (IsKeptInALocal(owner, call.Name, first) || IsKeptInALocal(owner, call.Name, second))))
                            {
                                Note(source, unit, call, first, second);
                                if (owner.Project == source.Project && !owner.IsSummarized)
                                {
                                    _aliasedArguments.Add((owner, call.Name, first));
                                    _aliasedArguments.Add((owner, call.Name, second));
                                }
                                else
                                {
                                    Warning(
                                        $"{unit.Name} passes {call.Arguments[first].Name} as arguments {first + 1} and {second + 1} of {call.Name}, which may give one of them a value. FORTRAN 77 forbids it, and {call.Name} is translated in another project, {owner.Project.Name}, to rely on that; pass a copy, or translate that project with \"{ScalarArgumentPass.Setting}\": true.",
                                        unit.File,
                                        call.Line);
                                }
                            }
                        }
                    }
                }
            }

            _aliased.Sort((left, right) => (string.CompareOrdinal(left.File, right.File), left.Line.CompareTo(right.Line), left.FirstArgument.CompareTo(right.FirstArgument)) switch
            {
                (not 0 and var file, _, _) => file,
                (_, not 0 and var line, _) => line,
                (_, _, not 0 and var argument) => argument,
                _ => left.SecondArgument.CompareTo(right.SecondArgument),
            });
        }

        private void Note(Source source, Unit unit, CallSite call, int first, int second) => _aliased.Add(new AliasedPlace(
            source.Project.Name, source.Settings.Name, unit.Name, unit.File, call.Arguments[first].Name!, call.Name, first + 1, second + 1, call.Line));

        // The pairs of a call's arguments that are the same storage and
        // that matter to the procedure called, by their places counted from
        // zero.
        private static IEnumerable<(int First, int Second)> FindPairs(CallSite call, Func<string, string> storage, Func<int, int, bool> matters)
        {
            for (var first = 0; first < call.Arguments.Count; first++)
            {
                for (var second = first + 1; second < call.Arguments.Count; second++)
                {
                    if (call.Arguments[first] is { Kind: not (ActualArgumentKind.Value or ActualArgumentKind.Procedure), Name: not null }
                        && call.Arguments[second] is { Kind: not (ActualArgumentKind.Value or ActualArgumentKind.Procedure), Name: not null }
                        && IsSameStorage(call.Arguments[first], call.Arguments[second], storage)
                        && matters(first, second))
                    {
                        yield return (first, second);
                    }
                }
            }
        }

        // Whether two arguments are seen to be the same storage: one
        // variable, an array and an element of it, names an EQUIVALENCE
        // lays over each other, or two elements of an array written with
        // the same subscripts. Two elements written differently are taken
        // for two, as the language asks of the program wherever one is
        // given a value, and as its compilers take them.
        private static bool IsSameStorage(ActualArgument first, ActualArgument second, Func<string, string> storage) =>
            storage(first.Name!) == storage(second.Name!)
            && !(first.Kind == ActualArgumentKind.ArrayElement && second.Kind == ActualArgumentKind.ArrayElement
                && first.Name == second.Name && first.Subscripts != second.Subscripts);

        // An argument the procedure has none for is taken as written, as
        // the effects themselves take it.
        private static bool IsWritten(IReadOnlyList<ArgumentUses> effects, int first, int second) =>
            first >= effects.Count || second >= effects.Count
            || effects[first].HasFlag(ArgumentUses.Written) || effects[second].HasFlag(ArgumentUses.Written);

        // Whether an argument of a procedure is one a method may keep in a
        // local: one number or logical value. An array and a character value
        // are always behind their addresses, so two of those that are the
        // same storage are read and written as the FORTRAN reads and writes
        // them, and are no concern here; many a library allows it of its
        // character arguments, to change text in place.
        private static bool IsKeptInALocal(Source owner, string name, int position)
        {
            var (kind, type, rank) = owner.Summarized.TryGetValue(name, out var summarized)
                ? position < summarized.Arguments.Count ? (summarized.Arguments[position].Kind, summarized.Arguments[position].Type, summarized.Arguments[position].Rank) : default
                : owner.Catalogue.Find(name) is { } found && position < found.Arguments.Count
                    ? (found.Arguments[position].Kind, found.Arguments[position].Type, found.Arguments[position].Rank)
                    : (DummyArgumentKind.Array, null, 0);
            return kind == DummyArgumentKind.Variable && rank == 0 && type is not (null or DataType.Character);
        }

        // What a procedure does with each of its arguments, counting what
        // it passes them on to: worked out for one translated now, and read
        // from its summary otherwise.
        private IReadOnlyList<ArgumentUses>? EffectsOf(Source owner, string name) =>
            owner.Summarized.TryGetValue(name, out var summarized)
                ? [.. summarized.Arguments.Select(argument => argument.Uses)]
                : _effects.TryGetValue(owner.Project, out var effects) ? effects.Find(name) : null;

        // Gives the storage a name of a unit stands for: itself, or one
        // name for all that an EQUIVALENCE lays over each other, however
        // the statements chain them.
        private static Func<string, string> StorageOf(SymbolTable table)
        {
            var roots = new Dictionary<string, string>(StringComparer.Ordinal);
            string Find(string name) => roots.TryGetValue(name, out var root) && root != name ? roots[name] = Find(root) : name;

            foreach (var set in table.Equivalences)
            {
                var names = set.Select(NameOf).OfType<string>().ToList();
                foreach (var name in names.Skip(1))
                {
                    var (from, to) = (Find(name), Find(names[0]));
                    if (from != to)
                    {
                        roots[from] = to;
                    }
                }
            }

            return Find;
        }

        private static string? NameOf(Expression expression) => expression switch
        {
            Expression.Name name => name.Identifier,
            Expression.Reference reference => reference.Identifier,
            Expression.Substring substring => NameOf(substring.Target),
            Expression.Parenthesized inner => NameOf(inner.Inner),
            _ => null,
        };
    }
}
