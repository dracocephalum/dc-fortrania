using Dracocephalum.Fortrania.Syntax;
using Shouldly;
using static Dracocephalum.Fortrania.Tests.Syntax.SyntaxRenderer;

namespace Dracocephalum.Fortrania.Tests.Syntax;

public class SourceParserTests
{
    [Fact]
    public void Parse_WhenFileHoldsSeveralUnits_ShouldReturnEachWithItsHeaderBodyAndEnd()
    {
        // Arrange
        string[] lines =
        [
            "      SUBROUTINE FIRST ( A )",
            "      INTEGER A",
            "      A = 1",
            "      END",
            "C     Between the two.",
            "      INTEGER FUNCTION SECOND ( B )",
            "      INTEGER B",
            "      SECOND = B",
            "      RETURN",
            "      END",
            "C     After the last.",
        ];

        // Act
        var file = SourceParser.Parse(Read(lines));

        // Assert
        file.Units.Count.ShouldBe(2);
        file.TrailingComments.ShouldBe(["C     After the last."]);

        var first = file.Units[0];
        Render(first.Header.ShouldNotBeNull().Statement).ShouldBe("SUBROUTINE FIRST(A)");
        RenderTree(first.Body).ShouldBe(["Integer: A", "A = 1"]);
        first.End.Source.Line.ShouldBe(4);

        var second = file.Units[1];
        Render(second.Header.ShouldNotBeNull().Statement).ShouldBe("Integer FUNCTION SECOND(B)");
        second.Header.Source.LeadingComments.ShouldBe(["C     Between the two."]);
        RenderTree(second.Body).ShouldBe(["Integer: B", "SECOND = B", "RETURN"]);
        second.End.Source.Line.ShouldBe(10);
    }

    [Fact]
    public void Parse_WhenMainProgramHasNoProgramStatement_ShouldReturnUnitWithoutHeader()
    {
        // Arrange
        string[] lines =
        [
            "      INTEGER FUNCTION ( 3 )",
            "      PRINT *, 'Hello'",
            "      END",
            "      PROGRAM NAMED",
            "      END",
        ];

        // Act
        var file = SourceParser.Parse(Read(lines));

        // Assert
        file.Units.Count.ShouldBe(2);
        file.Units[0].Header.ShouldBeNull();
        RenderTree(file.Units[0].Body).ShouldBe(["Integer: FUNCTION(3)", "PRINT *: 'Hello'"]);
        Render(file.Units[1].Header.ShouldNotBeNull().Statement).ShouldBe("PROGRAM NAMED");
        file.Units[1].Body.ShouldBeEmpty();
    }

    [Fact]
    public void Parse_WhenTypedFunctionTextIsNotFirstInItsUnit_ShouldReadItAsDeclaration()
    {
        // Arrange
        string[] lines =
        [
            "      SUBROUTINE S",
            "      INTEGER FUNCTION F ( 3 )",
            "      END",
            "      INTEGER FUNCTION F ( X )",
            "      END",
        ];

        // Act
        var file = SourceParser.Parse(Read(lines));

        // Assert
        RenderTree(file.Units[0].Body).ShouldBe(["Integer: FUNCTIONF(3)"]);
        Render(file.Units[1].Header.ShouldNotBeNull().Statement).ShouldBe("Integer FUNCTION F(X)");
    }

    [Fact]
    public void Parse_WhenBlocksAreNested_ShouldPlaceEachStatementInsideItsBlock()
    {
        // Arrange
        string[] lines =
        [
            "      SUBROUTINE NEST ( N, X )",
            "      DO I = 1, N",
            "         IF ( X(I) .LT. 0 ) THEN",
            "            X(I) = 0",
            "         ELSE IF ( X(I) .GT. 9 ) THEN",
            "            DO WHILE ( X(I) .GT. 9 )",
            "               X(I) = X(I) - 1",
            "            END DO",
            "         ELSE",
            "            IF ( I .EQ. 1 ) RETURN",
            "         END IF",
            "         CALL SEEN ( I )",
            "      END DO",
            "      RETURN",
            "      END",
        ];

        // Act
        var unit = SourceParser.Parse(Read(lines)).Units.ShouldHaveSingleItem();

        // Assert
        RenderTree(unit.Body).ShouldBe(
        [
            "DO I = 1, N",
            "  IF (LessThan X[I] 0) THEN",
            "    X[I] = 0",
            "  ELSE IF (GreaterThan X[I] 9) THEN",
            "    DO WHILE (GreaterThan X[I] 9)",
            "      X[I] = (Subtract X[I] 1)",
            "    END DO",
            "  ELSE",
            "    IF (Equal I 1) => RETURN",
            "  END IF",
            "  CALL SEEN[I]",
            "END DO",
            "RETURN",
        ]);
    }

    [Fact]
    public void Parse_WhenBlocksAreEmpty_ShouldReturnThemWithEmptyBodies()
    {
        // Arrange
        string[] lines =
        [
            "      SUBROUTINE EMPTY",
            "      IF ( A ) THEN",
            "      ELSE",
            "      END IF",
            "      DO I = 1, 3",
            "      END DO",
            "      END",
        ];

        // Act
        var unit = SourceParser.Parse(Read(lines)).Units.ShouldHaveSingleItem();

        // Assert
        var block = unit.Body[0].ShouldBeOfType<Construct.If>();
        block.Branches.Count.ShouldBe(2);
        block.Branches.ShouldAllBe(branch => branch.Body.Count == 0);
        block.End.Source.Line.ShouldBe(4);

        var loop = unit.Body[1].ShouldBeOfType<Construct.Loop>();
        loop.Body.ShouldBeEmpty();
        loop.Header.Source.Line.ShouldBe(5);
        loop.End.Source.Line.ShouldBe(6);
    }

    [Fact]
    public void Parse_WhenSubroutineHasEntries_ShouldKeepThemInTheBodyInOrder()
    {
        // Arrange
        string[] lines =
        [
            "      SUBROUTINE UMBRELLA ( A, B )",
            "      RETURN",
            "      ENTRY FIRST ( A )",
            "      A = 1",
            "      RETURN",
            "      ENTRY SECOND ( B )",
            "      B = 2",
            "      RETURN",
            "      END",
        ];

        // Act
        var unit = SourceParser.Parse(Read(lines)).Units.ShouldHaveSingleItem();

        // Assert
        RenderTree(unit.Body).ShouldBe(
            ["RETURN", "ENTRY FIRST(A)", "A = 1", "RETURN", "ENTRY SECOND(B)", "B = 2", "RETURN"]);
    }

    [Fact]
    public void Parse_WhenFileHasNoStatements_ShouldReturnNoUnits()
    {
        // Act
        var file = SourceParser.Parse(Read("C     Nothing here."));

        // Assert
        file.Units.ShouldBeEmpty();
        file.TrailingComments.ShouldBe(["C     Nothing here."]);
    }

    [Theory]
    [InlineData(2, "      SUBROUTINE S", "      X = 1")]
    [InlineData(3, "      SUBROUTINE S", "      X = 1", "      SUBROUTINE T", "      END")]
    [InlineData(4, "      SUBROUTINE S", "      IF ( A ) THEN", "      X = 1", "      END")]
    [InlineData(4, "      SUBROUTINE S", "      DO I = 1, 3", "      X = 1", "      END")]
    [InlineData(2, "      SUBROUTINE S", "      END IF", "      END")]
    [InlineData(2, "      SUBROUTINE S", "      END DO", "      END")]
    [InlineData(2, "      SUBROUTINE S", "      ELSE", "      END")]
    [InlineData(2, "      SUBROUTINE S", "      ELSE IF ( A ) THEN", "      END")]
    [InlineData(4, "      SUBROUTINE S", "      IF ( A ) THEN", "      DO I = 1, 3", "      END IF", "      END DO", "      END")]
    [InlineData(4, "      SUBROUTINE S", "      DO I = 1, 3", "      IF ( A ) THEN", "      END DO", "      END IF", "      END")]
    [InlineData(5, "      SUBROUTINE S", "      DO I = 1, 3", "      IF ( A ) THEN", "      ELSE", "      END DO", "      END")]
    [InlineData(4, "      SUBROUTINE S", "      IF ( A ) THEN", "      ELSE", "      ELSE", "      END IF", "      END")]
    [InlineData(4, "      SUBROUTINE S", "      IF ( A ) THEN", "      ELSE", "      ELSE IF ( B ) THEN", "      END IF", "      END")]
    [InlineData(3, "      SUBROUTINE S", "      IF ( A ) THEN", "      ENTRY E", "      END IF", "      END")]
    [InlineData(3, "      SUBROUTINE S", "      DO I = 1, 3", "      ENTRY E", "      END DO", "      END")]
    [InlineData(2, "      PROGRAM P", "      ENTRY E", "      END")]
    [InlineData(2, "      X = 1", "      ENTRY E", "      END")]
    [InlineData(2, "      SUBROUTINE S", "      DO 10 I = 1, 3", "   10 CONTINUE", "      END")]
    [InlineData(2, "      SUBROUTINE S", "      DO 10 WHILE ( A )", "   10 CONTINUE", "      END")]
    [InlineData(2, "      SUBROUTINE S", "      GO TO 10", "      END")]
    public void Parse_WhenStructureIsBrokenOrUnsupported_ShouldThrowWithTheOffendingLine(int line, params string[] lines)
    {
        // Act & Assert
        var exception = Should.Throw<FortranSyntaxException>(() => SourceParser.Parse(Read(lines)));
        exception.Line.ShouldBe(line);
    }

    [Fact]
    public void Parse_WhenBlockIsStillOpenAtEnd_ShouldNameTheLineThatOpenedIt()
    {
        // Arrange
        string[] lines =
        [
            "      SUBROUTINE S",
            "      DO I = 1, 3",
            "      IF ( A ) THEN",
            "      END DO",
            "      END",
        ];

        // Act & Assert
        var exception = Should.Throw<FortranSyntaxException>(() => SourceParser.Parse(Read(lines)));
        exception.Message.ShouldBe("Line 4: END DO has no DO to close; the innermost open block is the block IF on line 3.");
    }

    [Fact]
    public void ParseInclude_WhenFileHoldsDeclarations_ShouldReturnThemWithTheTrailingComments()
    {
        // Arrange
        string[] lines =
        [
            "C     Sizes.",
            "      INTEGER MAXLEN",
            "      PARAMETER ( MAXLEN = 80 )",
            "C     End of sizes.",
        ];

        // Act
        var file = SourceParser.ParseInclude(Read(lines));

        // Assert
        RenderTree(file.Body).Length.ShouldBe(2);
        file.Body[0].ShouldBeOfType<Construct.Simple>().Statement.Source.LeadingComments.ShouldBe(["C     Sizes."]);
        file.TrailingComments.ShouldBe(["C     End of sizes."]);
    }

    [Fact]
    public void ParseInclude_WhenFirstStatementLooksLikeTypedFunction_ShouldReadItAsDeclaration()
    {
        // Act
        var file = SourceParser.ParseInclude(Read("      INTEGER FUNCTION F ( 3 )"));

        // Assert
        RenderTree(file.Body).ShouldBe(["Integer: FUNCTIONF(3)"]);
    }

    [Fact]
    public void ParseInclude_WhenFileHoldsBlocksAndAnEntry_ShouldAssembleThem()
    {
        // Arrange
        string[] lines =
        [
            "      ENTRY E ( X )",
            "      IF ( X .GT. 0 ) THEN",
            "         X = 0",
            "      END IF",
        ];

        // Act
        var file = SourceParser.ParseInclude(Read(lines));

        // Assert
        RenderTree(file.Body).ShouldBe(["ENTRY E(X)", "IF (GreaterThan X 0) THEN", "  X = 0", "END IF"]);
    }

    [Theory]
    [InlineData(1, "      SUBROUTINE S")]
    [InlineData(2, "      INTEGER N", "      END")]
    [InlineData(2, "      IF ( A ) THEN", "      X = 1")]
    [InlineData(1, "      END IF")]
    public void ParseInclude_WhenFileIsNotSelfContainedStatements_ShouldThrowWithTheOffendingLine(int line, params string[] lines)
    {
        // Act & Assert
        var exception = Should.Throw<FortranSyntaxException>(() => SourceParser.ParseInclude(Read(lines)));
        exception.Line.ShouldBe(line);
    }

    private static FixedFormSource Read(params string[] lines)
    {
        using var reader = new StringReader(string.Join('\n', lines));
        return FixedFormReader.Read(reader);
    }
}
