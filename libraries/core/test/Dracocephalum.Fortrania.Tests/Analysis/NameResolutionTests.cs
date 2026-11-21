using Dracocephalum.Fortrania.Analysis;
using Dracocephalum.Fortrania.Syntax;
using Shouldly;

namespace Dracocephalum.Fortrania.Tests.Analysis;

public class NameResolutionTests
{
    [Fact]
    public void Build_WhenNamesAreUsedInEveryWay_ShouldDecideWhatEachStandsFor()
    {
        // Arrange
        string[] lines =
        [
            "      SUBROUTINE DEMO ( N, A, CALLBK, FUNARG, PASSED )",
            "      IMPLICIT NONE",
            "      INTEGER N, I, LIMIT, RTRIM, COUNT",
            "      DOUBLE PRECISION A ( N ), X, TWICE, FUNARG, HALF, NORM",
            "      CHARACTER*(8) WORD",
            "      PARAMETER ( LIMIT = 3 )",
            "      EXTERNAL CALLBK, PASSED, NORM",
            "      TWICE ( X ) = 2 * X",
            "      DO I = 1, MIN ( N, LIMIT )",
            "         A ( I ) = TWICE ( A ( I ) ) + SQRT ( HALF ( I ) )",
            "     .           + FUNARG ( I )",
            "      END DO",
            "      COUNT = RTRIM ( WORD ) + LEN ( WORD ( 1:2 ) )",
            "      CALL CALLBK ( COUNT )",
            "      CALL REPORT ( WORD, PASSED, NORM )",
            "      END",
        ];

        // Act
        var table = Build(lines);

        // Assert
        Kinds(table).ShouldBe(
        [
            "DEMO Subroutine",
            "N Variable",
            "A Variable",
            "CALLBK Subroutine",
            "FUNARG ExternalFunction",
            "PASSED ExternalProcedure",
            "I Variable",
            "LIMIT Constant",
            "RTRIM ExternalFunction",
            "COUNT Variable",
            "X Variable",
            "TWICE StatementFunction",
            "HALF ExternalFunction",
            "NORM ExternalFunction",
            "WORD Variable",
            "MIN IntrinsicFunction",
            "SQRT IntrinsicFunction",
            "LEN IntrinsicFunction",
            "REPORT Subroutine",
        ]);

        table.Find("DEMO").ShouldNotBeNull().Type.ShouldBeNull();
        table.Find("REPORT").ShouldNotBeNull().Type.ShouldBeNull();
        table.Find("PASSED").ShouldNotBeNull().Type.ShouldBeNull();
        table.Find("MIN").ShouldNotBeNull().Type.ShouldBeNull();
        table.Find("HALF").ShouldNotBeNull().Type.ShouldNotBeNull().Type.ShouldBe(DataType.DoublePrecision);
        table.Find("CALLBK").ShouldNotBeNull().IsDummyArgument.ShouldBeTrue();
    }

    [Fact]
    public void Build_WhenUnitHasNoImplicitNone_ShouldTypeUndeclaredNamesByFirstLetter()
    {
        // Arrange
        string[] lines =
        [
            "      FUNCTION KOUNT ( X )",
            "      PARAMETER ( MAXN = 10, SCALE = 2.0 )",
            "      DOUBLE PRECISION D",
            "      D = HELPER ( X ) * SCALE",
            "      KOUNT = MAXN + NEXT ( D ) + INT ( D )",
            "      CALL DONE",
            "      END",
        ];

        // Act
        var table = Build(lines);

        // Assert
        table.Symbols.Select(symbol => $"{symbol.Name} {symbol.Kind} {symbol.Type?.Type} {symbol.IsTypeImplicit}").ShouldBe(
        [
            "KOUNT Variable Integer True",
            "X Variable Real True",
            "MAXN Constant Integer True",
            "SCALE Constant Real True",
            "D Variable DoublePrecision False",
            "HELPER ExternalFunction Real True",
            "NEXT ExternalFunction Integer True",
            "INT IntrinsicFunction  False",
            "DONE Subroutine  False",
        ]);
    }

    [Fact]
    public void Build_WhenIntrinsicNameIsMadeExternalOrIsAnArgument_ShouldNotTreatItAsIntrinsic()
    {
        // Arrange
        string[] lines =
        [
            "      SUBROUTINE S ( MAX, Y )",
            "      EXTERNAL SIN",
            "      REAL MIN ( 3 )",
            "      INTRINSIC COS",
            "      REAL ABS",
            "      Y = SIN ( Y ) + MAX ( Y ) + MIN ( 1 ) + ABS ( Y )",
            "      CALL T ( COS, MOD )",
            "      END",
        ];

        // Act
        var table = Build(lines);

        // Assert
        table.Find("SIN").ShouldNotBeNull().Kind.ShouldBe(SymbolKind.ExternalFunction);
        table.Find("MAX").ShouldNotBeNull().Kind.ShouldBe(SymbolKind.ExternalFunction);
        table.Find("MIN").ShouldNotBeNull().Kind.ShouldBe(SymbolKind.Variable);
        table.Find("COS").ShouldNotBeNull().Kind.ShouldBe(SymbolKind.IntrinsicFunction);
        table.Find("MOD").ShouldNotBeNull().Kind.ShouldBe(SymbolKind.Variable);

        var abs = table.Find("ABS").ShouldNotBeNull();
        abs.Kind.ShouldBe(SymbolKind.IntrinsicFunction);
        abs.Type.ShouldNotBeNull().Type.ShouldBe(DataType.Real);
    }

    [Fact]
    public void Build_WhenBitFunctionIsUsed_ShouldTreatItAsIntrinsic()
    {
        // Act
        var table = Build("      SUBROUTINE S ( I )", "      IMPLICIT NONE", "      INTEGER I", "      I = ISHFT ( I, 8 )", "      END");

        // Assert
        table.Find("ISHFT").ShouldNotBeNull().Kind.ShouldBe(SymbolKind.IntrinsicFunction);
    }

    [Fact]
    public void Build_WhenNamesAppearOnlyInInputOutputAndData_ShouldStillFindThem()
    {
        // Arrange
        string[] lines =
        [
            "      SUBROUTINE S",
            "      INTEGER TABLE ( 3 ), K",
            "      PARAMETER ( NVALS = 3, ZERO = 0 )",
            "      DATA ( TABLE ( K ), K = 1, NVALS ) / NVALS * ZERO /",
            "      WRITE ( UNIT = LUN, FMT = FRMT, IOSTAT = IOS )",
            "     .      ( ROW ( J ), J = 1, WIDTH ( LUN ) )",
            "      READ ( LUN, * ) NAME ( FIRST : LAST )",
            "      IF ( IOS .NE. 0 ) STOP",
            "      DO WHILE ( MORE ( LUN ) )",
            "         PRINT *, TOTAL",
            "      END DO",
            "      END",
        ];

        // Act
        var table = Build(lines);

        // Assert
        Kinds(table).ShouldBe(
        [
            "S Subroutine",
            "TABLE Variable",
            "K Variable",
            "NVALS Constant",
            "ZERO Constant",
            "LUN Variable",
            "FRMT Variable",
            "IOS Variable",
            "ROW ExternalFunction",
            "J Variable",
            "WIDTH ExternalFunction",
            "NAME Variable",
            "FIRST Variable",
            "LAST Variable",
            "MORE ExternalFunction",
            "TOTAL Variable",
        ]);
    }

    [Fact]
    public void Build_WhenFunctionAssignsItsOwnNameAndEntries_ShouldTreatThemAsVariables()
    {
        // Arrange
        string[] lines =
        [
            "      INTEGER FUNCTION F ( X )",
            "      INTEGER G",
            "      F = 1",
            "      RETURN",
            "      ENTRY G ( X )",
            "      G = F + 1",
            "      END",
        ];

        // Act
        var table = Build(lines);

        // Assert
        table.Find("F").ShouldNotBeNull().Kind.ShouldBe(SymbolKind.Variable);
        table.Find("G").ShouldNotBeNull().Kind.ShouldBe(SymbolKind.Variable);
    }

    [Theory]
    [InlineData(3, "      INTEGER I", "      I = J")]
    [InlineData(3, "      INTEGER I", "      I = F ( 1 )")]
    [InlineData(2, "      DIMENSION A ( 3 )")]
    [InlineData(2, "      PARAMETER ( N = 1 )")]
    [InlineData(2, "      F ( X ) = 1")]
    [InlineData(3, "      INTEGER F", "      F ( X ) = 1")]
    public void Build_WhenNameHasNoTypeUnderImplicitNone_ShouldThrowWhereTheNameFirstAppears(int line, params string[] body)
    {
        // Act & Assert
        var exception = Should.Throw<FortranAnalysisException>(() => Build(["      SUBROUTINE S", "      IMPLICIT NONE", .. body, "      END"]).Symbols.Count.ShouldBe(0));
        exception.Line.ShouldBe(line + 1);
        exception.Message.ShouldEndWith("has no type, and the program unit says IMPLICIT NONE.");
    }

    [Theory]
    [InlineData(3, "      PARAMETER ( N = 1 )", "      N = 2")]
    [InlineData(3, "      PARAMETER ( N = 1 )", "      CALL N")]
    [InlineData(3, "      PARAMETER ( N = 1 )", "      X = N ( 1 )")]
    [InlineData(3, "      F ( X ) = X", "      CALL F ( 1 )")]
    [InlineData(3, "      F ( X ) = X", "      CALL T ( F )")]
    [InlineData(3, "      DIMENSION A ( 3 )", "      CALL A")]
    [InlineData(2, "      CALL S")]
    [InlineData(2, "      X = S ( 1 )")]
    [InlineData(2, "      X = S")]
    [InlineData(2, "      S = 1")]
    [InlineData(1, "      INTEGER S")]
    [InlineData(2, "      INTRINSIC NOSUCH")]
    [InlineData(3, "      INTRINSIC SIN", "      CALL SIN ( X )")]
    [InlineData(2, "      X = T ( 1 )", "      CALL T")]
    [InlineData(3, "      INTEGER T", "      CALL T")]
    [InlineData(3, "      CALL T", "      X = T")]
    [InlineData(4, "      SIN ( X ) = 1", "      Y = 1", "      SIN ( X ) = 2")]
    [InlineData(3, "      Y = 1", "      F ( 1 ) = 2")]
    [InlineData(3, "      EXTERNAL E", "      E = 1")]
    [InlineData(3, "      CALL T", "      DO T = 1, 3", "      END DO")]
    public void Build_WhenNameIsUsedAgainstWhatItIs_ShouldThrowWithTheOffendingLine(int line, params string[] body)
    {
        // Act & Assert
        var exception = Should.Throw<FortranAnalysisException>(() => Build(["      SUBROUTINE S", .. body, "      END"]));
        exception.Line.ShouldBe(line);
    }

    [Theory]
    [InlineData(2, "      F = F ( 1 )")]
    [InlineData(2, "      CALL F")]
    public void Build_WhenFunctionInvokesItself_ShouldThrowWithTheOffendingLine(int line, params string[] body)
    {
        // Act & Assert
        var exception = Should.Throw<FortranAnalysisException>(() => Build(["      FUNCTION F ( X )", .. body, "      END"]));
        exception.Line.ShouldBe(line);
        exception.Message.ShouldContain("no recursion");
    }

    private static string[] Kinds(SymbolTable table) => [.. table.Symbols.Select(symbol => $"{symbol.Name} {symbol.Kind}")];

    private static SymbolTable Build(params string[] lines)
    {
        using var reader = new StringReader(string.Join('\n', lines));
        var unit = SourceParser.Parse(FixedFormReader.Read(reader)).Units.ShouldHaveSingleItem();
        return SymbolTableBuilder.Build(unit, _ => null);
    }
}
