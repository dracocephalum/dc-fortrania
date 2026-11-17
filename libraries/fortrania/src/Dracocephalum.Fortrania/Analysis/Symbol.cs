using Dracocephalum.Fortrania.Syntax;

namespace Dracocephalum.Fortrania.Analysis;

/// <summary>
/// What a program unit's declarations say about one name. It holds only
/// what was declared: a name's implicit type, and whether a name with no
/// dimensions is a variable or a function, are settled by how it is used.
/// </summary>
/// <param name="Name">The name, in upper case.</param>
/// <param name="DeclaredType">
/// The type a type statement or a <c>FUNCTION</c> statement gives it, with
/// the length that applies to this name, or <see langword="null"/> when no
/// statement types it.
/// </param>
/// <param name="Dimensions">Its array bounds, in order; empty when it is not an array.</param>
/// <param name="ConstantValue">The value a <c>PARAMETER</c> statement gives it, or <see langword="null"/> when it is not a named constant.</param>
/// <param name="IsDummyArgument">Whether it is an argument of the program unit or of one of its entries.</param>
/// <param name="IsEntryPoint">Whether it names the subroutine or function itself, or one of its entries.</param>
/// <param name="IsSaved">Whether a <c>SAVE</c> statement names it. A <c>SAVE</c> with no names is on the table, not here.</param>
/// <param name="IsExternal">Whether an <c>EXTERNAL</c> statement names it.</param>
/// <param name="IsIntrinsic">Whether an <c>INTRINSIC</c> statement names it.</param>
/// <param name="IsStatementFunction">Whether the unit defines it as a statement function.</param>
/// <param name="IncludeFile">The include file that first declared it, as named in the <c>INCLUDE</c> statement, or <see langword="null"/> when the program unit's own text did.</param>
public sealed record Symbol(
    string Name,
    TypeSpecification? DeclaredType,
    IReadOnlyList<DimensionBound> Dimensions,
    Expression? ConstantValue,
    bool IsDummyArgument,
    bool IsEntryPoint,
    bool IsSaved,
    bool IsExternal,
    bool IsIntrinsic,
    bool IsStatementFunction,
    string? IncludeFile)
{
    /// <summary>Gets a value indicating whether the name is an array.</summary>
    public bool IsArray => Dimensions.Count > 0;

    /// <summary>Gets a value indicating whether the name is a named constant.</summary>
    public bool IsConstant => ConstantValue is not null;
}
