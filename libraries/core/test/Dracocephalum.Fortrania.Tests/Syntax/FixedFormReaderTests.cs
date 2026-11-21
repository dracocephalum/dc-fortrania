using Dracocephalum.Fortrania.Syntax;
using Shouldly;

namespace Dracocephalum.Fortrania.Tests.Syntax;

public class FixedFormReaderTests
{
    [Fact]
    public void Read_WhenStatementIsContinued_ShouldJoinTheStatementFields()
    {
        // Arrange
        string[] lines =
        [
            "      CALL VADD ( V1,",
            "     .            V2,",
            "     .            VOUT )",
        ];

        // Act
        var source = Read(lines);

        // Assert
        var statement = source.Statements.ShouldHaveSingleItem();
        statement.Text.ShouldBe("CALL VADD ( V1,            V2,            VOUT )");
        statement.Line.ShouldBe(1);
        statement.Label.ShouldBeNull();
    }

    [Fact]
    public void Read_WhenLinesAreSeparateStatements_ShouldReturnEachWithItsLine()
    {
        // Arrange
        string[] lines =
        [
            "      X = 1",
            "      Y = 2",
        ];

        // Act
        var source = Read(lines);

        // Assert
        source.Statements.Select(s => (s.Text, s.Line)).ShouldBe([("X = 1", 1), ("Y = 2", 2)]);
    }

    [Theory]
    [InlineData("  100 CONTINUE", 100)]
    [InlineData("1 0 0 CONTINUE", 100)]
    [InlineData("    7 CONTINUE", 7)]
    public void Read_WhenLabelFieldHasDigits_ShouldReturnTheLabel(string line, int expected)
    {
        // Act
        var source = Read(line);

        // Assert
        var statement = source.Statements.ShouldHaveSingleItem();
        statement.Label.ShouldBe(expected);
        statement.Text.ShouldBe("CONTINUE");
    }

    [Fact]
    public void Read_WhenCommentsSurroundStatements_ShouldAttachEachToItsPlace()
    {
        // Arrange
        string[] lines =
        [
            "C     Before the first.",
            "      CALL FOO ( A,",
            "C     Between the lines.",
            "     .           B )",
            string.Empty,
            "C     Before the second.",
            "      RETURN",
            "C     After the last.",
        ];

        // Act
        var source = Read(lines);

        // Assert
        source.Statements.Count.ShouldBe(2);
        source.Statements[0].LeadingComments.ShouldBe(["C     Before the first."]);
        source.Statements[0].InteriorComments.ShouldBe(["C     Between the lines."]);
        source.Statements[1].LeadingComments.ShouldBe([string.Empty, "C     Before the second."]);
        source.Statements[1].InteriorComments.ShouldBeEmpty();
        source.TrailingComments.ShouldBe(["C     After the last."]);
    }

    [Fact]
    public void Read_WhenTextRunsPastColumn72_ShouldIgnoreTheSequenceField()
    {
        // Arrange
        var line = "      X = 1".PadRight(FixedFormLine.LastSignificantColumn) + "SEQ00010";

        // Act
        var source = Read(line);

        // Assert
        source.Statements.ShouldHaveSingleItem().Text.ShouldBe("X = 1");
    }

    [Fact]
    public void Read_WhenCharacterConstantIsContinued_ShouldPadTheShortLineToColumn72()
    {
        // Arrange
        string[] lines =
        [
            "      S = 'AB",
            "     .CD'",
        ];

        // Act
        var source = Read(lines);

        // Assert
        // The first line ends at column 13, so 59 blanks belong to the constant.
        var expected = "S = 'AB" + new string(' ', 59) + "CD'";
        source.Statements.ShouldHaveSingleItem().Text.ShouldBe(expected);
    }

    [Fact]
    public void Read_WhenDoubledApostropheIsInConstant_ShouldNotTreatTheLineAsOpen()
    {
        // Arrange
        string[] lines =
        [
            "      S = 'IT''S' //",
            "     .    'OK'",
        ];

        // Act
        var source = Read(lines);

        // Assert
        source.Statements.ShouldHaveSingleItem().Text.ShouldBe("S = 'IT''S' //    'OK'");
    }

    [Fact]
    public void Read_WhenContinuationHasNoStatement_ShouldThrow()
    {
        // Arrange
        string[] lines =
        [
            "C     A comment.",
            "     .    X = 1",
        ];

        // Act & Assert
        var exception = Should.Throw<FixedFormSyntaxException>(() => Read(lines));
        exception.Line.ShouldBe(2);
    }

    [Fact]
    public void Read_WhenLabelFieldHasALetter_ShouldThrow()
    {
        // Act & Assert
        var exception = Should.Throw<FixedFormSyntaxException>(() => Read("  X   Y = 1"));
        exception.Line.ShouldBe(1);
    }

    [Fact]
    public void Read_WhenSourceIsEmpty_ShouldReturnNothing()
    {
        // Act
        var source = Read();

        // Assert
        source.Statements.ShouldBeEmpty();
        source.TrailingComments.ShouldBeEmpty();
    }

    private static FixedFormSource Read(params string[] lines)
    {
        using var reader = new StringReader(string.Join('\n', lines));
        return FixedFormReader.Read(reader);
    }
}
