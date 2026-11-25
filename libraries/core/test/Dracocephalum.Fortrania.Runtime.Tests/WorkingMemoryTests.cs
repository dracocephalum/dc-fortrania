using Shouldly;

namespace Dracocephalum.Fortrania.Runtime.Tests;

public unsafe class WorkingMemoryTests
{
    [Fact]
    public void Register_WhenTheContextChoosesNothing_ShouldKeepAProjectOnTheStackUnlessItWasTranslatedForTheBlock()
    {
        // Arrange
        using var context = new FortranContext();
        var sut = context.Working;

        // Act
        var both = sut.Register("Both", 100, 100, [], WorkingBuffers.Default);
        var stack = sut.Register("Stack", 100, 100, [], WorkingBuffers.Stack);
        var block = sut.Register("Block", 100, 100, [], WorkingBuffers.Block);

        // Assert
        both.IsOnStack.ShouldBeTrue();
        ((nint)both.Start).ShouldBe(0);
        stack.IsOnStack.ShouldBeTrue();
        block.IsOnStack.ShouldBeFalse();
        ((nint)block.Start).ShouldNotBe(0);
        sut.BlockBytes.ShouldBe(100);
    }

    [Fact]
    public void Register_WhenTheContextChoosesTheBlock_ShouldGiveAProjectARegionItCanWriteAllOf()
    {
        // Arrange
        using var context = new FortranContext(new FortranContextOptions { WorkingBuffers = WorkingBuffers.Block });

        // Act
        var region = context.Working.Register("Library", 4096, 4096, [], WorkingBuffers.Default);

        // Assert
        region.IsOnStack.ShouldBeFalse();
        new Span<byte>(region.Start, 4096).Fill(7);
        region.Start[4095].ShouldBe((byte)7);
        ((nint)region.Start % 64).ShouldBe(0);
    }

    [Fact]
    public void Register_WhenAskedAgainForAProject_ShouldGiveTheSameRegion()
    {
        // Arrange
        using var context = new FortranContext(new FortranContextOptions { WorkingBuffers = WorkingBuffers.Block });
        var first = context.Working.Register("Library", 64, 64, [], WorkingBuffers.Default);

        // Act
        var second = context.Working.Register("Library", 64, 64, [], WorkingBuffers.Default);

        // Assert
        second.ShouldBeSameAs(first);
    }

    [Theory]
    [InlineData(WorkingBuffers.Stack, WorkingBuffers.Block, "stack", "block")]
    [InlineData(WorkingBuffers.Block, WorkingBuffers.Stack, "block", "stack")]
    public void Register_WhenTheProjectWasTranslatedForTheOtherPlaceAlone_ShouldRefuseAndSayBoth(
        WorkingBuffers chosen,
        WorkingBuffers translatedFor,
        string chosenName,
        string translatedName)
    {
        // Arrange
        using var context = new FortranContext(new FortranContextOptions { WorkingBuffers = chosen });

        // Act
        var thrown = Should.Throw<InvalidOperationException>(() => context.Working.Register("Library", 64, 64, [], translatedFor));

        // Assert
        thrown.Message.ShouldBe($"The context keeps working buffers in the {chosenName}, and Library was translated for the {translatedName} alone.");
    }

    [Fact]
    public void Register_WhenAProjectRefersToAnother_ShouldPutTheOthersRegionAfterItsOwnWhicheverIsMadeFirst()
    {
        // Arrange: the library is made first and alone, as when a host
        // calls it before the program that also calls it is made.
        using var context = new FortranContext(new FortranContextOptions { WorkingBuffers = WorkingBuffers.Block });
        var sut = context.Working;
        var library = sut.Register("Library", 300, 300, [], WorkingBuffers.Default);
        var alone = (nint)library.Start;
        sut.BlockBytes.ShouldBe(300);

        // Act
        var program = sut.Register("Program", 100, 400, ["Library"], WorkingBuffers.Default);

        // Assert
        sut.BlockBytes.ShouldBe(400);
        (library.Start - program.Start).ShouldBe(100);
        alone.ShouldNotBe(0);
    }

    [Fact]
    public void Register_WhenAnotherLibraryOfAProjectAsksForMore_ShouldGrowTheRegionAndMoveWhatLiesAfterIt()
    {
        // Arrange: one library of the program made, which needs little of
        // its region, and the library the program calls into after it.
        using var context = new FortranContext(new FortranContextOptions { WorkingBuffers = WorkingBuffers.Block });
        var sut = context.Working;
        var program = sut.Register("Program", 50, 350, ["Library"], WorkingBuffers.Default);
        var library = sut.Register("Library", 300, 300, [], WorkingBuffers.Default);
        sut.BlockBytes.ShouldBe(350);

        // Act: another library of the program, which needs more.
        var again = sut.Register("Program", 120, 420, ["Library"], WorkingBuffers.Default);
        var grown = sut.BlockBytes;
        var smaller = sut.Register("Program", 10, 310, ["Library"], WorkingBuffers.Default);

        // Assert: one region, as large as the most asked for.
        again.ShouldBeSameAs(program);
        smaller.ShouldBeSameAs(program);
        grown.ShouldBe(420);
        sut.BlockBytes.ShouldBe(420);
        (library.Start - program.Start).ShouldBe(120);
    }

    [Fact]
    public void Register_WhenALibraryOfAProjectAsksForMoreWhileACallThroughAnArgumentRuns_ShouldRefuseAndLeaveTheRegionAsItWas()
    {
        // Arrange
        using var context = new FortranContext(new FortranContextOptions { WorkingBuffers = WorkingBuffers.Block });
        var sut = context.Working;
        var region = sut.Register("Library", 100, 100, [], WorkingBuffers.Default);
        sut.Enter();

        // Act
        var thrown = Should.Throw<InvalidOperationException>(() => sut.Register("Library", 300, 300, [], WorkingBuffers.Default));
        sut.Leave();

        // Assert
        thrown.Message.ShouldContain("Make it before the call.");
        sut.BlockBytes.ShouldBe(100);
        sut.Register("Library", 100, 100, [], WorkingBuffers.Default).ShouldBeSameAs(region);
        sut.BlockBytes.ShouldBe(100);
    }

    [Fact]
    public void Register_WhenTheProgramIsMadeFirst_ShouldHaveRoomForWhatItReachesBeforeThatIsMade()
    {
        // Arrange
        using var context = new FortranContext(new FortranContextOptions { WorkingBuffers = WorkingBuffers.Block });
        var sut = context.Working;
        var program = sut.Register("Program", 100, 400, ["Library"], WorkingBuffers.Default);
        var before = (nint)program.Start;

        // Act: the library is made on the first call into it, while the
        // program's own procedures hold addresses into the block.
        var library = sut.Register("Library", 300, 300, [], WorkingBuffers.Default);

        // Assert
        sut.BlockBytes.ShouldBe(400);
        ((nint)program.Start).ShouldBe(before);
        (library.Start - program.Start).ShouldBe(100);
    }

    [Fact]
    public void Enter_WhenAProcedureIsCalledThroughAnArgument_ShouldMoveEveryRegionToABlockOfItsOwnAndLeaveMoveThemBack()
    {
        // Arrange
        using var context = new FortranContext(new FortranContextOptions { WorkingBuffers = WorkingBuffers.Block });
        var sut = context.Working;
        var program = sut.Register("Program", 100, 400, ["Library"], WorkingBuffers.Default);
        var library = sut.Register("Library", 300, 300, [], WorkingBuffers.Default);
        var outer = ((nint)program.Start, (nint)library.Start);
        new Span<byte>(library.Start, 300).Fill(1);

        // Act
        sut.Enter();
        var inner = ((nint)program.Start, (nint)library.Start);
        new Span<byte>(library.Start, 300).Fill(2);
        sut.Enter();
        var innermost = (nint)program.Start;
        sut.Leave();
        var again = ((nint)program.Start, (nint)library.Start);
        sut.Leave();

        // Assert
        inner.ShouldNotBe(outer);
        (inner.Item2 - inner.Item1).ShouldBe(100);
        innermost.ShouldNotBe(inner.Item1);
        again.ShouldBe(inner);
        ((nint)program.Start, (nint)library.Start).ShouldBe(outer);
        sut.Level.ShouldBe(0);

        // What the outer chain wrote is as it left it.
        new ReadOnlySpan<byte>(library.Start, 300).IndexOfAnyExcept((byte)1).ShouldBe(-1);
    }

    [Fact]
    public void Enter_WhenMoreCallsRunInsideOneAnotherThanTheHostAllows_ShouldRefuse()
    {
        // Arrange
        using var context = new FortranContext(new FortranContextOptions { CallbackDepth = 2 });
        var sut = context.Working;
        sut.Enter();
        sut.Enter();

        // Act
        var thrown = Should.Throw<InvalidOperationException>(sut.Enter);

        // Assert
        thrown.Message.ShouldStartWith("More than 2 calls through a procedure argument are running, one inside another.");
        sut.Level.ShouldBe(2);
    }

    [Fact]
    public void Register_WhenTheBlockWouldHaveToGrowWhileACallThroughAnArgumentRuns_ShouldRefuse()
    {
        // Arrange
        using var context = new FortranContext(new FortranContextOptions { WorkingBuffers = WorkingBuffers.Block });
        var sut = context.Working;
        sut.Register("Library", 300, 300, [], WorkingBuffers.Default);
        sut.Enter();

        // Act & Assert
        Should.Throw<InvalidOperationException>(() => sut.Register("Larger", 500, 500, [], WorkingBuffers.Default));
        Should.NotThrow(() => sut.Register("Smaller", 200, 200, [], WorkingBuffers.Default));
    }

    [Fact]
    public void CallbackDepth_WhenAHostLeavesItAlone_ShouldBeSixteen()
    {
        // Arrange
        using var context = new FortranContext();
        var sut = context.Working;

        // Act
        for (var level = 0; level < 16; level++)
        {
            sut.Enter();
        }

        // Assert
        sut.Level.ShouldBe(16);
        Should.Throw<InvalidOperationException>(sut.Enter);
    }
}
