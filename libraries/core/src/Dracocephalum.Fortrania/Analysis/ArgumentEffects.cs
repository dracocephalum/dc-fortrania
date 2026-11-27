namespace Dracocephalum.Fortrania.Analysis;

/// <summary>
/// What each procedure of a program does with each of its arguments, counting
/// what the procedures it passes them on to do, however far down.
/// </summary>
public sealed class ArgumentEffects
{
    private const ArgumentUses Both = ArgumentUses.Read | ArgumentUses.Written;

    private readonly Dictionary<string, ArgumentUses[]> _byProcedure;

    private ArgumentEffects(Dictionary<string, ArgumentUses[]> byProcedure) => _byProcedure = byProcedure;

    /// <summary>Works out the effects of every procedure the program units define.</summary>
    /// <remarks>
    /// The answer errs toward more, never less: an argument is taken as both
    /// read and written when it is passed to a procedure neither
    /// <paramref name="units"/> nor <paramref name="findElsewhere"/> knows,
    /// or in a place that procedure has no argument for.
    /// </remarks>
    /// <param name="units">The summaries of the program units, in any order.</param>
    /// <param name="findElsewhere">
    /// Gives the effects of a procedure defined outside <paramref name="units"/>,
    /// such as one of a library worked out before, or <see langword="null"/>
    /// when it knows none of that name. May itself be <see langword="null"/>.
    /// </param>
    /// <param name="variants">
    /// The summaries of units that are variants of some of
    /// <paramref name="units"/>, one for each platform: a procedure of one
    /// is the procedure of the same name, which does with an argument what
    /// any of its variants does with it. None unless given.
    /// </param>
    /// <returns>The effects, by procedure.</returns>
    /// <exception cref="FortranAnalysisException">Two units define a procedure of the same name, or a variant one that no unit defines or with another number of arguments.</exception>
    public static ArgumentEffects Compute(
        IEnumerable<UnitSummary> units,
        Func<string, IReadOnlyList<ArgumentUses>?>? findElsewhere = null,
        IEnumerable<UnitSummary>? variants = null)
    {
        ArgumentNullException.ThrowIfNull(units);

        var procedures = new List<(ProcedureInterface Procedure, ArgumentUses[] Uses)>();
        var byName = new Dictionary<string, ArgumentUses[]>(StringComparer.Ordinal);

        foreach (var entry in units.SelectMany(unit => unit.EntryPoints))
        {
            var uses = entry.Arguments.Select(argument => argument.Uses).ToArray();
            if (!byName.TryAdd(entry.Name, uses))
            {
                throw new FortranAnalysisException(entry.Line, null, $"{entry.Name} is defined by another program unit as well.");
            }

            procedures.Add((entry, uses));
        }

        // A variant's procedure shares the answers of the procedure it is a
        // variant of, so what it does and what it passes on add to them.
        foreach (var entry in (variants ?? []).SelectMany(unit => unit.EntryPoints))
        {
            if (!byName.TryGetValue(entry.Name, out var uses) || uses.Length != entry.Arguments.Count)
            {
                throw new FortranAnalysisException(entry.Line, null, $"A variant of {entry.Name} is not the procedure of that name it is to stand in for.");
            }

            for (var index = 0; index < uses.Length; index++)
            {
                uses[index] |= entry.Arguments[index].Uses;
            }

            procedures.Add((entry, uses));
        }

        // What a procedure does to an argument it passes on is what the one
        // called does to its own, so the answers are carried from callee to
        // caller until none grows any more.
        bool grew;
        do
        {
            grew = false;
            foreach (var (procedure, uses) in procedures)
            {
                for (var index = 0; index < uses.Length; index++)
                {
                    var before = uses[index];
                    var after = before;
                    foreach (var pass in procedure.Arguments[index].PassedTo)
                    {
                        after |= UsesOf(pass);
                    }

                    if (after != before)
                    {
                        uses[index] = after;
                        grew = true;
                    }
                }
            }
        }
        while (grew);

        return new ArgumentEffects(byName);

        ArgumentUses UsesOf(ArgumentPass pass)
        {
            IReadOnlyList<ArgumentUses>? callee = byName.GetValueOrDefault(pass.Procedure);
            callee ??= findElsewhere?.Invoke(pass.Procedure);
            return callee is not null && pass.Index < callee.Count ? callee[pass.Index] : Both;
        }
    }

    /// <summary>Looks a procedure up.</summary>
    /// <param name="name">The name it is called by, in upper case.</param>
    /// <returns>
    /// What it does with each argument, in order, or <see langword="null"/>
    /// when no unit given defines it. An argument that is a procedure has
    /// <see cref="ArgumentUses.None"/>.
    /// </returns>
    public IReadOnlyList<ArgumentUses>? Find(string name) => _byProcedure.GetValueOrDefault(name);
}
