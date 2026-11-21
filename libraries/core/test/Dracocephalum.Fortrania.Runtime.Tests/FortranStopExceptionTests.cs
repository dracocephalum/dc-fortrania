using Shouldly;

namespace Dracocephalum.Fortrania.Runtime.Tests;

public class FortranStopExceptionTests
{
    [Fact]
    public void Constructor_WhenGivenNoCode_ShouldHaveExitCodeZero() =>
        new FortranStopException().ExitCode.ShouldBe(0);

    [Fact]
    public void Constructor_WhenGivenACode_ShouldKeepIt() =>
        new FortranStopException(3).ExitCode.ShouldBe(3);

    [Fact]
    public void Constructor_WhenGivenAMessageAndACause_ShouldKeepBoth()
    {
        // Arrange
        var cause = new InvalidOperationException();

        // Act
        var exception = new FortranStopException("stopped", cause);

        // Assert
        exception.Message.ShouldBe("stopped");
        exception.InnerException.ShouldBeSameAs(cause);
        new FortranStopException("only").Message.ShouldBe("only");
    }
}
