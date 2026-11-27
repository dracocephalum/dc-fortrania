using Shouldly;

namespace Dracocephalum.Fortrania.Benchmarks.Tests;

public class SavedScalarBenchmarksTests
{
    [Fact]
    public void InLocals_WhenStartedFromTheSameState_ShouldGiveTheSameBitsAsThroughState()
    {
        // Arrange and act: each way from a state of its own, since both
        // leave behind what the next call starts from.
        var throughState = Run(benchmarks => (benchmarks.BisectionThroughState(), benchmarks.ShiftThroughState()));
        var inLocals = Run(benchmarks => (benchmarks.BisectionInLocals(), benchmarks.ShiftInLocals()));

        // Assert: a copy to a local keeps every operation and its order.
        BitConverter.DoubleToInt64Bits(inLocals.Solved).ShouldBe(BitConverter.DoubleToInt64Bits(throughState.Solved));
        inLocals.Shifted.ShouldBe(throughState.Shifted);
    }

    private static (double Solved, int Shifted) Run(Func<SavedScalarBenchmarks, (double Solved, int Shifted)> measure)
    {
        var benchmarks = new SavedScalarBenchmarks();
        benchmarks.Allocate();
        try
        {
            return measure(benchmarks);
        }
        finally
        {
            benchmarks.Free();
        }
    }
}
