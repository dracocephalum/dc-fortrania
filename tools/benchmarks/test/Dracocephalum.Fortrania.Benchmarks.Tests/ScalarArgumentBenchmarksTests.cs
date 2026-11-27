using Shouldly;

namespace Dracocephalum.Fortrania.Benchmarks.Tests;

public class ScalarArgumentBenchmarksTests
{
    [Fact]
    public void InLocals_WhenGivenTheSameArguments_ShouldGiveTheSameBitsAsThroughPointers()
    {
        // Arrange
        var benchmarks = new ScalarArgumentBenchmarks();
        benchmarks.Allocate();
        try
        {
            // Act
            var throughPointers = benchmarks.ThroughPointers();
            var inLocals = benchmarks.InLocals();

            // Assert: a copy to a local keeps every operation and its order.
            BitConverter.DoubleToInt64Bits(inLocals).ShouldBe(BitConverter.DoubleToInt64Bits(throughPointers));
        }
        finally
        {
            benchmarks.Free();
        }
    }
}
