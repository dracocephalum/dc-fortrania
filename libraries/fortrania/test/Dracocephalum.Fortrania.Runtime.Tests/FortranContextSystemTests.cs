using System.Text;
using Shouldly;

namespace Dracocephalum.Fortrania.Runtime.Tests;

public unsafe class FortranContextSystemTests
{
    [Fact]
    public void Exit_WhenCalled_ShouldThrowWithTheStatus()
    {
        // Arrange
        using var context = new FortranContext();

        // Act & Assert
        Should.Throw<FortranStopException>(() => ExitWith(context, 7)).ExitCode.ShouldBe(7);
        Should.Throw<FortranStopException>(() => context.Exit()).ExitCode.ShouldBe(0);
    }

    private static void ExitWith(FortranContext context, int status) => context.Exit(&status);

    [Fact]
    public void GetArgument_WhenTheCommandLineIsSet_ShouldGiveEachPartAndBlanksPastTheLast()
    {
        // Arrange
        using var context = new FortranContext { CommandLine = ["tool", "first", "the second one"] };

        // Act & Assert
        context.ArgumentCount().ShouldBe(2);
        Argument(context, 0, 6).ShouldBe("tool  ");
        Argument(context, 1, 6).ShouldBe("first ");
        Argument(context, 2, 6).ShouldBe("the se");
        Argument(context, 3, 6).ShouldBe("      ");
        Argument(context, -1, 6).ShouldBe("      ");
    }

    [Fact]
    public void ArgumentCount_WhenNoCommandLineIsSet_ShouldBeZero()
    {
        // Arrange
        using var context = new FortranContext();

        // Act & Assert
        context.ArgumentCount().ShouldBe(0);
        Argument(context, 0, 4).ShouldBe("    ");
    }

    [Fact]
    public void GetEnvironment_WhenAVariableIsSetOrNot_ShouldGiveItsValueOrBlanks()
    {
        // Arrange
        using var context = new FortranContext();
        Environment.SetEnvironmentVariable("FORTRANIA_RUNTIME_TEST", "värde");

        // Act & Assert
        Variable(context, "FORTRANIA_RUNTIME_TEST   ", 8).ShouldBe("värde  ");
        Variable(context, "FORTRANIA_RUNTIME_UNSET", 4).ShouldBe("    ");
        Variable(context, "   ", 4).ShouldBe("    ");
    }

    [Fact]
    public void DateAndTime_WhenTheClockIsGiven_ShouldLayTheMomentOutAsTheReferenceCompilerDoes()
    {
        // Arrange
        var moment = new DateTimeOffset(2026, 11, 19, 13, 16, 31, 882, TimeSpan.FromHours(13));
        using var context = new FortranContext { Clock = new FixedClock(moment) };
        byte* date = stackalloc byte[8];
        byte* time = stackalloc byte[10];
        byte* zone = stackalloc byte[5];
        int* values = stackalloc int[8];

        // Act
        context.DateAndTime(date, 8, time, 10, zone, 5, values);

        // Assert
        Encoding.ASCII.GetString(date, 8).ShouldBe("20261119");
        Encoding.ASCII.GetString(time, 10).ShouldBe("131631.882");
        Encoding.ASCII.GetString(zone, 5).ShouldBe("+1300");
        new ReadOnlySpan<int>(values, 8).ToArray().ShouldBe([2026, 11, 19, 780, 13, 16, 31, 882]);
    }

    [Fact]
    public void DateAndTime_WhenTheZoneIsBehindUniversalTimeAndFewerAnswersAreAskedFor_ShouldGiveThoseAsked()
    {
        // Arrange
        var moment = new DateTimeOffset(2001, 2, 3, 4, 5, 6, 7, new TimeSpan(-9, -30, 0));
        using var context = new FortranContext { Clock = new FixedClock(moment) };
        byte* date = stackalloc byte[8];
        byte* time = stackalloc byte[10];
        byte* zone = stackalloc byte[5];

        // Act
        context.DateAndTime(date, 8);
        context.DateAndTime(date, 8, time, 10);
        context.DateAndTime(date, 8, time, 10, zone, 5);

        // Assert
        Encoding.ASCII.GetString(date, 8).ShouldBe("20010203");
        Encoding.ASCII.GetString(time, 10).ShouldBe("040506.007");
        Encoding.ASCII.GetString(zone, 5).ShouldBe("-0930");
    }

    [Fact]
    public void RunCommand_WhenCommandsAreNotAllowed_ShouldRunNothingAndSaySo()
    {
        // Arrange
        using var context = new FortranContext();

        // Act & Assert
        context.AllowsCommands.ShouldBeFalse();
        Run(context, "exit 3").ShouldBe(-1);
    }

    [Fact]
    public void RunCommand_WhenCommandsAreAllowed_ShouldRunTheCommandAndGiveItsStatus()
    {
        // Arrange
        using var context = new FortranContext { AllowsCommands = true };

        // Act & Assert
        Run(context, "exit 3   ").ShouldBe(3);
        Run(context, "exit 0").ShouldBe(0);
    }

    private static string Argument(FortranContext context, int position, int length)
    {
        byte* value = stackalloc byte[length];
        context.GetArgument(&position, value, length);
        return Encoding.UTF8.GetString(value, length);
    }

    private static string Variable(FortranContext context, string name, int length)
    {
        var bytes = Encoding.UTF8.GetBytes(name);
        byte* value = stackalloc byte[length];
        fixed (byte* first = bytes)
        {
            context.GetEnvironment(first, bytes.Length, value, length);
        }

        return Encoding.UTF8.GetString(value, length);
    }

    private static int Run(FortranContext context, string command)
    {
        var bytes = Encoding.UTF8.GetBytes(command);
        int status = 99;
        fixed (byte* first = bytes)
        {
            context.RunCommand(first, bytes.Length, &status);
        }

        return status;
    }

    private sealed class FixedClock(DateTimeOffset moment) : TimeProvider
    {
        public override TimeZoneInfo LocalTimeZone { get; } =
            TimeZoneInfo.CreateCustomTimeZone("fixed", moment.Offset, "fixed", "fixed");

        public override DateTimeOffset GetUtcNow() => moment.ToUniversalTime();
    }
}
