using Shouldly;

namespace Dracocephalum.Fortrania.Benchmarks.Tests;

public class PairedRunnerTests
{
    [Fact]
    public void Measure_WhenTheCandidateTakesAFifthLonger_ShouldGiveThatRatioAndBothTimes()
    {
        // Arrange
        var clock = new CountingClock();
        var sut = new PairedRunner(clock);

        // Act
        var result = sut.Measure(() => clock.Ticks += 10, () => clock.Ticks += 12, operationsPerInvoke: 2);

        // Assert
        result.Ratio.ShouldBe(1.2, 1e-12);
        result.LowerQuartile.ShouldBe(1.2, 1e-12);
        result.UpperQuartile.ShouldBe(1.2, 1e-12);
        result.BaselineNanoseconds.ShouldBe(5, 1e-9);
        result.CandidateNanoseconds.ShouldBe(6, 1e-9);
        result.IsDifferent.ShouldBeTrue();
    }

    [Fact]
    public void Measure_WhenBothTakeTheSame_ShouldNotCallThemDifferent()
    {
        // Arrange
        var clock = new CountingClock();
        var sut = new PairedRunner(clock);

        // Act
        var result = sut.Measure(() => clock.Ticks += 10, () => clock.Ticks += 10, operationsPerInvoke: 1);

        // Assert
        result.Ratio.ShouldBe(1.0);
        result.IsDifferent.ShouldBeFalse();
    }

    // A clock that stands still but for what the measured actions add to
    // it, a tick a nanosecond, so that a measurement has one right answer.
    private sealed class CountingClock : TimeProvider
    {
        public long Ticks { get; set; }

        public override long TimestampFrequency => 1_000_000_000;

        public override long GetTimestamp() => Ticks;
    }
}
