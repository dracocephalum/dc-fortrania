using Dracocephalum.Fortrania.Syntax;

namespace Dracocephalum.Fortrania.Analysis;

/// <summary>One function the language supplies, under one of its names.</summary>
/// <param name="Name">The name, generic or specific, in upper case.</param>
/// <param name="MinimumArguments">The fewest arguments it takes.</param>
/// <param name="MaximumArguments">The most arguments it takes.</param>
/// <param name="Accepts">The types its arguments may have; all arguments of one reference have the same type.</param>
/// <param name="Result">The type of its result, or <see langword="null"/> when that is the type of its arguments.</param>
internal sealed record Intrinsic(string Name, int MinimumArguments, int MaximumArguments, DataType[] Accepts, DataType? Result);
