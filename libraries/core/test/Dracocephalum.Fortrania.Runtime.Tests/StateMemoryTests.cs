using Shouldly;

namespace Dracocephalum.Fortrania.Runtime.Tests;

public unsafe class StateMemoryTests
{
    [Fact]
    public void Allocate_WhenAskedForABlock_ShouldHandOutZeroedMemoryThatCanBeWritten()
    {
        // Arrange
        using var memory = new StateMemory();

        // Act
        var block = memory.Allocate(4096);

        // Assert
        new ReadOnlySpan<byte>(block, 4096).IndexOfAnyExcept((byte)0).ShouldBe(-1);
        block[4095] = 7;
        block[4095].ShouldBe((byte)7);
        ((nint)block % 8).ShouldBe(0);
    }

    [Fact]
    public void Allocate_WhenAskedTwice_ShouldHandOutBlocksThatDoNotMeetAndCountTheirBytes()
    {
        // Arrange
        using var memory = new StateMemory();

        // Act
        var first = memory.Allocate(64);
        var second = memory.Allocate(32);

        // Assert
        (first == second).ShouldBeFalse();
        (first + 64 <= second || second + 32 <= first).ShouldBeTrue();
        memory.Allocated.ShouldBe(96);
    }

    [Fact]
    public void Allocate_WhenAskedForNothing_ShouldStillHandOutAnAddress()
    {
        // Arrange
        using var memory = new StateMemory();

        // Act
        var block = memory.Allocate(0);

        // Assert
        (block == null).ShouldBeFalse();
        memory.Allocated.ShouldBe(1);
    }

    [Fact]
    public void Allocate_WhenTheSizeIsNegative_ShouldThrow()
    {
        // Arrange
        using var memory = new StateMemory();

        // Act & Assert
        Should.Throw<ArgumentOutOfRangeException>(() => memory.Allocate(-1));
    }

    [Fact]
    public void Allocate_WhenDisposed_ShouldThrow()
    {
        // Arrange
        var memory = new StateMemory();
        memory.Allocate(16);
        memory.Dispose();

        // Act & Assert
        Should.Throw<ObjectDisposedException>(() => memory.Allocate(16));
    }

    [Fact]
    public void Dispose_WhenCalledTwice_ShouldDoNothingTheSecondTime()
    {
        // Arrange
        var memory = new StateMemory();
        memory.Allocate(16);
        memory.Dispose();

        // Act & Assert
        Should.NotThrow(memory.Dispose);
    }
}
