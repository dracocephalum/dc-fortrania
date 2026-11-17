using Dracocephalum.Fortrania.Analysis;
using Dracocephalum.Fortrania.Syntax;
using Shouldly;

namespace Dracocephalum.Fortrania.Tests.Analysis;

public class TypeCheckerTests
{
    private static readonly string[] Header =
    [
        "      SUBROUTINE S0 ( PROC, ASSUMD )",
        "      IMPLICIT NONE",
        "      INTEGER I, J, IA ( 3 )",
        "      REAL R",
        "      DOUBLE PRECISION D, DA ( 0:4 ), ASSUMD ( * )",
        "      COMPLEX Z",
        "      LOGICAL L",
        "      CHARACTER*(10) C, CA ( 2 )",
        "      EXTERNAL PROC",
    ];

    [Fact]
    public void Check_WhenEveryStatementIsWellTyped_ShouldAcceptTheUnit()
    {
        // Arrange
        string[] body =
        [
            "      INTEGER N",
            "      PARAMETER ( N = 2 * 3 )",
            "      DOUBLE PRECISION PI, HALF, X, BUF ( N + 1 )",
            "      PARAMETER ( PI = 3 )",
            "      CHARACTER*(N) TAG",
            "      CHARACTER*(*) NAME",
            "      PARAMETER ( NAME = 'S0' )",
            "      EQUIVALENCE ( D, IA ( 1 ) )",
            "      DATA ( IA ( I ), I = 1, 3 ) / 3 * 0 /, C / 'X' /",
            "      HALF ( X ) = X / 2",
            "      I = D",
            "      D = I + R",
            "      Z = D",
            "      L = I .LT. J .AND. .NOT. L",
            "      C ( 1:3 ) = TAG // NAME",
            "      CA ( I ) ( 2: ) = C",
            "      DA ( IA ( 1 ) ) = HALF ( PI ) + BUF ( 1 ) + ASSUMD ( 9 )",
            "      IF ( L ) I = 1",
            "      IF ( I .GT. 0 ) THEN",
            "         DO J = 1, N, 2",
            "            CALL T ( PROC, DA, C ( 1:1 ), I + 1, SQRT ( D ) )",
            "         END DO",
            "      ELSE IF ( C .EQ. 'X' ) THEN",
            "         DO WHILE ( L )",
            "            DO D = 0.D0, 1, R",
            "            END DO",
            "         END DO",
            "      END IF",
            "      OPEN ( UNIT = I, FILE = C, STATUS = 'OLD', IOSTAT = J )",
            "      READ ( I, FMT = '(A)', IOSTAT = J ) C",
            "      WRITE ( C, * ) ( DA ( J ), J = 0, 4 ), L",
            "      PRINT *, 'Done', D * 2",
            "      INQUIRE ( FILE = C, EXIST = L )",
            "      REWIND I",
            "      CLOSE ( I )",
        ];

        // Act & Assert
        Should.NotThrow(() => Check(body));
    }

    [Theory]
    [InlineData("      I = L")]
    [InlineData("      L = I")]
    [InlineData("      C = I")]
    [InlineData("      D = C")]
    [InlineData("      L = C")]
    [InlineData("      C = L")]
    [InlineData("      IA ( R ) = 1")]
    [InlineData("      I ( 1:2 ) = 'AB'")]
    [InlineData("      I = J + L")]
    [InlineData("      IF ( I ) J = 1")]
    [InlineData("      IF ( L ) J = L")]
    [InlineData("      IF ( I ) THEN", "      END IF")]
    [InlineData("      DO WHILE ( C )", "      END DO")]
    [InlineData("      DO L = 1, 3", "      END DO")]
    [InlineData("      DO Z = 1, 3", "      END DO")]
    [InlineData("      DO I = 1, C", "      END DO")]
    [InlineData("      DO I = L, 3", "      END DO")]
    [InlineData("      DO I = 1, 3, Z", "      END DO")]
    [InlineData("      CALL T ( I + L )")]
    [InlineData("      CALL T ( IA ( 1, 2 ) )")]
    [InlineData("      WRITE ( 6, * ) I // C")]
    [InlineData("      WRITE ( I + L, * ) C")]
    [InlineData("      WRITE ( 6, * ) ( IA ( J ), J = 1, L )")]
    [InlineData("      PRINT *, -C")]
    [InlineData("      PRINT C // 1, I")]
    [InlineData("      OPEN ( UNIT = I, FILE = C // 2 )")]
    [InlineData("      CLOSE ( .NOT. I )")]
    [InlineData("      INQUIRE ( FILE = C, EXIST = L .AND. I )")]
    [InlineData("      REWIND -L")]
    public void Check_WhenStatementIsIllTyped_ShouldThrowWithItsLine(params string[] statements)
    {
        // Act & Assert
        var exception = Should.Throw<FortranAnalysisException>(() => Check(statements));
        exception.Line.ShouldBe(Header.Length + 1);
    }

    [Theory]
    [InlineData("      INTEGER BAD ( R )")]
    [InlineData("      INTEGER BAD ( 0:D )")]
    [InlineData("      INTEGER BAD ( L:3 )")]
    [InlineData("      CHARACTER*(R) BAD")]
    [InlineData("      INTEGER BAD", "      PARAMETER ( BAD = 'X' )")]
    [InlineData("      LOGICAL BAD", "      PARAMETER ( BAD = 1 )")]
    [InlineData("      CHARACTER*(3) BAD", "      PARAMETER ( BAD = 1.5 )")]
    public void Check_WhenDeclarationIsIllTyped_ShouldThrowWhereTheNameFirstAppears(params string[] statements)
    {
        // Act & Assert
        var exception = Should.Throw<FortranAnalysisException>(() => Check(statements));
        exception.Line.ShouldBe(Header.Length + 1);
    }

    [Theory]
    [InlineData("      IF ( L ) THEN", "      ELSE IF ( D ) THEN", "      END IF")]
    [InlineData("      INTEGER BAD", "      BAD ( I ) = L")]
    [InlineData("      INTEGER BAD", "      BAD ( I ) = I + C")]
    public void Check_WhenSecondStatementIsIllTyped_ShouldThrowWithItsLine(params string[] statements)
    {
        // Act & Assert
        var exception = Should.Throw<FortranAnalysisException>(() => Check(statements));
        exception.Line.ShouldBe(Header.Length + 2);
    }

    [Theory]
    [InlineData("      EQUIVALENCE ( I, IA ( R ) )")]
    [InlineData("      DATA IA ( 1, 2 ) / 0 /")]
    [InlineData("      DATA IA / R * 0 /")]
    [InlineData("      DATA ( IA ( J ), J = 1, L ) / 3 * 0 /")]
    public void Check_WhenEquivalenceOrDataIsIllTyped_ShouldThrow(string statement)
    {
        // Act & Assert
        Should.Throw<FortranAnalysisException>(() => Check(statement));
    }

    [Fact]
    public void Check_WhenIncludedDeclarationIsIllTyped_ShouldNameTheIncludeFile()
    {
        // Arrange
        string[] lines = ["      SUBROUTINE S", "      INCLUDE 'bad.inc'", "      END"];
        string[] include = ["      REAL SIZE", "      PARAMETER ( SIZE = 2.5 )", "      INTEGER BUF ( SIZE )"];

        // Act & Assert
        var exception = Should.Throw<FortranAnalysisException>(
            () => TypeChecker.Check(Build(lines, _ => SourceParser.ParseInclude(Read(include)))));
        exception.Line.ShouldBe(3);
        exception.IncludeFile.ShouldBe("bad.inc");
    }

    private static void Check(params string[] body) =>
        TypeChecker.Check(Build([.. Header, .. body, "      END"], _ => null));

    private static SymbolTable Build(string[] lines, Func<string, IncludeFile?> resolveInclude)
    {
        var unit = SourceParser.Parse(Read(lines)).Units.ShouldHaveSingleItem();
        return SymbolTableBuilder.Build(unit, resolveInclude);
    }

    private static FixedFormSource Read(string[] lines)
    {
        using var reader = new StringReader(string.Join('\n', lines));
        return FixedFormReader.Read(reader);
    }
}
