namespace Dracocephalum.Fortrania.Syntax;

/// <summary>
/// One item in the list an input or output statement transfers. The two
/// kinds are nested here and nothing else can derive from this type.
/// </summary>
public abstract record IOItem
{
    private IOItem()
    {
    }

    /// <summary>A single item: any expression to write, or a variable, array element, substring or array to read.</summary>
    /// <param name="Expression">The item.</param>
    public sealed record Value(Expression Expression) : IOItem;

    /// <summary>An implied <c>DO</c>: <c>( A(I), I = 1, N )</c>.</summary>
    /// <param name="Items">The items repeated, which may themselves be implied loops.</param>
    /// <param name="ControlVariable">The loop variable, in upper case.</param>
    /// <param name="First">Its first value.</param>
    /// <param name="Last">Its last value.</param>
    /// <param name="Step">Its increment, or <see langword="null"/> when omitted, which means one.</param>
    public sealed record ImpliedDo(
        IReadOnlyList<IOItem> Items,
        string ControlVariable,
        Expression First,
        Expression Last,
        Expression? Step) : IOItem;
}
