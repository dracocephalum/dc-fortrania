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
    /// Gets a short name for every emitted file to call the runtime's
    /// class by, such as <c>F</c>, which gives <c>F.Assign(...)</c>. It is
    /// <see langword="null"/> unless set, and the runtime's methods are
    /// then called with no name before them, <c>Assign(...)</c>, so that
    /// what the translation adds stays small beside what was translated.
    /// Either way, where the program has a procedure emitted under the name
    /// that would be written, the runtime's method is written with the full
    /// name of its class, since the procedure would be found first.
    /// </summary>
    public string? RuntimeAlias { get; init; }

    /// <summary>
    /// Gets the diagnostics every emitted file switches off for itself, by
    /// their identifiers, such as <c>CA1707</c>; none unless set. They are
    /// added to the ones the emitter always switches off because a faithful
    /// translation raises them: a statement that cannot be reached, a
    /// variable that is set and never read, and a variable assigned to
    /// itself, all of which the source has.
    /// </summary>
    public IReadOnlyList<string> SuppressedWarnings { get; init; } = [];

    /// <summary>
    /// Gets the other libraries whose procedures this one may call; none
    /// unless set. A name this library does not define is looked for in
    /// them, in order, and a call that reaches one of them is written as a
    /// call of that library's instance in the same context.
    /// </summary>
    public IReadOnlyList<LibraryReference> References { get; init; } = [];
}
