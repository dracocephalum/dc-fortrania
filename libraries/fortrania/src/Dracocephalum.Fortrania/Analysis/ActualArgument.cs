using Dracocephalum.Fortrania.Syntax;

namespace Dracocephalum.Fortrania.Analysis;

/// <summary>One argument of a call, as the called procedure has to take it.</summary>
/// <param name="Kind">What is passed.</param>
/// <param name="Type">Its type; <see langword="null"/> for a procedure that has none in the calling unit.</param>
/// <param name="Name">
/// The variable, array or procedure passed, by its name in the calling unit;
/// <see langword="null"/> for a <see cref="ActualArgumentKind.Value"/>.
/// </param>
public sealed record ActualArgument(ActualArgumentKind Kind, DataType? Type, string? Name);
