using Dracocephalum.Fortrania.Syntax;
using Shouldly;

namespace Dracocephalum.Fortrania.Tests.Syntax;

public class FixedFormLineTests
{
    [Theory]
    [InlineData("C     A comment")]
    [InlineData("c     a lower-case comment")]
    [InlineData("*     An asterisk comment")]
    [InlineData("!     A bang comment")]
    [InlineData("")]
    [InlineData("          ")]
    public void Classify_WhenLineIsCommentOrBlank_ShouldReturnComment(string line)
    {
        // Act
        var kind = FixedFormLine.Classify(line);

        // Assert
        kind.ShouldBe(FixedFormLineKind.Comment);
    }

    [Theory]
    [InlineData("      CALL VADD ( V1, V2, VOUT )")]
    [InlineData("  100 CONTINUE")]
    [InlineData("     0X = 1")]
    [InlineData("  X")]
    public void Classify_WhenLineStartsStatement_ShouldReturnInitial(string line)
    {
        // Act
        var kind = FixedFormLine.Classify(line);

        // Assert
        kind.ShouldBe(FixedFormLineKind.Initial);
    }

    [Theory]
    [InlineData("     .           VOUT )")]
    [InlineData("     1           VOUT )")]
    [InlineData("     &")]
    public void Classify_WhenColumnSixIsMarked_ShouldReturnContinuation(string line)
    {
        // Act
        var kind = FixedFormLine.Classify(line);

        // Assert
        kind.ShouldBe(FixedFormLineKind.Continuation);
    }

    [Fact]
    public void Classify_WhenOnlySequenceFieldHasText_ShouldReturnComment()
    {
        // Arrange
        var line = new string(' ', FixedFormLine.LastSignificantColumn) + "SEQ00010";

        // Act
        var kind = FixedFormLine.Classify(line);

        // Assert
        kind.ShouldBe(FixedFormLineKind.Comment);
    }
}
