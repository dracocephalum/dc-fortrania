using Dracocephalum.Fortrania.Analysis;
using Dracocephalum.Fortrania.Syntax;
using Dracocephalum.Fortrania.Tests.Syntax;
using Shouldly;

namespace Dracocephalum.Fortrania.Tests.Analysis;

public class SymbolTableBuilderTests
{
    [Fact]
    public void Build_WhenSubroutineDeclaresItsNames_ShouldRecordWhatEachDeclarationSays()
    {
        // Arrange
        string[] lines =
        [
            "      SUBROUTINE DEMO ( N, X, NAME, F )",
            "      IMPLICIT NONE",
            "      INTEGER N, COUNT",
            "      DOUBLE PRECISION X ( 3, * ), WORK",
            "      CHARACTER*(*) NAME",
            "      CHARACTER*(80) LINE, SHORT*8",
            "      DIMENSION WORK ( 0:N )",
            "      INTEGER LIMIT",
            "      PARAMETER ( LIMIT = 10 )",
            "      EXTERNAL F",
            "      INTRINSIC SIN",
            "      SAVE COUNT",
            "      COUNT = COUNT + 1",
            "      END",
        ];

        // Act
        var table = Build(lines);

        // Assert
        table.IsImplicitNone.ShouldBeTrue();
        table.SavesEverything.ShouldBeFalse();
        table.Symbols.Select(symbol => symbol.Name).ShouldBe(
            ["DEMO", "N", "X", "NAME", "F", "COUNT", "WORK", "LINE", "SHORT", "LIMIT", "SIN"]);

        var demo = table.Find("DEMO").ShouldNotBeNull();
        demo.IsEntryPoint.ShouldBeTrue();
        demo.DeclaredType.ShouldBeNull();

        var n = table.Find("N").ShouldNotBeNull();
        n.IsDummyArgument.ShouldBeTrue();
        n.DeclaredType.ShouldNotBeNull().Type.ShouldBe(DataType.Integer);
        n.IsArray.ShouldBeFalse();

        var x = table.Find("X").ShouldNotBeNull();
        x.Dimensions.Count.ShouldBe(2);
        x.Dimensions[1].Upper.ShouldBeNull();

        table.Find("NAME").ShouldNotBeNull().DeclaredType.ShouldNotBeNull().Length.ShouldNotBeNull().IsAssumed.ShouldBeTrue();
        RenderLength(table.Find("LINE")).ShouldBe("80");
        RenderLength(table.Find("SHORT")).ShouldBe("8");

        var work = table.Find("WORK").ShouldNotBeNull();
        work.DeclaredType.ShouldNotBeNull().Type.ShouldBe(DataType.DoublePrecision);
        SyntaxRenderer.Render(work.Dimensions.ShouldHaveSingleItem().Upper).ShouldBe("N");

        var limit = table.Find("LIMIT").ShouldNotBeNull();
        limit.IsConstant.ShouldBeTrue();
        SyntaxRenderer.Render(limit.ConstantValue).ShouldBe("10");

        var f = table.Find("F").ShouldNotBeNull();
        f.IsExternal.ShouldBeTrue();
        f.IsDummyArgument.ShouldBeTrue();

        table.Find("SIN").ShouldNotBeNull().IsIntrinsic.ShouldBeTrue();
        table.Find("COUNT").ShouldNotBeNull().IsSaved.ShouldBeTrue();
        table.Find("NOWHERE").ShouldBeNull();
        table.Symbols.ShouldAllBe(symbol => symbol.IncludeFile == null);
    }

    [Fact]
    public void Build_WhenFunctionHasEntries_ShouldListEveryWayInAndEveryArgument()
    {
        // Arrange
        string[] lines =
        [
            "      DOUBLE PRECISION FUNCTION AREA ( R )",
            "      DOUBLE PRECISION R, S, SQUARE",
            "      AREA = 3 * R * R",
            "      RETURN",
            "      ENTRY SQUARE ( S )",
            "      SQUARE = S * S",
            "      RETURN",
            "      END",
        ];

        // Act
        var table = Build(lines);

        // Assert
        table.EntryPoints.Select(entry => entry.Source.Line).ShouldBe([1, 5]);
        table.IsImplicitNone.ShouldBeFalse();

        var area = table.Find("AREA").ShouldNotBeNull();
        area.IsEntryPoint.ShouldBeTrue();
        area.DeclaredType.ShouldNotBeNull().Type.ShouldBe(DataType.DoublePrecision);

        var square = table.Find("SQUARE").ShouldNotBeNull();
        square.IsEntryPoint.ShouldBeTrue();
        square.DeclaredType.ShouldNotBeNull().Type.ShouldBe(DataType.DoublePrecision);

        table.Find("R").ShouldNotBeNull().IsDummyArgument.ShouldBeTrue();
        table.Find("S").ShouldNotBeNull().IsDummyArgument.ShouldBeTrue();
    }

    [Fact]
    public void Build_WhenFunctionTypeIsDeclaredInTheBody_ShouldTypeTheFunctionName()
    {
        // Arrange
        string[] lines =
        [
            "      FUNCTION COUNT ( X )",
            "      INTEGER COUNT",
            "      COUNT = 1",
            "      END",
        ];

        // Act
        var table = Build(lines);

        // Assert
        table.Find("COUNT").ShouldNotBeNull().DeclaredType.ShouldNotBeNull().Type.ShouldBe(DataType.Integer);
        table.Find("X").ShouldNotBeNull().DeclaredType.ShouldBeNull();
    }

    [Fact]
    public void Build_WhenUnitIsMainProgram_ShouldHaveNoEntryPoints()
    {
        // Act
        var table = Build("      PROGRAM MAIN", "      INTEGER I", "      SAVE", "      I = 1", "      END");

        // Assert
        table.EntryPoints.ShouldBeEmpty();
        table.SavesEverything.ShouldBeTrue();
        table.Symbols.ShouldHaveSingleItem().Name.ShouldBe("I");
    }

    [Fact]
    public void Build_WhenUnitHasDataAndEquivalence_ShouldCollectThemInOrder()
    {
        // Arrange
        string[] lines =
        [
            "      SUBROUTINE S",
            "      INTEGER I, J, K ( 2 )",
            "      DOUBLE PRECISION D",
            "      EQUIVALENCE ( D, K ), ( I, J )",
            "      DATA I / 1 /",
            "      J = 2",
            "      DATA K / 2 * 0 /, D / 1.D0 /",
            "      END",
        ];

        // Act
        var table = Build(lines);

        // Assert
        table.Equivalences.Count.ShouldBe(2);
        table.Equivalences[0].Select(SyntaxRenderer.Render).ShouldBe(["D", "K"]);
        table.Data.Count.ShouldBe(3);
    }

    [Fact]
    public void Build_WhenAssignmentBeforeAnyExecutableTargetsANonArray_ShouldReadItAsStatementFunction()
    {
        // Arrange
        string[] lines =
        [
            "      SUBROUTINE S ( A )",
            "      DOUBLE PRECISION A ( 3 ), F, X, DF",
            "      F ( X ) = 1.D0 + X",
            "      DOUBLE PRECISION LATE",
            "      DF ( X, LATE ) = X * LATE",
            "      A ( 1 ) = F ( 2.D0 )",
            "      A ( 2 ) = 0.D0",
            "      END",
        ];

        // Act
        var table = Build(lines);

        // Assert
        table.StatementFunctions.Select(function => function.Source.Line).ShouldBe([3, 5]);
        table.Find("F").ShouldNotBeNull().IsStatementFunction.ShouldBeTrue();
        table.Find("DF").ShouldNotBeNull().IsStatementFunction.ShouldBeTrue();
        table.Find("A").ShouldNotBeNull().IsStatementFunction.ShouldBeFalse();
        table.Find("LATE").ShouldNotBeNull().DeclaredType.ShouldNotBeNull();
    }

    [Fact]
    public void Build_WhenArrayElementIsAssignedFirst_ShouldEndTheDeclarations()
    {
        // Arrange
        string[] lines =
        [
            "      SUBROUTINE S",
            "      INTEGER A ( 3 ), I",
            "      A ( I ) = 0",
            "      INTEGER J",
            "      END",
        ];

        // Act & Assert
        var exception = Should.Throw<FortranAnalysisException>(() => Build(lines));
        exception.Line.ShouldBe(4);
    }

    [Fact]
    public void Build_WhenUnitIncludesFiles_ShouldDrawTheirDeclarationsInAndSayWhereEachCameFrom()
    {
        // Arrange
        var includes = new Dictionary<string, string[]>
        {
            ["sizes.inc"] =
            [
                "      INTEGER MAXLEN",
                "      PARAMETER ( MAXLEN = 80 )",
                "      INCLUDE 'inner.inc'",
            ],
            ["inner.inc"] =
            [
                "      INTEGER DEPTH",
                "      PARAMETER ( DEPTH = 2 )",
            ],
        };
        string[] lines =
        [
            "      SUBROUTINE S ( LINE )",
            "      INCLUDE 'sizes.inc'",
            "      CHARACTER*(MAXLEN) LINE",
            "      END",
        ];

        // Act
        var table = Build(lines, includes);

        // Assert
        table.Symbols.Select(symbol => $"{symbol.Name}:{symbol.IncludeFile}").ShouldBe(
            ["S:", "LINE:", "MAXLEN:sizes.inc", "DEPTH:inner.inc"]);
        table.Find("MAXLEN").ShouldNotBeNull().IsConstant.ShouldBeTrue();
        RenderLength(table.Find("LINE")).ShouldBe("MAXLEN");
    }

    [Fact]
    public void Build_WhenIncludeFileIsMissing_ShouldThrowNamingTheFile()
    {
        // Act & Assert
        var exception = Should.Throw<FortranAnalysisException>(
            () => Build("      SUBROUTINE S", "      INCLUDE 'gone.inc'", "      END"));
        exception.Message.ShouldBe("Line 2: The include file 'gone.inc' was not found.");
    }

    [Fact]
    public void Build_WhenIncludeFileIncludesItself_ShouldThrow()
    {
        // Arrange
        var includes = new Dictionary<string, string[]> { ["loop.inc"] = ["      INCLUDE 'loop.inc'"] };

        // Act & Assert
        var exception = Should.Throw<FortranAnalysisException>(
            () => Build(["      SUBROUTINE S", "      INCLUDE 'loop.inc'", "      END"], includes));
        exception.IncludeFile.ShouldBe("loop.inc");
    }

    [Theory]
    [InlineData("      X = 1")]
    [InlineData("      CALL T")]
    [InlineData("      IF ( A ) THEN", "      END IF")]
    [InlineData("      DO I = 1, 3", "      END DO")]
    [InlineData("      ENTRY E")]
    public void Build_WhenIncludeFileHoldsMoreThanDeclarations_ShouldThrowWithItsNameAndLine(params string[] include)
    {
        // Arrange
        var includes = new Dictionary<string, string[]> { ["code.inc"] = ["      INTEGER N", .. include] };

        // Act & Assert
        var exception = Should.Throw<FortranAnalysisException>(
            () => Build(["      SUBROUTINE S", "      INCLUDE 'code.inc'", "      END"], includes));
        exception.Line.ShouldBe(2);
        exception.IncludeFile.ShouldBe("code.inc");
        exception.Message.ShouldStartWith("Line 2 of include file 'code.inc': ");
    }

    [Theory]
    [InlineData(3, "      INTEGER I", "      REAL I")]
    [InlineData(2, "      INTEGER I, I")]
    [InlineData(3, "      INTEGER A ( 3 )", "      DIMENSION A ( 4 )")]
    [InlineData(3, "      PARAMETER ( N = 1 )", "      PARAMETER ( N = 2 )")]
    [InlineData(3, "      INTEGER A ( 3 )", "      PARAMETER ( A = 1 )")]
    [InlineData(3, "      PARAMETER ( N = 1 )", "      DIMENSION N ( 2 )")]
    [InlineData(2, "      PARAMETER ( ARG = 1 )")]
    [InlineData(3, "      PARAMETER ( N = 1 )", "      SAVE N")]
    [InlineData(3, "      PARAMETER ( N = 1 )", "      EXTERNAL N")]
    [InlineData(3, "      EXTERNAL F", "      INTRINSIC F")]
    [InlineData(3, "      INTEGER A ( 3 )", "      EXTERNAL A")]
    [InlineData(2, "      SAVE ARG")]
    [InlineData(2, "      SAVE S")]
    [InlineData(2, "      EXTERNAL S")]
    [InlineData(2, "      DIMENSION S ( 3 )")]
    [InlineData(2, "      INTRINSIC ARG")]
    [InlineData(2, "      ENTRY S")]
    [InlineData(2, "      ENTRY E ( P, P )")]
    [InlineData(2, "      ENTRY ARG")]
    [InlineData(3, "      F ( X ) = X", "      F ( Y ) = Y")]
    [InlineData(2, "      F ( X, X ) = X")]
    [InlineData(3, "      F ( X ) = X", "      DIMENSION F ( 3 )")]
    [InlineData(3, "      F ( X ) = X", "      EXTERNAL F")]
    [InlineData(2, "      ARG ( X ) = X")]
    [InlineData(3, "      X = 1", "      INTEGER I")]
    [InlineData(3, "      CALL T", "      IMPLICIT NONE")]
    [InlineData(3, "      IF ( B ) THEN", "      INTEGER I", "      END IF")]
    [InlineData(3, "      DO I = 1, 3", "      SAVE", "      END DO")]
    [InlineData(4, "      DO I = 1, 3", "      END DO", "      INCLUDE 'x.inc'")]
    public void Build_WhenDeclarationsContradictOrAreMisplaced_ShouldThrowWithTheOffendingLine(int line, params string[] body)
    {
        // Act & Assert
        var exception = Should.Throw<FortranAnalysisException>(() => Build(["      SUBROUTINE S ( ARG )", .. body, "      END"]));
        exception.Line.ShouldBe(line);
        exception.IncludeFile.ShouldBeNull();
    }

    [Fact]
    public void Build_WhenArgumentIsListedTwice_ShouldThrow()
    {
        // Act & Assert
        var exception = Should.Throw<FortranAnalysisException>(() => Build("      SUBROUTINE S ( A, A )", "      END"));
        exception.Message.ShouldBe("Line 1: A appears twice in the argument list of S.");
    }

    [Fact]
    public void Build_WhenDataFollowsExecutableStatements_ShouldAcceptIt()
    {
        // Act
        var table = Build("      SUBROUTINE S", "      INTEGER I", "      I = 1", "      IF ( I .GT. 0 ) THEN", "      DATA I / 0 /", "      END IF", "      END");

        // Assert
        table.Data.ShouldHaveSingleItem();
    }

    private static string RenderLength(Symbol? symbol) =>
        SyntaxRenderer.Render(symbol.ShouldNotBeNull().DeclaredType.ShouldNotBeNull().Length.ShouldNotBeNull().Value);

    private static SymbolTable Build(params string[] lines) => Build(lines, []);

    private static SymbolTable Build(string[] lines, Dictionary<string, string[]> includes)
    {
        var unit = SourceParser.Parse(Read(lines)).Units.ShouldHaveSingleItem();
        return SymbolTableBuilder.Build(
            unit,
            name => includes.TryGetValue(name, out var include) ? SourceParser.ParseInclude(Read(include)) : null);
    }

    private static FixedFormSource Read(string[] lines)
    {
        using var reader = new StringReader(string.Join('\n', lines));
        return FixedFormReader.Read(reader);
    }
}
