using Dracocephalum.Fortrania.Analysis;
using Dracocephalum.Fortrania.Syntax;
using Shouldly;

namespace Dracocephalum.Fortrania.Tests.Analysis;

public class UnitSummarizerTests
{
    [Theory]
    [InlineData("      PROGRAM MAIN", UnitKind.MainProgram, "MAIN")]
    [InlineData("      X = 1", UnitKind.MainProgram, null)]
    [InlineData("      SUBROUTINE SUB", UnitKind.Subroutine, "SUB")]
    [InlineData("      INTEGER FUNCTION FUN ( )", UnitKind.Function, "FUN")]
    [InlineData("      BLOCK DATA TABLES", UnitKind.BlockData, "TABLES")]
    [InlineData("      BLOCK DATA", UnitKind.BlockData, null)]
    public void Summarize_WhenUnitIsOfAKind_ShouldSayWhichAndNameIt(string first, UnitKind kind, string? name)
    {
        // Act
        var summary = Summarize(first, "      END");

        // Assert
        summary.Kind.ShouldBe(kind);
        summary.Name.ShouldBe(name);
    }

    [Fact]
    public void Summarize_WhenUnitIsAMainProgram_ShouldHaveNoEntryPoints() =>
        Summarize("      PROGRAM MAIN", "      END").EntryPoints.ShouldBeEmpty();

    [Fact]
    public void Summarize_WhenUnitIsASubroutine_ShouldDescribeEachDummyArgument()
    {
        // Act
        var summary = Summarize(
            "      SUBROUTINE SUB ( N, X, TABLE, NAME, F, S, P )",
            "      INTEGER N",
            "      DOUBLE PRECISION X, TABLE ( 3, N ), F",
            "      CHARACTER*(*) NAME ( * )",
            "      EXTERNAL F, S, P",
            "      X = F ( N )",
            "      CALL S ( P )",
            "      END");

        // Assert
        var entry = summary.EntryPoints.ShouldHaveSingleItem();
        entry.Name.ShouldBe("SUB");
        entry.IsFunction.ShouldBeFalse();
        entry.ResultType.ShouldBeNull();
        entry.Line.ShouldBe(1);
        entry.Arguments.ShouldBe(
        [
            new DummyArgument("N", DummyArgumentKind.Variable, DataType.Integer, 0),
            new DummyArgument("X", DummyArgumentKind.Variable, DataType.DoublePrecision, 0),
            new DummyArgument("TABLE", DummyArgumentKind.Array, DataType.DoublePrecision, 2),
            new DummyArgument("NAME", DummyArgumentKind.Array, DataType.Character, 1),
            new DummyArgument("F", DummyArgumentKind.Function, DataType.DoublePrecision, 0),
            new DummyArgument("S", DummyArgumentKind.Subroutine, null, 0),
            new DummyArgument("P", DummyArgumentKind.Procedure, null, 0),
        ]);
    }

    [Fact]
    public void Summarize_WhenFunctionHasEntries_ShouldGiveEachItsOwnResultTypeAndArguments()
    {
        // Act
        var summary = Summarize(
            "      LOGICAL FUNCTION FIRST ( A )",
            "      INTEGER A, B, SECOND",
            "      FIRST = A .GT. 0",
            "      RETURN",
            "      ENTRY SECOND ( A, B )",
            "      SECOND = A + B",
            "      END");

        // Assert
        summary.EntryPoints.Count.ShouldBe(2);
        summary.EntryPoints[0].Name.ShouldBe("FIRST");
        summary.EntryPoints[0].ResultType.ShouldBe(DataType.Logical);
        summary.EntryPoints[0].Arguments.Select(argument => argument.Name).ShouldBe(["A"]);
        summary.EntryPoints[1].Name.ShouldBe("SECOND");
        summary.EntryPoints[1].ResultType.ShouldBe(DataType.Integer);
        summary.EntryPoints[1].IsFunction.ShouldBeTrue();
        summary.EntryPoints[1].Arguments.Select(argument => argument.Name).ShouldBe(["A", "B"]);
        summary.EntryPoints[1].Line.ShouldBe(5);
    }

    [Fact]
    public void Summarize_WhenUnitCallsASubroutine_ShouldSayWhatEachArgumentIs()
    {
        // Act
        var summary = Summarize(
            "      SUBROUTINE SUB",
            "      INTEGER I, IA ( 4 ), N",
            "      DOUBLE PRECISION D",
            "      CHARACTER*(8) C, CA ( 2 )",
            "      PARAMETER ( N = 3 )",
            "      EXTERNAL HELPER",
            "      INTRINSIC SIN",
            "      CALL OTHER ( I, IA, IA ( 2 ), N, 7, I + 1, ( D ), C, C ( 2:3 ),",
            "     .             CA ( 1 ), CA ( 2 ) ( :4 ), C // 'X', HELPER, SIN )",
            "      END");

        // Assert
        var call = summary.Calls.ShouldHaveSingleItem();
        call.Name.ShouldBe("OTHER");
        call.IsFunctionReference.ShouldBeFalse();
        call.Line.ShouldBe(8);
        call.Arguments.ShouldBe(
        [
            new ActualArgument(ActualArgumentKind.Variable, DataType.Integer, "I"),
            new ActualArgument(ActualArgumentKind.Array, DataType.Integer, "IA"),
            new ActualArgument(ActualArgumentKind.ArrayElement, DataType.Integer, "IA"),
            new ActualArgument(ActualArgumentKind.Value, DataType.Integer, null),
            new ActualArgument(ActualArgumentKind.Value, DataType.Integer, null),
            new ActualArgument(ActualArgumentKind.Value, DataType.Integer, null),
            new ActualArgument(ActualArgumentKind.Value, DataType.DoublePrecision, null),
            new ActualArgument(ActualArgumentKind.Variable, DataType.Character, "C"),
            new ActualArgument(ActualArgumentKind.Variable, DataType.Character, "C"),
            new ActualArgument(ActualArgumentKind.ArrayElement, DataType.Character, "CA"),
            new ActualArgument(ActualArgumentKind.ArrayElement, DataType.Character, "CA"),
            new ActualArgument(ActualArgumentKind.Value, DataType.Character, null),
            new ActualArgument(ActualArgumentKind.Procedure, null, "HELPER"),
            new ActualArgument(ActualArgumentKind.Procedure, null, "SIN"),
        ]);
    }

    [Fact]
    public void Summarize_WhenUnitPassesATypedFunction_ShouldGiveTheArgumentThatType()
    {
        // Act
        var summary = Summarize(
            "      SUBROUTINE SUB",
            "      DOUBLE PRECISION F",
            "      EXTERNAL F",
            "      CALL OTHER ( F )",
            "      END");

        // Assert
        summary.Calls.ShouldHaveSingleItem().Arguments
            .ShouldBe([new ActualArgument(ActualArgumentKind.Procedure, DataType.DoublePrecision, "F")]);
    }

    [Fact]
    public void Summarize_WhenFunctionsAreReferencedAnywhere_ShouldListEachReferenceInOrder()
    {
        // Act
        var summary = Summarize(
            "      SUBROUTINE SUB ( X )",
            "      DOUBLE PRECISION X, DF, A ( 3 ), SF",
            "      INTEGER IFUN, I",
            "      LOGICAL LF",
            "      CHARACTER*(4) CF",
            "      SF ( X ) = DF ( X ) + 1",
            "      A ( IFUN ( 1 ) ) = SQRT ( DF ( X ) )",
            "      IF ( LF ( ) ) THEN",
            "         CALL OTHER ( IFUN ( IFUN ( 2 ) ) )",
            "      ELSE IF ( LF ( ) ) THEN",
            "         DO I = IFUN ( 3 ), IFUN ( 4 ), IFUN ( 5 )",
            "            WRITE ( IFUN ( 6 ), * ) CF ( X ) ( 1:IFUN ( 7 ) )",
            "         END DO",
            "      END IF",
            "      IF ( X .GT. 0 ) X = SF ( A ( IFUN ( 8 ) ) )",
            "      END");

        // Assert
        summary.Calls.Select(call => $"{call.Line} {call.Name} {call.ResultType?.ToString() ?? "-"}").ShouldBe(
        [
            "6 DF DoublePrecision",
            "7 IFUN Integer",
            "7 DF DoublePrecision",
            "8 LF Logical",
            "9 OTHER -",
            "9 IFUN Integer",
            "9 IFUN Integer",
            "10 LF Logical",
            "11 IFUN Integer",
            "11 IFUN Integer",
            "11 IFUN Integer",
            "12 IFUN Integer",
            "12 CF Character",
            "12 IFUN Integer",
            "15 IFUN Integer",
        ]);
        summary.Calls[3].Arguments.ShouldBeEmpty();
    }

    [Fact]
    public void Summarize_WhenProcedureCalledIsADummyArgument_ShouldNotListTheCall()
    {
        // Act
        var summary = Summarize(
            "      SUBROUTINE SUB ( S, F, X )",
            "      DOUBLE PRECISION F, X",
            "      EXTERNAL S, F",
            "      CALL S ( F ( X ) )",
            "      END");

        // Assert
        summary.Calls.ShouldBeEmpty();
    }

    [Fact]
    public void Summarize_WhenAnArgumentCannotBeTyped_ShouldThrowWithItsLine()
    {
        // Act & Assert
        var exception = Should.Throw<FortranAnalysisException>(() => Summarize(
            "      SUBROUTINE SUB",
            "      LOGICAL L",
            "      CALL OTHER ( L + 1 )",
            "      END"));
        exception.Line.ShouldBe(3);
    }

    private static UnitSummary Summarize(params string[] lines)
    {
        using var reader = new StringReader(string.Join('\n', lines));
        var unit = SourceParser.Parse(FixedFormReader.Read(reader)).Units.ShouldHaveSingleItem();
        return UnitSummarizer.Summarize(SymbolTableBuilder.Build(unit, _ => null));
    }
}
