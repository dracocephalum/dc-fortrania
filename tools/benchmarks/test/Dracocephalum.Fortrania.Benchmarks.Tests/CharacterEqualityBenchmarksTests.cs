using Shouldly;

namespace Dracocephalum.Fortrania.Benchmarks.Tests;

public class CharacterEqualityBenchmarksTests
{
    [Fact]
    public void Test_WhateverTheTwoLengthsAndWhereverTheValuesDiffer_ShouldSayWhatTheFrameworksComparisonSays()
    {
        // Arrange
        var benchmarks = new CharacterEqualityBenchmarks();

        // Act
        var byTheRuntime = benchmarks.Test(useRuntime: true);
        var byTheFramework = benchmarks.Test(useRuntime: false);

        // Assert
        byTheRuntime.ShouldBe(byTheFramework);
        byTheRuntime.ShouldContain(true);
        byTheRuntime.ShouldContain(false);
    }
}
