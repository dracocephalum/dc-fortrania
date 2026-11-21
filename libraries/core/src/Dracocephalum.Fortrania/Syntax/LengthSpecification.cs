namespace Dracocephalum.Fortrania.Syntax;

/// <summary>
/// The length written after an asterisk in a type statement: <c>*80</c>,
/// <c>*(N+1)</c> or <c>*(*)</c>. For <c>CHARACTER</c> it is a count of
/// characters; for the other types, as an extension, a size in bytes.
/// </summary>
/// <param name="Value">The length, or <see langword="null"/> for <c>*(*)</c>, where the length is that of the actual argument.</param>
public sealed record LengthSpecification(Expression? Value)
{
    /// <summary>Gets a value indicating whether the length is taken from the actual argument.</summary>
    public bool IsAssumed => Value is null;
}
