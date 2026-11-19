using Shouldly;

namespace Dracocephalum.Fortrania.Runtime.Tests;

public class FortranContextTests
{
    [Fact]
    public void Get_WhenALibraryIsAskedForTheFirstTime_ShouldMakeItAndGiveTheSameOneFromThenOn()
    {
        // Arrange
        using var context = new FortranContext();

        // Act
        var first = context.Get<FirstLibrary>();
        var second = context.Get<SecondLibrary>();

        // Assert
        first.Context.ShouldBeSameAs(context);
        context.Get<FirstLibrary>().ShouldBeSameAs(first);
        context.Get<SecondLibrary>().ShouldBeSameAs(second);
    }

    [Fact]
    public void Get_WhenALibraryWasMadeByHand_ShouldGiveThatOne()
    {
        // Arrange
        using var context = new FortranContext();
        var made = new FirstLibrary(context);

        // Act & Assert
        context.Get<FirstLibrary>().ShouldBeSameAs(made);
    }

    [Fact]
    public void Get_WhenALibraryDoesNotAddItselfWhenMade_ShouldAddIt()
    {
        // Arrange
        using var context = new FortranContext();

        // Act
        var library = context.Get<Forgetful>();

        // Assert
        context.Get<Forgetful>().ShouldBeSameAs(library);
    }

    [Fact]
    public void Add_WhenTheProgramHasALibraryOfThatClass_ShouldThrow()
    {
        // Arrange
        using var context = new FortranContext();
        _ = new FirstLibrary(context);

        // Act & Assert
        Should.Throw<InvalidOperationException>(() => new FirstLibrary(context));
    }

    [Fact]
    public void Get_WhenThereAreTwoPrograms_ShouldGiveEachItsOwnLibrary()
    {
        // Arrange
        using var one = new FortranContext();
        using var other = new FortranContext();

        // Act
        var first = one.Get<FirstLibrary>();
        var second = other.Get<FirstLibrary>();

        // Assert
        first.ShouldNotBeSameAs(second);
        one.IO.ShouldNotBeSameAs(other.IO);
        one.Memory.ShouldNotBeSameAs(other.Memory);
    }

    [Fact]
    public void Get_WhenManyClassesOfLibraryAreAskedFor_ShouldFindRoomForAll()
    {
        // Arrange
        using var context = new FortranContext();

        // Act
        context.Get<FirstLibrary>();
        context.Get<SecondLibrary>();
        context.Get<Wrapped<int>>();
        context.Get<Wrapped<long>>();
        context.Get<Wrapped<byte>>();
        context.Get<Wrapped<char>>();
        var last = context.Get<Wrapped<double>>();

        // Assert
        context.Get<Wrapped<double>>().ShouldBeSameAs(last);
    }

    [Fact]
    public void Handle_WhenAskedFor_ShouldStandForTheContextUntilItIsDisposed()
    {
        // Arrange
        var context = new FortranContext();
        using var other = new FortranContext();

        // Act
        var handle = context.Handle;

        // Assert
        handle.ShouldNotBe(0);
        context.Handle.ShouldBe(handle);
        other.Handle.ShouldNotBe(handle);
        FortranContext.FromHandle(handle).ShouldBeSameAs(context);
        FortranContext.FromHandle(other.Handle).ShouldBeSameAs(other);

        context.Dispose();
        Should.Throw<ArgumentException>(() => FortranContext.FromHandle(handle));
    }

    [Fact]
    public void FromHandle_WhenAnotherContextHasTakenThePlaceOfOneThatIsGone_ShouldStillRefuseTheOldHandle()
    {
        // Arrange
        var gone = new FortranContext();
        var old = gone.Handle;
        gone.Dispose();

        // Act
        var handles = new List<nint>();
        var contexts = new List<FortranContext>();
        for (var index = 0; index < 40; index++)
        {
            contexts.Add(new FortranContext());
            handles.Add(contexts[^1].Handle);
        }

        // Assert
        handles.ShouldNotContain(old);
        handles.Distinct().Count().ShouldBe(40);
        Should.Throw<ArgumentException>(() => FortranContext.FromHandle(old));
        contexts.ForEach(context => context.Dispose());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(123456789)]
    public void FromHandle_WhenTheValueIsNoHandle_ShouldThrow(int value)
    {
        // Act & Assert
        Should.Throw<ArgumentException>(() => FortranContext.FromHandle(value));
    }

    [Fact]
    public void Constructor_WhenGivenWhatAProgramRunOnItsOwnHas_ShouldGiveItTheConsoleItsCommandLineAndLeaveToRunCommands()
    {
        // Act
        using var context = new FortranContext(FortranContextOptions.Standalone);

        // Assert
        context.IO.Output.ShouldBeSameAs(Console.Out);
        context.IO.Error.ShouldBeSameAs(Console.Error);
        context.IO.Input.ShouldBeSameAs(Console.In);
        context.CommandLine.ShouldBe(Environment.GetCommandLineArgs());
        context.AllowsCommands.ShouldBeTrue();
    }

    [Fact]
    public void Constructor_WhenGivenOptions_ShouldGiveTheProgramWhatTheySay()
    {
        // Arrange
        var clock = new FakeClock();
        var options = new FortranContextOptions
        {
            CommandLine = ["tool", "argument"],
            AllowsCommands = true,
            Clock = clock,
            RecordLengthUnit = 4,
            NewLine = "\n",
        };

        // Act
        using var context = new FortranContext(options);

        // Assert
        context.CommandLine.ShouldBe(["tool", "argument"]);
        context.AllowsCommands.ShouldBeTrue();
        context.Clock.ShouldBeSameAs(clock);
        context.IO.RecordLengthUnit.ShouldBe(4);
        context.IO.NewLine.ShouldBe("\n");
        context.IO.Output.ShouldBeNull();
    }

    [Fact]
    public void Constructor_WhenTheOptionsAreLeftAsMade_ShouldGiveTheProgramNothingButFilesOnDisk()
    {
        // Act
        using var context = new FortranContext(new FortranContextOptions());

        // Assert
        context.CommandLine.ShouldBeEmpty();
        context.AllowsCommands.ShouldBeFalse();
        context.Clock.ShouldBeSameAs(TimeProvider.System);
        context.IO.RecordLengthUnit.ShouldBe(1);
        context.IO.NewLine.ShouldBe(Environment.NewLine);
        context.IO.Output.ShouldBeNull();
        context.IO.Input.ShouldBeNull();
    }

    [Fact]
    public unsafe void Dispose_WhenCalled_ShouldCloseTheFilesFreeTheMemoryAndRefuseWhatComesAfter()
    {
        // Arrange
        var context = new FortranContext();
        context.Get<FirstLibrary>();
        context.Memory.Allocate(16);

        // Act
        context.Dispose();
        context.Dispose();

        // Assert
        Should.Throw<ObjectDisposedException>(() => context.Memory.Allocate(16));
        Should.Throw<ObjectDisposedException>(() => context.IO.Open(11));
        Should.Throw<ObjectDisposedException>(() => context.Get<SecondLibrary>());
        Should.Throw<ObjectDisposedException>(() => context.Handle);
    }

    private sealed class FakeClock : TimeProvider;

    private sealed class FirstLibrary : IFortranLibrary<FirstLibrary>
    {
        public FirstLibrary(FortranContext context)
        {
            Context = context;
            context.Add(this);
        }

        public FortranContext Context { get; }

        public static FirstLibrary Create(FortranContext context) => new(context);
    }

    private sealed class SecondLibrary : IFortranLibrary<SecondLibrary>
    {
        private SecondLibrary(FortranContext context) => context.Add(this);

        public static SecondLibrary Create(FortranContext context) => new(context);
    }

    private sealed class Wrapped<T> : IFortranLibrary<Wrapped<T>>
    {
        private Wrapped(FortranContext context) => context.Add(this);

        public static Wrapped<T> Create(FortranContext context) => new(context);
    }

    private sealed class Forgetful : IFortranLibrary<Forgetful>
    {
        public static Forgetful Create(FortranContext context) => new();
    }
}
