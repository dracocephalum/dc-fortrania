using Dracocephalum.Fortrania.Analysis;

namespace Dracocephalum.Fortrania.Emit;

/// <summary>
/// Writes an analysed program unit as C# source: one file that adds the
/// unit's procedures to the class the options name.
/// </summary>
public sealed class CSharpEmitter
{
    private readonly EmitOptions _options;
    private readonly Func<string, ProcedureInterface?> _findProcedure;

    /// <summary>Initializes a new instance of the <see cref="CSharpEmitter"/> class.</summary>
    /// <param name="options">What the emitted code is to be called.</param>
    /// <param name="findProcedure">
    /// Gives the procedure a name is called by, or <see langword="null"/>
    /// when the program does not define it. A call is written to suit the
    /// procedure it reaches where that is known, and by its own arguments
    /// alone where it is not.
    /// </param>
    public CSharpEmitter(EmitOptions options, Func<string, ProcedureInterface?> findProcedure)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(findProcedure);

        _options = options;
        _findProcedure = findProcedure;
    }

    /// <summary>Writes the program unit <paramref name="table"/> describes.</summary>
    /// <param name="table">The names of a program unit that has passed the type check.</param>
    /// <returns>The text of a C# source file.</returns>
    /// <exception cref="UnsupportedConstructException">The unit uses something the emitter cannot translate yet.</exception>
    public string Emit(SymbolTable table)
    {
        ArgumentNullException.ThrowIfNull(table);

        return new UnitEmitter(table, _options, _findProcedure).Emit();
    }
}
