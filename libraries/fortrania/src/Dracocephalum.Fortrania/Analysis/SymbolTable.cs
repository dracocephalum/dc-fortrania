using Dracocephalum.Fortrania.Syntax;

namespace Dracocephalum.Fortrania.Analysis;

/// <summary>
/// Everything one program unit declares, with the include files it names
/// already drawn in.
/// </summary>
public sealed class SymbolTable
{
    private readonly Dictionary<string, Symbol> _byName;

    internal SymbolTable(
        ProgramUnit unit,
        bool isImplicitNone,
        bool savesEverything,
        IReadOnlyList<Symbol> symbols,
        IReadOnlyList<ParsedStatement> entryPoints,
        IReadOnlyList<DataGroup> data,
        IReadOnlyList<IReadOnlyList<Expression>> equivalences,
        IReadOnlyList<ParsedStatement> statementFunctions)
    {
        Unit = unit;
        IsImplicitNone = isImplicitNone;
        SavesEverything = savesEverything;
        Symbols = symbols;
        EntryPoints = entryPoints;
        Data = data;
        Equivalences = equivalences;
        StatementFunctions = statementFunctions;
        _byName = symbols.ToDictionary(symbol => symbol.Name, StringComparer.Ordinal);
    }

    /// <summary>Gets the program unit the table describes.</summary>
    public ProgramUnit Unit { get; }

    /// <summary>Gets a value indicating whether the unit says <c>IMPLICIT NONE</c>, so that no name takes a type from its first letter.</summary>
    public bool IsImplicitNone { get; }

    /// <summary>Gets a value indicating whether the unit has a <c>SAVE</c> with no names, which saves every variable it may.</summary>
    public bool SavesEverything { get; }

    /// <summary>Gets the declared names, in the order they first appear.</summary>
    public IReadOnlyList<Symbol> Symbols { get; }

    /// <summary>
    /// Gets the ways into a subroutine or function: its own first statement,
    /// then each <c>ENTRY</c> in order. Empty for a main program or block data.
    /// </summary>
    public IReadOnlyList<ParsedStatement> EntryPoints { get; }

    /// <summary>Gets the groups of every <c>DATA</c> statement, in order.</summary>
    public IReadOnlyList<DataGroup> Data { get; }

    /// <summary>Gets the sets of every <c>EQUIVALENCE</c> statement, in order.</summary>
    public IReadOnlyList<IReadOnlyList<Expression>> Equivalences { get; }

    /// <summary>
    /// Gets the statement function definitions, in order. Each is a
    /// <see cref="Statement.Assignment"/> whose target names the function and
    /// lists its dummy arguments.
    /// </summary>
    public IReadOnlyList<ParsedStatement> StatementFunctions { get; }

    /// <summary>Looks a name up.</summary>
    /// <param name="name">A name, in upper case.</param>
    /// <returns>What is declared about it, or <see langword="null"/> when no declaration mentions it.</returns>
    public Symbol? Find(string name) => _byName.GetValueOrDefault(name);
}
