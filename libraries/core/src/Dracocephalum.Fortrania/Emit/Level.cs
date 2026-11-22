namespace Dracocephalum.Fortrania.Emit;

/// <summary>
/// Binding strength of a C# expression, weakest first, so that a part is
/// put in parentheses exactly where the whole would otherwise regroup it.
/// </summary>
internal enum Level
{
    Conditional,
    Or,
    And,
    Equality,
    Relational,
    Additive,
    Multiplicative,
    Unary,
    Primary,
}
