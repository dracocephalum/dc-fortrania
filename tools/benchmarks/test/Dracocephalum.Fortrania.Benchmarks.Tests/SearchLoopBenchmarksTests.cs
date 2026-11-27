using Shouldly;

namespace Dracocephalum.Fortrania.Benchmarks.Tests;

public class SearchLoopBenchmarksTests
{
    [Fact]
    public void ByFindElement_WhateverTheLength_ShouldFindWhatTheLoopFinds()
    {
        // Arrange
        var benchmarks = new SearchLoopBenchmarks();
        benchmarks.Allocate();
        try
        {
            // Act and assert: the sums of the places found.
            benchmarks.FourByFindElement().ShouldBe(benchmarks.FourByLoop());
            benchmarks.FortyByFindElement().ShouldBe(benchmarks.FortyByLoop());
            benchmarks.TwoHundredByFindElement().ShouldBe(benchmarks.TwoHundredByLoop());
            benchmarks.TwoHundredByLoop().ShouldBeGreaterThan(0);
        }
        finally
        {
            benchmarks.Free();
        }
    }
}
