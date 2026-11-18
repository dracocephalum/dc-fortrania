using Dracocephalum.Fortrania.Syntax;

namespace Dracocephalum.Fortrania.Analysis;

/// <summary>One dummy argument of a procedure, as a caller has to know it.</summary>
/// <param name="Name">Its name inside the procedure, in upper case.</param>
/// <param name="Kind">What the procedure takes it to be.</param>
/// <param name="Type">Its type; <see langword="null"/> for a subroutine and for a procedure that is only passed on.</param>
/// <param name="Rank">The number of its dimensions; zero when it is not an array.</param>
public sealed record DummyArgument(string Name, DummyArgumentKind Kind, DataType? Type, int Rank);
