namespace Dracocephalum.Fortrania.Analysis;

/// <summary>What a name stands for throughout one program unit.</summary>
public enum SymbolKind
{
    /// <summary>
    /// A variable or an array, a dummy argument that is one, or the name of a
    /// function or of one of its entries, which holds the result inside it.
    /// </summary>
    Variable,

    /// <summary>A named constant, given its value by <c>PARAMETER</c>.</summary>
    Constant,

    /// <summary>A statement function, defined in the unit itself.</summary>
    StatementFunction,

    /// <summary>A function the language supplies.</summary>
    IntrinsicFunction,

    /// <summary>A function defined in another program unit, or passed in as a dummy argument.</summary>
    ExternalFunction,

    /// <summary>
    /// A subroutine: one that is called, one passed in as a dummy argument
    /// and called, or the subroutine the unit itself defines and its entries.
    /// </summary>
    Subroutine,

    /// <summary>
    /// A name declared <c>EXTERNAL</c> that the unit only passes on. Nothing
    /// in the unit says whether it is a function or a subroutine.
    /// </summary>
    ExternalProcedure,
}
