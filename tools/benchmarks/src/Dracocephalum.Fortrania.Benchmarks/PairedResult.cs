namespace Dracocephalum.Fortrania.Benchmarks;

/// <summary>
/// What a paired measurement found: how a candidate's time stands to the
/// baseline's, as a ratio below one when the candidate is faster, and the
/// least time each took for one operation.
/// </summary>
/// <param name="Ratio">The median of the pairs' ratios, candidate over baseline.</param>
/// <param name="LowerQuartile">The ratio a quarter of the pairs were below.</param>
/// <param name="UpperQuartile">The ratio a quarter of the pairs were above.</param>
/// <param name="BaselineNanoseconds">The baseline's fastest slice, in nanoseconds an operation.</param>
/// <param name="CandidateNanoseconds">The candidate's fastest slice, in nanoseconds an operation.</param>
internal readonly record struct PairedResult(
    double Ratio,
    double LowerQuartile,
    double UpperQuartile,
    double BaselineNanoseconds,
    double CandidateNanoseconds)
{
    /// <summary>
    /// Gets a value indicating whether the two differ by more than the
    /// pairs disagree among themselves: the middle half of the ratios lies
    /// wholly to one side of one.
    /// </summary>
    public bool IsDifferent => LowerQuartile > 1 || UpperQuartile < 1;

    /// <summary>Sums up the slices of a measurement.</summary>
    /// <param name="baselineTicks">What each baseline slice took, a pair an element.</param>
    /// <param name="candidateTicks">What each candidate slice took, in the same order.</param>
    /// <param name="operations">How many operations a slice did.</param>
    /// <param name="ticksPerSecond">The clock's frequency.</param>
    /// <returns>The measurement's result.</returns>
    public static PairedResult From(ReadOnlySpan<long> baselineTicks, ReadOnlySpan<long> candidateTicks, long operations, long ticksPerSecond)
    {
        var ratios = new double[baselineTicks.Length];
        for (var pair = 0; pair < ratios.Length; pair++)
        {
            ratios[pair] = (double)candidateTicks[pair] / baselineTicks[pair];
        }

        Array.Sort(ratios);
        var nanosecondsPerTick = 1e9 / ticksPerSecond / operations;
        return new PairedResult(
            Quantile(ratios, 0.5),
            Quantile(ratios, 0.25),
            Quantile(ratios, 0.75),
            Least(baselineTicks) * nanosecondsPerTick,
            Least(candidateTicks) * nanosecondsPerTick);
    }

    // The value at a fraction of the way through sorted values, between two
    // of them where the fraction falls between.
    private static double Quantile(double[] sorted, double fraction)
    {
        var position = fraction * (sorted.Length - 1);
        var below = (int)position;
        var above = Math.Min(below + 1, sorted.Length - 1);
        return sorted[below] + ((sorted[above] - sorted[below]) * (position - below));
    }

    private static long Least(ReadOnlySpan<long> values)
    {
        var least = long.MaxValue;
        foreach (var value in values)
        {
            least = Math.Min(least, value);
        }

        return least;
    }
}
