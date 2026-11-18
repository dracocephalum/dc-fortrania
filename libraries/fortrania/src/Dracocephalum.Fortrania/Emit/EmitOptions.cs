namespace Dracocephalum.Fortrania.Emit;

/// <summary>What the emitted code is to be called and how it is to present itself.</summary>
/// <param name="Namespace">The namespace of the emitted class.</param>
/// <param name="ClassName">
/// The class every program unit becomes methods of. It is emitted as a
/// partial class, one file per program unit.
/// </param>
public sealed record EmitOptions(string Namespace, string ClassName)
{
    /// <summary>Gets the convention the emitted procedures follow; Fortrania's own unless set.</summary>
    public EmitConvention Convention { get; init; } = EmitConvention.Fortrania;

    /// <summary>
    /// Gets the diagnostics every emitted file switches off for itself, by
    /// their identifiers, such as <c>CA1707</c>; none unless set. They are
    /// added to the ones the emitter always switches off because a faithful
    /// translation raises them: a statement that cannot be reached and a
    /// variable that is set and never read, both of which the source has.
    /// </summary>
    public IReadOnlyList<string> SuppressedWarnings { get; init; } = [];
}
