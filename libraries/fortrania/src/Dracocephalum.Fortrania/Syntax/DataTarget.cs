namespace Dracocephalum.Fortrania.Syntax;

/// <summary>
/// One item in the list of names a <c>DATA</c> statement initializes. The two
/// kinds are nested here and nothing else can derive from this type.
/// </summary>
public abstract record DataTarget
{
    private DataTarget()
    {
    }

    /// <summary>A variable, an array element, a substring, or a whole array by its name.</summary>
    /// <param name="Designator">What is initialized: a name, a reference or a substring.</param>
    public sealed record Variable(Expression Designator) : DataTarget;

    /// <summary>An implied <c>DO</c>: <c>( A(I), I = 1, 3 )</c>.</summary>
    /// <param name="Targets">The items repeated, which may themselves be implied loops.</param>
    /// <param name="ControlVariable">The loop variable, in upper case.</param>
    /// <param name="First">Its first value.</param>
    /// <param name="Last">Its last value.</param>
    /// <param name="Step">Its increment, or <see langword="null"/> when omitted, which means one.</param>
    public sealed record ImpliedDo(
        IReadOnlyList<DataTarget> Targets,
        string ControlVariable,
        Expression First,
        Expression Last,
        Expression? Step) : DataTarget;
}
