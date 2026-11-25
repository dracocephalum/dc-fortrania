using Shouldly;

namespace Dracocephalum.Fortrania.Runtime.Tests;

public unsafe class BufferArenaTests
{
    [Fact]
    public void Register_WhenTheContextChoosesNothing_ShouldKeepAProjectOnTheStackUnlessItWasTranslatedForTheArena()
    {
        // Arrange
        using var context = new FortranContext();
        var sut = context.Arena;

        // Act
        var both = sut.Register("Both", 100, 100, [], BufferPlacement.Default);
        var stack = sut.Register("Stack", 100, 100, [], BufferPlacement.Stack);
        var arena = sut.Register("Arena", 100, 100, [], BufferPlacement.Arena);
        var idle = arena.CurrentSource;
        sut.Push();

        // Assert
        both.CurrentSource.ShouldBe(BufferSource.Stack);
        ((nint)both.Start).ShouldBe(0);
        stack.CurrentSource.ShouldBe(BufferSource.Stack);
        idle.ShouldBe(BufferSource.None);
        arena.CurrentSource.ShouldBe(BufferSource.Arena);
        ((nint)arena.Start).ShouldNotBe(0);
        sut.Bytes.ShouldBe(100);
    }

    [Fact]
    public void Push_WhenTheContextChoosesTheArena_ShouldGiveAProjectARegionItCanWriteAllOf()
    {
        // Arrange
        using var context = new FortranContext(new FortranContextOptions { BufferPlacement = BufferPlacement.Arena });
        var region = context.Arena.Register("Library", 4096, 4096, [], BufferPlacement.Default);

        // Act
        context.Arena.Push();

        // Assert
        region.CurrentSource.ShouldBe(BufferSource.Arena);
        new Span<byte>(region.Start, 4096).Fill(7);
        region.Start[4095].ShouldBe((byte)7);
        ((nint)region.Start % 64).ShouldBe(0);
    }

    [Fact]
    public void Register_WhenAskedAgainForAProject_ShouldGiveTheSameRegion()
    {
        // Arrange
        using var context = new FortranContext(new FortranContextOptions { BufferPlacement = BufferPlacement.Arena });
        var first = context.Arena.Register("Library", 64, 64, [], BufferPlacement.Default);

        // Act
        var second = context.Arena.Register("Library", 64, 64, [], BufferPlacement.Default);

        // Assert
        second.ShouldBeSameAs(first);
    }

    [Theory]
    [InlineData(BufferPlacement.Stack, BufferPlacement.Arena, "stack", "arena")]
    [InlineData(BufferPlacement.Arena, BufferPlacement.Stack, "arena", "stack")]
    public void Register_WhenTheProjectWasTranslatedForTheOtherPlaceAlone_ShouldRefuseAndSayBoth(
        BufferPlacement chosen,
        BufferPlacement translatedFor,
        string chosenName,
        string translatedName)
    {
        // Arrange
        using var context = new FortranContext(new FortranContextOptions { BufferPlacement = chosen });

        // Act
        var thrown = Should.Throw<InvalidOperationException>(() => context.Arena.Register("Library", 64, 64, [], translatedFor));

        // Assert
        thrown.Message.ShouldBe($"The context keeps working buffers in the {chosenName}, and Library was translated for the {translatedName} alone.");
    }

    [Fact]
    public void Register_WhenAProjectRefersToAnother_ShouldPutTheOthersRegionAfterItsOwnWhicheverIsMadeFirst()
    {
        // Arrange: the library is made first and alone, as when a host
        // calls it before the program that also calls it is made.
        using var context = new FortranContext(new FortranContextOptions { BufferPlacement = BufferPlacement.Arena });
        var sut = context.Arena;
        var library = sut.Register("Library", 300, 300, [], BufferPlacement.Default);
        sut.Bytes.ShouldBe(300);

        // Act
        var program = sut.Register("Program", 100, 400, ["Library"], BufferPlacement.Default);
        sut.Push();

        // Assert
        sut.Bytes.ShouldBe(400);
        (library.Start - program.Start).ShouldBe(100);
    }

    [Fact]
    public void Register_WhenTheProgramIsMadeFirst_ShouldHaveRoomForWhatItReachesBeforeThatIsMade()
    {
        // Arrange
        using var context = new FortranContext(new FortranContextOptions { BufferPlacement = BufferPlacement.Arena });
        var sut = context.Arena;
        var program = sut.Register("Program", 100, 400, ["Library"], BufferPlacement.Default);
        sut.Push();
        var before = (nint)program.Start;

        // Act: the library is made on the first call into it, while the
        // program's own procedures hold addresses into the copy open.
        var library = sut.Register("Library", 300, 300, [], BufferPlacement.Default);

        // Assert: nothing moved, nothing was replaced.
        sut.Bytes.ShouldBe(400);
        ((nint)program.Start).ShouldBe(before);
        (library.Start - program.Start).ShouldBe(100);
        sut.RetiredCount.ShouldBe(0);
    }

    [Fact]
    public void Register_WhenAnotherLibraryOfAProjectAsksForMore_ShouldGrowTheRegionAndMoveWhatLiesAfterIt()
    {
        // Arrange: one library of the program made, which needs little of
        // its region, and the library the program calls into after it.
        using var context = new FortranContext(new FortranContextOptions { BufferPlacement = BufferPlacement.Arena });
        var sut = context.Arena;
        var program = sut.Register("Program", 50, 350, ["Library"], BufferPlacement.Default);
        var library = sut.Register("Library", 300, 300, [], BufferPlacement.Default);
        sut.Bytes.ShouldBe(350);

        // Act: another library of the program, which needs more.
        var again = sut.Register("Program", 120, 420, ["Library"], BufferPlacement.Default);
        var grown = sut.Bytes;
        var smaller = sut.Register("Program", 10, 310, ["Library"], BufferPlacement.Default);
        sut.Push();

        // Assert: one region, as large as the most asked for.
        again.ShouldBeSameAs(program);
        smaller.ShouldBeSameAs(program);
        grown.ShouldBe(420);
        sut.Bytes.ShouldBe(420);
        (library.Start - program.Start).ShouldBe(120);
    }

    [Fact]
    public void Register_WhenTheArenaMustGrowWhileACopyIsOpen_ShouldKeepTheOldCopyUntilItIsClosed()
    {
        // Arrange: a procedure running in the open copy, holding its address.
        using var context = new FortranContext(new FortranContextOptions { BufferPlacement = BufferPlacement.Arena });
        var sut = context.Arena;
        var region = sut.Register("Library", 100, 100, [], BufferPlacement.Default);
        sut.Push();
        var held = region.Start;
        new Span<byte>(held, 100).Fill(5);

        // Act: a library that needs more, made by code the translation did
        // not see while the procedure runs.
        sut.Register("Library", 300, 300, [], BufferPlacement.Default);
        var kept = sut.RetiredCount;
        var fresh = (nint)region.Start;
        new Span<byte>(region.Start, 300).Fill(9);
        sut.Pop();

        // Assert: what the running procedure holds was left alone, and freed
        // only once the copy it was in was closed.
        kept.ShouldBe(1);
        fresh.ShouldNotBe((nint)held);
        sut.Bytes.ShouldBe(300);
        sut.RetiredCount.ShouldBe(0);
        region.CurrentSource.ShouldBe(BufferSource.None);
    }

    [Fact]
    public void Register_WhenTheArenaMustGrowInACallbackLevel_ShouldReplaceEveryOpenCopyEachWhenItsDepthIsOpenAgain()
    {
        // Arrange: a call from outside, and a callback inside it.
        using var context = new FortranContext(new FortranContextOptions { BufferPlacement = BufferPlacement.Arena });
        var sut = context.Arena;
        var region = sut.Register("Library", 100, 100, [], BufferPlacement.Default);
        sut.Push();
        var outer = (nint)region.Start;
        new Span<byte>(region.Start, 100).Fill(1);
        sut.Push();

        // Act: the arena grows in the callback, which closes, and then the
        // call from outside goes on.
        sut.Register("Library", 300, 300, [], BufferPlacement.Default);
        var keptInCallback = sut.RetiredCount;
        new Span<byte>(region.Start, 300).Fill(2);
        sut.Pop();
        var keptAfterCallback = sut.RetiredCount;
        var outerAgain = (nint)region.Start;
        new Span<byte>(region.Start, 300).Fill(3);
        sut.Pop();

        // Assert: both copies replaced; the callback's freed when it closed,
        // the outer one's when the call from outside did; the outer call
        // given a copy of the new size when it went on.
        keptInCallback.ShouldBe(2);
        keptAfterCallback.ShouldBe(1);
        outerAgain.ShouldNotBe(outer);
        sut.RetiredCount.ShouldBe(0);
        sut.Depth.ShouldBe(0);
    }

    [Fact]
    public void Register_WhenTheArenaGrowsWhileNothingRuns_ShouldFreeTheCopiesMadeBeforeAndKeepNone()
    {
        // Arrange: copies made for two depths, and closed.
        using var context = new FortranContext(new FortranContextOptions { BufferPlacement = BufferPlacement.Arena });
        var sut = context.Arena;
        var region = sut.Register("Library", 100, 100, [], BufferPlacement.Default);
        sut.Push();
        sut.Push();
        sut.Pop();
        sut.Pop();

        // Act
        sut.Register("Library", 300, 300, [], BufferPlacement.Default);
        sut.Push();

        // Assert
        sut.RetiredCount.ShouldBe(0);
        new Span<byte>(region.Start, 300).Fill(4);
        region.Start[299].ShouldBe((byte)4);
    }

    [Fact]
    public void Push_WhenAProcedureIsCalledThroughAnArgument_ShouldMoveEveryRegionToACopyOfItsOwnAndPopMoveThemBack()
    {
        // Arrange
        using var context = new FortranContext(new FortranContextOptions { BufferPlacement = BufferPlacement.Arena });
        var sut = context.Arena;
        var program = sut.Register("Program", 100, 400, ["Library"], BufferPlacement.Default);
        var library = sut.Register("Library", 300, 300, [], BufferPlacement.Default);
        sut.Push();
        var outer = ((nint)program.Start, (nint)library.Start);
        new Span<byte>(library.Start, 300).Fill(1);

        // Act
        sut.Push();
        var inner = ((nint)program.Start, (nint)library.Start);
        new Span<byte>(library.Start, 300).Fill(2);
        sut.Push();
        var innermost = (nint)program.Start;
        sut.Pop();
        var again = ((nint)program.Start, (nint)library.Start);
        sut.Pop();

        // Assert
        inner.ShouldNotBe(outer);
        (inner.Item2 - inner.Item1).ShouldBe(100);
        innermost.ShouldNotBe(inner.Item1);
        again.ShouldBe(inner);
        ((nint)program.Start, (nint)library.Start).ShouldBe(outer);
        sut.Depth.ShouldBe(1);

        // What the outer chain wrote is as it left it.
        new ReadOnlySpan<byte>(library.Start, 300).IndexOfAnyExcept((byte)1).ShouldBe(-1);
    }

    [Fact]
    public void Pop_WhenTheCallEndsByAnException_ShouldStillCloseTheCopyAndFreeWhatItKept()
    {
        // Arrange
        using var context = new FortranContext(new FortranContextOptions { BufferPlacement = BufferPlacement.Arena });
        var sut = context.Arena;
        sut.Register("Library", 100, 100, [], BufferPlacement.Default);

        // Act: as the method that opens a copy for a call from outside does.
        Should.Throw<InvalidOperationException>(() =>
        {
            sut.Push();
            try
            {
                sut.Register("Library", 300, 300, [], BufferPlacement.Default);
                throw new InvalidOperationException("STOP");
            }
            finally
            {
                sut.Pop();
            }
        });

        // Assert
        sut.Depth.ShouldBe(0);
        sut.RetiredCount.ShouldBe(0);
    }

    [Fact]
    public void Pop_WhenNoCopyIsOpen_ShouldRefuse()
    {
        // Arrange
        using var context = new FortranContext();

        // Act & Assert
        Should.Throw<InvalidOperationException>(context.Arena.Pop).Message.ShouldBe("No copy of the arena is open to close.");
    }

    [Fact]
    public void Push_WhenMoreCallbacksRunInsideOneAnotherThanTheHostAllows_ShouldRefuse()
    {
        // Arrange: the call from outside, and two callbacks inside it.
        using var context = new FortranContext(new FortranContextOptions { CallbackDepth = 2 });
        var sut = context.Arena;
        sut.Push();
        sut.Push();
        sut.Push();

        // Act
        var thrown = Should.Throw<InvalidOperationException>(sut.Push);

        // Assert
        thrown.Message.ShouldStartWith("More than 2 calls through a procedure argument are running, one inside another.");
        sut.Depth.ShouldBe(3);
    }

    [Fact]
    public void CallbackDepth_WhenAHostLeavesItAlone_ShouldBeSixteen()
    {
        // Arrange
        using var context = new FortranContext();
        var sut = context.Arena;

        // Act: the call from outside, and sixteen callbacks inside it.
        for (var depth = 0; depth < 17; depth++)
        {
            sut.Push();
        }

        // Assert
        sut.Depth.ShouldBe(17);
        Should.Throw<InvalidOperationException>(sut.Push);
    }
}
