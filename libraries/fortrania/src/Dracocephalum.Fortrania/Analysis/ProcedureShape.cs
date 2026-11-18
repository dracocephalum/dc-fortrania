using Dracocephalum.Fortrania.Syntax;

namespace Dracocephalum.Fortrania.Analysis;

/// <summary>
/// What a program unit's own calls show of a procedure it takes as a dummy
/// argument. The language asks no more of the procedure than that it fit
/// those calls, and says nothing of it where the unit only passes it on.
/// </summary>
/// <param name="ResultType">The type of the function's result; <see langword="null"/> for a subroutine.</param>
/// <param name="Arguments">
/// The type of each argument the calls pass, in order;
/// <see langword="null"/> for one that is itself a procedure.
/// </param>
public sealed record ProcedureShape(DataType? ResultType, IReadOnlyList<DataType?> Arguments)
{
    /// <summary>Tells whether <paramref name="other"/> is the same shape.</summary>
    /// <param name="other">Another shape, or <see langword="null"/>.</param>
    /// <returns>Whether the result and every argument agree.</returns>
    public bool Equals(ProcedureShape? other) =>
        other is not null && ResultType == other.ResultType && Arguments.SequenceEqual(other.Arguments);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = default(HashCode);
        hash.Add(ResultType);
        foreach (var argument in Arguments)
        {
            hash.Add(argument);
        }

        return hash.ToHashCode();
    }
}
