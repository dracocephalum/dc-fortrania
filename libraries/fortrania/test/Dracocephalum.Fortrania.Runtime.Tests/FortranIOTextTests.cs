using System.Text;
using Shouldly;

namespace Dracocephalum.Fortrania.Runtime.Tests;

/// <summary>
/// What the reference compiler does with formats is checked where
/// translated programs are run against its output. These tests are for
/// what such a program cannot show: the screen and keyboard, line endings
/// the reference compiler does not read, and the runtime's own settings.
/// </summary>
public unsafe class FortranIOTextTests
{
    [Fact]
    public void ReadFormatted_WhenLinesEndInAnyOfTheThreeEndings_ShouldTakeEachAsOneRecord()
    {
        // Arrange
        using var io = new FortranIO();
        io.Register("text", Encoding.ASCII.GetBytes("one\ntwo\r\nthree\rfour\r\r\nsix"));
        io.Open(11, file: "text"u8, status: "OLD"u8).ShouldBe(0);

        // Act
        var lines = new List<string>();
        while (ReadLine(io, 11, 8) is { } line)
        {
            lines.Add(line);
        }

        // Assert
        lines.ShouldBe(["one", "two", "three", "four", string.Empty, "six"]);
    }

    [Theory]
    [InlineData(4095)]
    [InlineData(4096)]
    [InlineData(4097)]
    [InlineData(20000)]
    public void ReadFormatted_WhenALineIsLongerThanWhatIsReadAhead_ShouldStillReadItWhole(int length)
    {
        // Arrange
        var text = new string('x', length) + "\r\n" + "next" + "\n";
        using var io = new FortranIO();
        io.Register("text", Encoding.ASCII.GetBytes(text));
        io.Open(11, file: "text"u8, status: "OLD"u8).ShouldBe(0);

        // Act
        var first = ReadLine(io, 11, length + 10);
        var second = ReadLine(io, 11, 10);

        // Assert
        first.ShouldBe(new string('x', length));
        second.ShouldBe("next");
        ReadLine(io, 11, 10).ShouldBeNull();
    }

    [Fact]
    public void ReadFormatted_WhenACarriageReturnIsTheLastByteReadAhead_ShouldLookForItsLineFeedInWhatFollows()
    {
        // Arrange
        var text = new string('x', 4095) + "\r\nnext\n";
        using var io = new FortranIO();
        io.Register("text", Encoding.ASCII.GetBytes(text));
        io.Open(11, file: "text"u8, status: "OLD"u8).ShouldBe(0);

        // Act & Assert
        ReadLine(io, 11, 5000).ShouldBe(new string('x', 4095));
        ReadLine(io, 11, 10).ShouldBe("next");
    }

    [Fact]
    public void WriteFormatted_WhenTheLineEndingIsSet_ShouldEndEveryRecordWithIt()
    {
        // Arrange
        using var stream = new MemoryStream();
        using var io = new FortranIO { NewLine = "\n" };
        io.Register("text", stream);
        io.Open(11, file: "text"u8).ShouldBe(0);

        // Act
        WriteLine(io, 11, "(A)", "one");
        WriteLine(io, 11, "(A,/,A)", "two", "three");
        io.Close(11).ShouldBe(0);

        // Assert
        Encoding.ASCII.GetString(stream.ToArray()).ShouldBe("one\ntwo\nthree\n");
    }

    [Fact]
    public void WriteFormatted_WhenARecordIsWrittenBeforeOthers_ShouldMakeItTheLastOfTheFile()
    {
        // Arrange
        using var stream = new MemoryStream(Encoding.ASCII.GetBytes("one\ntwo\nthree\n"));
        using var io = new FortranIO { NewLine = "\n" };
        io.Register("text", stream);
        io.Open(11, file: "text"u8, status: "OLD"u8).ShouldBe(0);
        ReadLine(io, 11, 8).ShouldBe("one");

        // Act
        WriteLine(io, 11, "(A)", "2");
        io.Close(11).ShouldBe(0);

        // Assert
        Encoding.ASCII.GetString(stream.ToArray()).ShouldBe("one\n2\n");
    }

    [Fact]
    public void WriteFormatted_WhenTheUnitIsTheScreen_ShouldWriteToTheWriterGiven()
    {
        // Arrange
        using var output = new StringWriter();
        using var error = new StringWriter();
        using var io = new FortranIO { Output = output, Error = error, NewLine = "\n" };

        // Act
        WriteLine(io, 6, "(1X,A)", "to the screen");
        WriteLine(io, 0, "(A)", "to the error output");
        WriteLine(io, 6, "(A,$)", "prompt> ");

        // Assert
        output.ToString().ShouldBe(" to the screen\nprompt> ");
        error.ToString().ShouldBe("to the error output\n");
    }

    [Fact]
    public void ReadFormatted_WhenTheUnitIsTheKeyboard_ShouldReadLinesFromTheReaderGiven()
    {
        // Arrange
        using var input = new StringReader("first\nsecond");
        using var io = new FortranIO { Input = input };

        // Act & Assert
        ReadLine(io, 5, 8).ShouldBe("first");
        ReadLine(io, 5, 8).ShouldBe("second");
        ReadLine(io, 5, 8).ShouldBeNull();
    }

    [Fact]
    public void WriteFormatted_WhenNothingIsSetForTheScreenAndKeyboard_ShouldDropWhatIsWrittenAndFindNothingToRead()
    {
        // Arrange
        using var io = new FortranIO();

        // Act
        WriteLine(io, 6, "(A)", "dropped");
        WriteLine(io, 0, "(A)", "dropped");
        var read = ReadLine(io, 5, 8);

        // Assert
        read.ShouldBeNull();
        io.Output.ShouldBeNull();
        io.Error.ShouldBeNull();
        io.Input.ShouldBeNull();
    }

    [Fact]
    public void UseConsole_WhenCalled_ShouldSetTheScreenAndKeyboardToTheConsoles()
    {
        // Arrange
        using var io = new FortranIO();

        // Act
        io.UseConsole();

        // Assert
        io.Output.ShouldBeSameAs(Console.Out);
        io.Error.ShouldBeSameAs(Console.Error);
        io.Input.ShouldBeSameAs(Console.In);
    }

    [Fact]
    public void WriteFormatted_WhenAFileIsOpenedOnAStandardUnit_ShouldWriteToTheFileFromThenOn()
    {
        // Arrange
        using var output = new StringWriter();
        using var stream = new MemoryStream();
        using var io = new FortranIO { Output = output, NewLine = "\n" };
        io.Register("log", stream);

        // Act
        io.Open(6, file: "log"u8).ShouldBe(0);
        WriteLine(io, 6, "(A)", "to the file");
        io.Close(6).ShouldBe(0);

        // Assert
        output.ToString().ShouldBeEmpty();
        Encoding.ASCII.GetString(stream.ToArray()).ShouldBe("to the file\n");
    }

    [Theory]
    [InlineData(5, false, 5001)]
    [InlineData(6, true, 5001)]
    [InlineData(19, false, 5001)]
    [InlineData(19, true, 5001)]
    public void WriteFormatted_WhenTheUnitCannotBeUsedSo_ShouldGiveTheStatus(int unit, bool reads, int expected)
    {
        // Arrange
        using var io = new FortranIO { Output = TextWriter.Null, Input = TextReader.Null };

        // Act
        int status;
        if (reads)
        {
            io.ReadFormatted(unit, "(A)"u8);
            status = io.EndRead();
        }
        else
        {
            io.WriteFormatted(unit, "(A)"u8);
            status = io.EndWrite();
        }

        // Assert
        status.ShouldBe(expected);
    }

    [Fact]
    public void WriteFormatted_WhenTheFileCannotBeWritten_ShouldGiveTheStatusOfAFileNotPermitted()
    {
        // Arrange
        using var io = new FortranIO();
        io.Register("text", new byte[4]);
        io.Open(11, file: "text"u8, status: "OLD"u8).ShouldBe(0);

        // Act
        io.WriteFormatted(11, "(A)"u8);
        var status = io.EndWrite();

        // Assert
        status.ShouldBe(13);
    }

    [Fact]
    public void Write_WhenAnIntegerMeetsCharacterEditing_ShouldWriteItsBytesAsTheReferenceCompilerDoes()
    {
        // Arrange
        using var output = new StringWriter();
        using var io = new FortranIO { Output = output, NewLine = "\n" };

        // Act
        io.WriteFormatted(6, "(A)"u8);
        io.Write(0x44434241);
        var status = io.EndWrite();

        // Assert
        status.ShouldBe(0);
        output.ToString().ShouldBe(BitConverter.IsLittleEndian ? "ABCD\n" : "DCBA\n");
    }

    [Theory]
    [InlineData("('text')")]
    [InlineData("(3X)")]
    [InlineData("()")]
    public void Write_WhenTheFormatHasNothingThatTakesAValue_ShouldGiveAFormatErrorAndNotGoRoundForEver(string format)
    {
        // Arrange
        using var io = new FortranIO { Output = TextWriter.Null };

        // Act
        io.WriteFormatted(6, Encoding.ASCII.GetBytes(format));
        io.Write(1);
        var status = io.EndWrite();

        // Assert
        status.ShouldBe(5006);
    }

    [Theory]
    [InlineData("(F8.2)", double.NaN, "     NaN")]
    [InlineData("(F9.2)", double.PositiveInfinity, " Infinity")]
    [InlineData("(F9.2)", double.NegativeInfinity, "-Infinity")]
    [InlineData("(F8.2)", double.NegativeInfinity, "    -Inf")]
    [InlineData("(E12.4)", double.PositiveInfinity, "    Infinity")]
    [InlineData("(D12.4)", 5e-324, "  0.4941-323")]
    [InlineData("(D12.4)", 1.7976931348623157e308, "  0.1798+309")]
    public void Write_WhenANumberIsNotFiniteOrAtTheEdgeOfItsRange_ShouldWriteItAsTheReferenceCompilerDoes(string format, double value, string expected)
    {
        // Arrange
        using var output = new StringWriter();
        using var io = new FortranIO { Output = output, NewLine = "\n" };

        // Act
        io.WriteFormatted(6, Encoding.ASCII.GetBytes(format));
        io.Write(value);
        io.EndWrite().ShouldBe(0);

        // Assert
        output.ToString().ShouldBe(expected + "\n");
    }

    [Fact]
    public void WriteFormatted_WhenTextIsNotAscii_ShouldPassItsBytesThroughToAFileAndDecodeThemForTheScreen()
    {
        // Arrange
        using var output = new StringWriter();
        using var stream = new MemoryStream();
        using var io = new FortranIO { Output = output, NewLine = "\n" };
        io.Register("text", stream);
        io.Open(11, file: "text"u8).ShouldBe(0);

        // Act
        WriteLine(io, 11, "(A)", "häuser ☉");
        WriteLine(io, 6, "(A)", "häuser ☉");
        io.Close(11).ShouldBe(0);

        // Assert
        Encoding.UTF8.GetString(stream.ToArray()).ShouldBe("häuser ☉\n");
        output.ToString().ShouldBe("häuser ☉\n");
    }

    [Fact]
    public void NewLine_WhenNothingIsSet_ShouldBeThePlatformsOwn()
    {
        // Arrange
        using var io = new FortranIO();

        // Act & Assert
        io.NewLine.ShouldBe(Environment.NewLine);
    }

    // Reads one record into a character value of the given length;
    // nothing at the end of the file.
    private static string? ReadLine(FortranIO io, int unit, int length)
    {
        var text = new byte[length];
        fixed (byte* first = text)
        {
            io.ReadFormatted(unit, "(A)"u8);
            io.Read(first, length);
        }

        var status = io.EndRead();
        if (status != 0)
        {
            status.ShouldBe(-1);
            return null;
        }

        return Encoding.UTF8.GetString(text).TrimEnd(' ');
    }

    private static void WriteLine(FortranIO io, int unit, string format, params string[] items)
    {
        io.WriteFormatted(unit, Encoding.ASCII.GetBytes(format));
        foreach (var item in items)
        {
            var bytes = Encoding.UTF8.GetBytes(item);
            fixed (byte* first = bytes)
            {
                io.Write(first, bytes.Length);
            }
        }

        io.EndWrite().ShouldBe(0);
    }
}
