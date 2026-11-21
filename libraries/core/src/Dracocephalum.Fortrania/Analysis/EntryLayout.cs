using Dracocephalum.Fortrania.Syntax;

namespace Dracocephalum.Fortrania.Analysis;

/// <summary>
/// How the entry points of a subroutine or function divide its statements
/// among themselves.
/// </summary>
/// <remarks>
/// Control is taken to run down the unit's own list of statements: in at an
/// entry point, on through any <c>ENTRY</c> after it, and out at a
/// <c>RETURN</c> or <c>STOP</c> that stands in no block. That is all there
/// is while the language as read has no <c>GO TO</c>.
/// </remarks>
public sealed class EntryLayout
{
    private EntryLayout(IReadOnlyList<EntryBody> bodies, bool isDisjoint)
    {
        Bodies = bodies;
        IsDisjoint = isDisjoint;
    }

    /// <summary>
    /// Gets the entry points with what each reaches: the unit's own first
    /// statement, then each <c>ENTRY</c> in order. Empty for a main program
    /// or block data.
    /// </summary>
    public IReadOnlyList<EntryBody> Bodies { get; }

    /// <summary>
    /// Gets a value indicating whether no statement is reached from two
    /// entry points, so that each can be translated as a procedure of its
    /// own. It fails when control can run on from one entry point's
    /// statements into an <c>ENTRY</c>.
    /// </summary>
    public bool IsDisjoint { get; }

    /// <summary>Works out the layout of the program unit <paramref name="table"/> describes.</summary>
    /// <param name="table">The names of a program unit.</param>
    /// <returns>Its layout.</returns>
    public static EntryLayout Of(SymbolTable table)
    {
        ArgumentNullException.ThrowIfNull(table);

        var unit = table.Unit;
        var bodies = new List<(ParsedStatement Entry, List<Construct> Statements)>();
        var open = new List<List<Construct>>();
        var isDisjoint = true;

        if (unit.Header is { Statement: Statement.Subroutine or Statement.Function } header)
        {
            Open(header);
        }

        foreach (var construct in unit.Body)
        {
            if (construct is Construct.Simple { Statement: { Statement: Statement.Entry } entry })
            {
                isDisjoint &= open.Count == 0;
                Open(entry);
                continue;
            }

            foreach (var statements in open)
            {
                statements.Add(construct);
            }

            if (construct is Construct.Simple { Statement.Statement: Statement.Return or Statement.Stop })
            {
                open.Clear();
            }
        }

        return new EntryLayout([.. bodies.Select(body => Describe(table, body.Entry, body.Statements))], isDisjoint);

        void Open(ParsedStatement entry)
        {
            var statements = new List<Construct>();
            bodies.Add((entry, statements));
            open.Add(statements);
        }
    }

    private static EntryBody Describe(SymbolTable table, ParsedStatement entry, List<Construct> statements)
    {
        var own = entry.Statement.ProcedureHeading().Parameters;

        // A statement function's definition is a declaration, for every
        // entry point alike, and its own arguments are not the unit's.
        var stray = statements
            .Where(construct => !IsStatementFunctionDefinition(table, construct))
            .SelectMany(NameWalker.NamesIn)
            .Distinct(StringComparer.Ordinal)
            .Where(name => table.Find(name) is { IsDummyArgument: true } && !own.Contains(name, StringComparer.Ordinal))
            .ToList();

        return new EntryBody(entry, statements, stray);
    }

    private static bool IsStatementFunctionDefinition(SymbolTable table, Construct construct) =>
        construct is Construct.Simple { Statement.Statement: Statement.Assignment { Target: Expression.Reference target } }
        && table.Find(target.Identifier)?.Kind == SymbolKind.StatementFunction;
}
