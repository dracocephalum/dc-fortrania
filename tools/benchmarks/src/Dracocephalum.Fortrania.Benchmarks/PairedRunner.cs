namespace Dracocephalum.Fortrania.Benchmarks;

/// <summary>
/// Measures a candidate against a baseline in pairs: a short slice of one,
/// then a short slice of the other, many times over, and the ratio of each
/// pair.
/// </summary>
/// <remarks>
/// It exists because a machine's speed is not constant. On a virtual
/// machine a loop that touches no memory was seen to take anything from 60
/// to 166 milliseconds from one run of it to the next, so two things
/// measured one after the other differ by what the machine did meanwhile,
/// whatever the care taken over each. The two slices of a pair are a
/// millisecond apart and meet the same machine, and the median of some
/// hundreds of ratios came out the same to the third digit while the times
/// themselves moved by a factor of two and more.
/// </remarks>
internal sealed class PairedRunner(TimeProvider clock)
{
    /// <summary>How many pairs are measured.</summary>
    public const int Pairs = 300;

    // A slice is long enough for reading the clock to be nothing beside it,
    // and short enough that the machine seldom changes within a pair.
    private static readonly TimeSpan Slice = TimeSpan.FromMilliseconds(0.5);

    private const int WarmupPairs = 20;
    private const int CalibrationTries = 3;

    /// <summary>Measures one candidate against the baseline.</summary>
    /// <param name="baseline">Does the baseline's operations once.</param>
    /// <param name="candidate">Does the candidate's operations once.</param>
    /// <param name="operationsPerInvoke">How many operations either does when called once.</param>
    /// <returns>The measurement's result.</returns>
    public PairedResult Measure(Action baseline, Action candidate, int operationsPerInvoke)
    {
        var invocations = Calibrate(baseline);
        for (var pair = 0; pair < WarmupPairs; pair++)
        {
            Time(baseline, invocations);
            Time(candidate, invocations);
        }

        var baselineTicks = new long[Pairs];
        var candidateTicks = new long[Pairs];
        for (var pair = 0; pair < Pairs; pair++)
        {
            // Which goes first alternates, so that a machine that is
            // speeding up or slowing down favours neither.
            if ((pair & 1) == 0)
            {
                baselineTicks[pair] = Time(baseline, invocations);
                candidateTicks[pair] = Time(candidate, invocations);
            }
            else
            {
                candidateTicks[pair] = Time(candidate, invocations);
                baselineTicks[pair] = Time(baseline, invocations);
            }
        }

        return PairedResult.From(baselineTicks, candidateTicks, (long)invocations * operationsPerInvoke, clock.TimestampFrequency);
    }

    // How many calls make a slice, by the fastest of a few tries: a try
    // that the machine disturbed would give too few.
    private int Calibrate(Action action)
    {
        var sliceTicks = (long)(Slice.TotalSeconds * clock.TimestampFrequency);
        var invocations = 1;
        while (true)
        {
            var least = long.MaxValue;
            for (var attempt = 0; attempt < CalibrationTries; attempt++)
            {
                least = Math.Min(least, Time(action, invocations));
            }

            if (least >= sliceTicks || invocations >= int.MaxValue / 2)
            {
                return invocations;
            }

            invocations *= 2;
        }
    }

    private long Time(Action action, int invocations)
    {
        var start = clock.GetTimestamp();
        for (var invocation = 0; invocation < invocations; invocation++)
        {
            action();
        }

        return clock.GetTimestamp() - start;
    }
}
