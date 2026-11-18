using Shouldly;

namespace Dracocephalum.Fortrania.Runtime.Tests;

public class FortranContextTests
{
    [Fact]
    public void Get_WhenALibraryWasAdded_ShouldGiveThatLibrary()
    {
        // Arrange
        using var context = new FortranContext();
        var first = new FirstLibrary();
        var second = new SecondLibrary();

        // Act
        context.Add(first);
        context.Add(second);

        // Assert
        context.Get<FirstLibrary>().ShouldBeSameAs(first);
        context.Get<SecondLibrary>().ShouldBeSameAs(second);
    }

    [Fact]
    public void Get_WhenTheProgramHasNoSuchLibrary_ShouldThrow()
    {
        // Arrange
        using var context = new FortranContext();
        context.Add(new FirstLibrary());

        // Act & Assert
        Should.Throw<InvalidOperationException>(() => context.Get<SecondLibrary>())
            .Message.ShouldContain(nameof(SecondLibrary));
    }

    [Fact]
    public void Add_WhenTheProgramHasALibraryOfThatClass_ShouldThrow()
    {
        // Arrange
        using var context = new FortranContext();
        context.Add(new FirstLibrary());

        // Act & Assert
        Should.Throw<InvalidOperationException>(() => context.Add(new FirstLibrary()));
    }

    [Fact]
    public void Get_WhenThereAreTwoPrograms_ShouldGiveEachItsOwnLibrary()
    {
        // Arrange
        using var one = new FortranContext();
        using var other = new FortranContext();
        var first = new FirstLibrary();
        var second = new FirstLibrary();

        // Act
        one.Add(first);
        other.Add(second);

        // Assert
        one.Get<FirstLibrary>().ShouldBeSameAs(first);
        other.Get<FirstLibrary>().ShouldBeSameAs(second);
        one.IO.ShouldNotBeSameAs(other.IO);
        one.Memory.ShouldNotBeSameAs(other.Memory);
    }

    [Fact]
    public void Add_WhenManyClassesOfLibraryAreAdded_ShouldFindRoomForAll()
    {
        // Arrange
        using var context = new FortranContext();

        // Act
        context.Add(new FirstLibrary());
        context.Add(new SecondLibrary());
        context.Add(new Wrapped<int>());
        context.Add(new Wrapped<long>());
        context.Add(new Wrapped<byte>());
        context.Add(new Wrapped<char>());
        context.Add(new Wrapped<double>());

        // Assert
        context.Get<Wrapped<double>>().ShouldNotBeNull();
        context.Get<FirstLibrary>().ShouldNotBeNull();
    }

    [Fact]
    public unsafe void Dispose_WhenCalled_ShouldCloseTheFilesFreeTheMemoryAndForgetTheLibraries()
    {
        // Arrange
        var context = new FortranContext();
        context.Add(new FirstLibrary());
        context.Memory.Allocate(16);

        // Act
        context.Dispose();
        context.Dispose();

        // Assert
        Should.Throw<ObjectDisposedException>(() => context.Memory.Allocate(16));
        Should.Throw<ObjectDisposedException>(() => context.IO.Open(11));
        Should.Throw<ObjectDisposedException>(() => context.Add(new SecondLibrary()));
        Should.Throw<InvalidOperationException>(() => context.Get<FirstLibrary>());
    }

    private sealed class FirstLibrary;

    private sealed class SecondLibrary;

    private sealed class Wrapped<T>;
}
