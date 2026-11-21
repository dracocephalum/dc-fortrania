using Dracocephalum.Fortrania.Analysis;
using Dracocephalum.Fortrania.Syntax;
using Shouldly;

namespace Dracocephalum.Fortrania.Tests.Analysis;

public class EntryLayoutTests
{
    [Theory]
    [InlineData("      PROGRAM MAIN")]
    [InlineData("      X = 1")]
    [InlineData("      BLOCK DATA")]
    public void Of_WhenUnitIsNotAProcedure_ShouldHaveNoBodies(string first)
    {
        // Act
        var layout = Layout(first, "      END");

        // Assert
        layout.Bodies.ShouldBeEmpty();
        layout.IsDisjoint.ShouldBeTrue();
    }

    [Fact]
    public void Of_WhenProcedureHasNoEntry_ShouldGiveItsOwnStatementsUpToTheFirstReturn()
    {
        // Act
        var layout = Layout(
            "      SUBROUTINE SUB ( X )",
            "      DOUBLE PRECISION X",
            "      IF ( X .LT. 0 ) THEN",
            "         RETURN",
            "      END IF",
            "      X = 1",
            "      RETURN",
            "      X = 2",
            "      END");

        // Assert
        var body = layout.Bodies.ShouldHaveSingleItem();
        body.Name.ShouldBe("SUB");
        body.Parameters.ShouldBe(["X"]);
        Lines(body).ShouldBe([2, 3, 6, 7]);
        body.StrayArguments.ShouldBeEmpty();
        layout.IsDisjoint.ShouldBeTrue();
    }

    [Fact]
    public void Of_WhenEveryEntryFollowsAReturn_ShouldBeDisjoint()
    {
        // Act
        var layout = Layout(
            "      INTEGER FUNCTION STORE ( N )",
            "      INTEGER N, SAVED, GET",
            "      SAVE SAVED",
            "      STORE = 0",
            "      RETURN",
            "      ENTRY PUT ( N )",
            "      SAVED = N",
            "      PUT = 0",
            "      RETURN",
            "      ENTRY GET ( )",
            "      GET = SAVED",
            "      END");

        // Assert
        layout.IsDisjoint.ShouldBeTrue();
        layout.Bodies.Select(body => $"{body.Name}({string.Join(",", body.Parameters)}): {string.Join(" ", Lines(body))}")
            .ShouldBe(["STORE(N): 2 3 4 5", "PUT(N): 7 8 9", "GET(): 11"]);
        layout.Bodies[1].Entry.Source.Line.ShouldBe(6);
    }

    [Fact]
    public void Of_WhenControlRunsOnIntoAnEntry_ShouldNotBeDisjointAndShouldGiveTheStatementsToBoth()
    {
        // Act
        var layout = Layout(
            "      SUBROUTINE FIRST ( X, Y )",
            "      DOUBLE PRECISION X, Y",
            "      X = 0",
            "      ENTRY SECOND ( Y )",
            "      Y = 1",
            "      RETURN",
            "      ENTRY THIRD ( X )",
            "      X = 2",
            "      END");

        // Assert
        layout.IsDisjoint.ShouldBeFalse();
        layout.Bodies.Select(body => $"{body.Name}: {string.Join(" ", Lines(body))}")
            .ShouldBe(["FIRST: 2 3 5 6", "SECOND: 5 6", "THIRD: 8"]);
    }

    [Fact]
    public void Of_WhenAReturnStandsInABlock_ShouldNotEndTheBodyThere()
    {
        // Act
        var layout = Layout(
            "      SUBROUTINE FIRST ( X )",
            "      DOUBLE PRECISION X",
            "      IF ( X .GT. 0 ) RETURN",
            "      DO WHILE ( X .LT. 0 )",
            "         RETURN",
            "      END DO",
            "      ENTRY SECOND ( X )",
            "      X = 1",
            "      END");

        // Assert
        layout.IsDisjoint.ShouldBeFalse();
        Lines(layout.Bodies[0]).ShouldBe([2, 3, 4, 8]);
    }

    [Fact]
    public void Of_WhenStopEndsTheStatementsBeforeAnEntry_ShouldBeDisjoint()
    {
        // Act
        var layout = Layout(
            "      SUBROUTINE FIRST",
            "      STOP",
            "      ENTRY SECOND",
            "      END");

        // Assert
        layout.IsDisjoint.ShouldBeTrue();
        layout.Bodies[1].Statements.ShouldBeEmpty();
    }

    [Fact]
    public void Of_WhenStatementsUseAnotherEntrysArgument_ShouldListItAsStray()
    {
        // Act
        var layout = Layout(
            "      SUBROUTINE FIRST ( A, B, PROC, N )",
            "      INTEGER N",
            "      DOUBLE PRECISION A, B, C, T ( N ), SF",
            "      EXTERNAL PROC",
            "      SF ( C ) = C + B",
            "      A = SF ( B )",
            "      RETURN",
            "      ENTRY SECOND ( C, N )",
            "      IF ( C .GT. 0 ) THEN",
            "         CALL PROC ( A, T ( N ) )",
            "         C = A + B + A",
            "      END IF",
            "      RETURN",
            "      ENTRY THIRD ( PROC )",
            "      CALL OTHER ( PROC )",
            "      END");

        // Assert
        layout.Bodies[0].StrayArguments.ShouldBeEmpty();
        layout.Bodies[1].StrayArguments.ShouldBe(["PROC", "A", "B"]);
        layout.Bodies[2].StrayArguments.ShouldBeEmpty();
    }

    [Fact]
    public void Of_WhenOneEntryRunsOnIntoCodeUsingTheNextOnesArgument_ShouldListItAsStrayForTheFirstOnly()
    {
        // Act
        var layout = Layout(
            "      SUBROUTINE FIRST ( X )",
            "      DOUBLE PRECISION X, Y",
            "      X = 0",
            "      ENTRY SECOND ( Y )",
            "      Y = 1",
            "      END");

        // Assert
        layout.Bodies[0].StrayArguments.ShouldBe(["Y"]);
        layout.Bodies[1].StrayArguments.ShouldBeEmpty();
    }

    private static IEnumerable<int> Lines(EntryBody body) => body.Statements.Select(construct => construct switch
    {
        Construct.Simple simple => simple.Statement.Source.Line,
        Construct.If block => block.Branches[0].Header.Source.Line,
        Construct.Loop loop => loop.Header.Source.Line,
        _ => 0,
    });

    private static EntryLayout Layout(params string[] lines)
    {
        using var reader = new StringReader(string.Join('\n', lines));
        var unit = SourceParser.Parse(FixedFormReader.Read(reader)).Units.ShouldHaveSingleItem();
        return EntryLayout.Of(SymbolTableBuilder.Build(unit, _ => null));
    }
}
