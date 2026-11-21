using Shouldly;

namespace Dracocephalum.Fortrania.Benchmarks.Tests;

public class PairedResultTests
{
    [Fact]
    public void From_WhenThePairsDisagree_ShouldGiveTheMedianAndTheQuartilesOfTheirRatios()
    {
        // Arrange
        long[] baseline = [100, 100, 100, 100, 100];
        long[] candidate = [50, 90, 80, 300, 70];

        // Act
        var result = PairedResult.From(baseline, candidate, operations: 10, ticksPerSecond: 1_000_000_000);

        // Assert
        result.Ratio.ShouldBe(0.8, 1e-12);
        result.LowerQuartile.ShouldBe(0.7, 1e-12);
        result.UpperQuartile.ShouldBe(0.9, 1e-12);
        result.BaselineNanoseconds.ShouldBe(10, 1e-9);
        result.CandidateNanoseconds.ShouldBe(5, 1e-9);
        result.IsDifferent.ShouldBeTrue();
    }

    [Fact]
    public void From_WhenTheMiddleHalfOfTheRatiosSpansOne_ShouldNotCallThemDifferent()
    {
        // Arrange
        long[] baseline = [100, 100, 100, 100, 100];
        long[] candidate = [80, 95, 99, 105, 130];

        // Act
        var result = PairedResult.From(baseline, candidate, operations: 1, ticksPerSecond: 1_000_000_000);

        // Assert
        result.Ratio.ShouldBe(0.99, 1e-12);
        result.IsDifferent.ShouldBeFalse();
    }
}
