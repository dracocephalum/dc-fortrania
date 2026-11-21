using Dracocephalum.Fortrania.Syntax;

namespace Dracocephalum.Fortrania.Analysis;

/// <summary>
/// One name of a program unit: what it stands for there, and everything the
/// unit declares about it. A name means one thing throughout a program
/// unit, so this is the answer for every place the name is written.
/// </summary>
/// <param name="Name">The name, in upper case.</param>
/// <param name="Kind">What the name stands for.</param>
/// <param name="Type">
/// Its type, with the length that applies to this name. It is
/// <see langword="null"/> for a subroutine and for an external procedure of
/// unknown nature, which have none, and for an intrinsic function no
/// statement types, whose result depends on its arguments.
/// </param>
/// <param name="IsTypeImplicit">Whether <paramref name="Type"/> comes from the name's first letter and not from a statement.</param>
/// <param name="Dimensions">Its array bounds, in order; empty when it is not an array.</param>
/// <param name="ConstantValue">The value a <c>PARAMETER</c> statement gives it, or <see langword="null"/> when it is not a named constant.</param>
/// <param name="IsDummyArgument">Whether it is an argument of the program unit or of one of its entries.</param>
/// <param name="IsEntryPoint">Whether it names the subroutine or function itself, or one of its entries.</param>
/// <param name="IsSaved">Whether a <c>SAVE</c> statement names it. A <c>SAVE</c> with no names is on the table, not here.</param>
/// <param name="IsExternal">Whether an <c>EXTERNAL</c> statement names it.</param>
/// <param name="IsIntrinsic">Whether an <c>INTRINSIC</c> statement names it.</param>
/// <param name="IsStatementFunction">Whether the unit defines it as a statement function.</param>
/// <param name="IncludeFile">The include file that first declared it, as named in the <c>INCLUDE</c> statement, or <see langword="null"/> when the program unit's own text did.</param>
/// <param name="Line">The one-based number of the line the name first appears on, in <paramref name="IncludeFile"/> when that is not <see langword="null"/>.</param>
public sealed record Symbol(
    string Name,
    SymbolKind Kind,
    TypeSpecification? Type,
    bool IsTypeImplicit,
    IReadOnlyList<DimensionBound> Dimensions,
    Expression? ConstantValue,
    bool IsDummyArgument,
    bool IsEntryPoint,
    bool IsSaved,
    bool IsExternal,
    bool IsIntrinsic,
    bool IsStatementFunction,
    string? IncludeFile,
    int Line)
{
    /// <summary>Gets a value indicating whether the name is an array.</summary>
    public bool IsArray => Dimensions.Count > 0;

    /// <summary>Gets a value indicating whether the name is a named constant.</summary>
    public bool IsConstant => ConstantValue is not null;
}
