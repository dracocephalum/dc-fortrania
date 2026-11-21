namespace Dracocephalum.Fortrania.Analysis;

/// <summary>
/// The procedures of a program, by the names they are called by: every
/// subroutine and function of the units added, and every entry of theirs.
/// </summary>
public sealed class ProcedureCatalogue
{
    private readonly Dictionary<string, ProcedureInterface> _byName = new(StringComparer.Ordinal);

    /// <summary>Gets the number of names the catalogue holds.</summary>
    public int Count => _byName.Count;

    /// <summary>Adds the entry points of a program unit.</summary>
    /// <param name="unit">The summary of a program unit; a main program or block data adds nothing.</param>
    /// <exception cref="FortranAnalysisException">A unit added before defines a procedure of the same name.</exception>
    public void Add(UnitSummary unit)
    {
        ArgumentNullException.ThrowIfNull(unit);

        foreach (var entry in unit.EntryPoints)
        {
            if (!_byName.TryAdd(entry.Name, entry))
            {
                throw new FortranAnalysisException(entry.Line, null, $"{entry.Name} is defined by another program unit as well.");
            }
        }
    }

    /// <summary>Looks a procedure up.</summary>
    /// <param name="name">The name it is called by, in upper case.</param>
    /// <returns>How it is called, or <see langword="null"/> when no unit added defines it.</returns>
    public ProcedureInterface? Find(string name) => _byName.GetValueOrDefault(name);
}
