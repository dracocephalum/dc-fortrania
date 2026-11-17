namespace Dracocephalum.Fortrania.Syntax;

/// <summary>
/// One item of the parenthesized list that controls an input or output
/// statement: <c>UNIT=LUN</c>, <c>FMT='(A)'</c>, <c>IOSTAT=IOSTAT</c>, or a
/// unit or format given by position alone.
/// </summary>
/// <param name="Keyword">
/// The keyword before the equals sign, in upper case, or <see langword="null"/>
/// when the item is given by position: the first such item is the unit and
/// the second the format.
/// </param>
/// <param name="Value">The value, or <see langword="null"/> for an asterisk: the default unit, or list-directed formatting.</param>
public sealed record IOControl(string? Keyword, Expression? Value);
