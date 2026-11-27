using Shouldly;

namespace Dracocephalum.Fortrania.Benchmarks.Tests;

public class CharacterAssignBenchmarksTests
{
    [Fact]
    public void Assign_WhateverTheTwoLengths_ShouldLeaveTheBytesTheFrameworksCopyAndFillLeave()
    {
        // Arrange
        var benchmarks = new CharacterAssignBenchmarks();
        benchmarks.Allocate();
        try
        {
            // Act
            var byTheRuntime = benchmarks.Assign(useRuntime: true);
            var byCopyAndFill = benchmarks.Assign(useRuntime: false);

            // Assert: the target's bytes, and nothing before or after them.
            byTheRuntime.ShouldBe(byCopyAndFill);
        }
        finally
        {
            benchmarks.Free();
        }
    }
}
