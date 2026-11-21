using Dracocephalum.Fortrania.Analysis;
using Dracocephalum.Fortrania.Syntax;
using Shouldly;

namespace Dracocephalum.Fortrania.Tests.Analysis;

public class UnitSummarizerUsesTests
{
    private const ArgumentUses None = ArgumentUses.None;
    private const ArgumentUses Read = ArgumentUses.Read;
    private const ArgumentUses Written = ArgumentUses.Written;
    private const ArgumentUses Both = ArgumentUses.Read | ArgumentUses.Written;

    // X and Y double precision, I and J integer, A a double precision array,
    // C and T character, L logical; all but J, Y and T are dummy arguments.
    // F is a dummy function and S a dummy subroutine; SF is a statement
    // function whose own argument is named X.
    private static readonly string[] Declarations =
    [
        "      SUBROUTINE SUB ( X, I, A, C, L, F, S )",
        "      IMPLICIT NONE",
        "      INTEGER I, J",
        "      DOUBLE PRECISION X, Y, A ( * ), F, DFUN, SF",
        "      CHARACTER*(*) C",
        "      CHARACTER*(8) T",
        "      LOGICAL L",
        "      EXTERNAL F, S",
        "      SF ( X ) = X * 2",
    ];

    [Theory]
    [InlineData("Y = 1", None)]
    [InlineData("Y = X", Read)]
    [InlineData("Y = -( X + 1 )", Read)]
    [InlineData("X = 1", Written)]
    [InlineData("X = X + 1", Both)]
    [InlineData("IF ( X .GT. 0 ) Y = 1", Read)]
    [InlineData("IF ( L ) X = 1", Written)]
    [InlineData("Y = SQRT ( X )", Read)]
    [InlineData("Y = SF ( X )", Read)]
    [InlineData("Y = SF ( Y )", None)]
    [InlineData("Y = A ( INT ( X ) )", Read)]
    [InlineData("A ( INT ( X ) ) = 0", Read)]
    [InlineData("T ( INT ( X ): ) = ' '", Read)]
    [InlineData("WRITE ( *, * ) X", Read)]
    [InlineData("PRINT *, X, Y", Read)]
    [InlineData("READ ( *, * ) X", Written)]
    [InlineData("READ ( *, * ) ( X, J = 1, 3 )", Written)]
    [InlineData("WRITE ( *, * ) ( X, J = 1, 3 )", Read)]
    [InlineData("Y = F ( X )", Both)]
    [InlineData("CALL S ( X )", Both)]
    [InlineData("CALL S ( X + 1 )", Read)]
    [InlineData("CALL S ( ( X ) )", Read)]
    [InlineData("CALL OTHER ( X )", None)]
    [InlineData("CALL OTHER ( X * 2 )", Read)]
    [InlineData("Y = DFUN ( X )", None)]
    [InlineData("Y = DFUN ( -X )", Read)]
    public void Summarize_WhenStatementUsesAScalarArgument_ShouldSayHow(string statement, ArgumentUses expected) =>
        UsesOf("X", statement).ShouldBe(expected);

    [Theory]
    [InlineData("DO I = 1, 3", Written)]
    [InlineData("DO J = I, 3", Read)]
    [InlineData("DO J = 1, I", Read)]
    [InlineData("DO J = 1, 3, I", Read)]
    [InlineData("DO WHILE ( I .GT. 0 )", Read)]
    public void Summarize_WhenLoopUsesAnArgument_ShouldSayHow(string header, ArgumentUses expected) =>
        UsesOf("I", header, "END DO").ShouldBe(expected);

    [Theory]
    [InlineData("Y = A ( 1 )", Read)]
    [InlineData("A ( 1 ) = 0", Written)]
    [InlineData("A ( 2 ) = A ( 1 )", Both)]
    [InlineData("READ ( *, * ) ( A ( J ), J = 1, 3 )", Written)]
    [InlineData("WRITE ( *, * ) A ( 1 )", Read)]
    [InlineData("CALL S ( A )", Both)]
    [InlineData("CALL S ( A ( 2 ) )", Both)]
    [InlineData("CALL OTHER ( A ( 2 ) )", None)]
    [InlineData("CALL OTHER ( A ( 2 ) + 1 )", Read)]
    public void Summarize_WhenStatementUsesAnArrayArgument_ShouldSayHow(string statement, ArgumentUses expected) =>
        UsesOf("A", statement).ShouldBe(expected);

    [Theory]
    [InlineData("T = C", Read)]
    [InlineData("T = C ( 2:3 )", Read)]
    [InlineData("C = T", Written)]
    [InlineData("C ( 2:3 ) = T", Written)]
    [InlineData("C = C // 'X'", Both)]
    [InlineData("WRITE ( C, * ) Y", Written)]
    [InlineData("WRITE ( UNIT = C, FMT = * ) Y", Written)]
    [InlineData("READ ( C, * ) Y", Read)]
    [InlineData("WRITE ( *, C ) Y", Read)]
    [InlineData("WRITE ( *, FMT = C ) Y", Read)]
    [InlineData("OPEN ( UNIT = 7, FILE = C )", Read)]
    [InlineData("OPEN ( UNIT = 7, STATUS = C )", Read)]
    [InlineData("INQUIRE ( FILE = C, EXIST = L )", Read)]
    [InlineData("INQUIRE ( UNIT = 7, NAME = C )", Written)]
    [InlineData("INQUIRE ( 7, ACCESS = C )", Written)]
    [InlineData("CALL S ( C ( 2: ) )", Both)]
    public void Summarize_WhenStatementUsesACharacterArgument_ShouldSayHow(string statement, ArgumentUses expected) =>
        UsesOf("C", statement).ShouldBe(expected);

    [Theory]
    [InlineData("READ ( 7, *, IOSTAT = I ) Y", Written)]
    [InlineData("WRITE ( 7, *, IOSTAT = I ) Y", Written)]
    [InlineData("OPEN ( UNIT = 7, IOSTAT = I )", Written)]
    [InlineData("CLOSE ( UNIT = 7, IOSTAT = I )", Written)]
    [InlineData("REWIND ( UNIT = 7, IOSTAT = I )", Written)]
    [InlineData("BACKSPACE ( UNIT = 7, IOSTAT = I )", Written)]
    [InlineData("ENDFILE ( UNIT = 7, IOSTAT = I )", Written)]
    [InlineData("INQUIRE ( UNIT = 7, IOSTAT = I )", Written)]
    [InlineData("INQUIRE ( UNIT = 7, NUMBER = I )", Written)]
    [InlineData("INQUIRE ( UNIT = I, EXIST = L )", Read)]
    [InlineData("READ ( I, * ) Y", Read)]
    [InlineData("WRITE ( I, * ) Y", Read)]
    [InlineData("WRITE ( UNIT = I, FMT = * ) Y", Read)]
    [InlineData("READ ( 7, REC = I ) Y", Read)]
    [InlineData("CLOSE ( I )", Read)]
    [InlineData("REWIND I", Read)]
    public void Summarize_WhenControlListNamesAnArgument_ShouldSayWhetherItIsStoredInto(string statement, ArgumentUses expected) =>
        UsesOf("I", statement).ShouldBe(expected);

    [Fact]
    public void Summarize_WhenArgumentGivesABoundOrALength_ShouldCountItAsRead()
    {
        // Act
        var entry = Summarize(
            "      SUBROUTINE SUB ( N, M, A, C )",
            "      INTEGER N, M",
            "      DOUBLE PRECISION A ( 2:N )",
            "      CHARACTER*(M) C",
            "      END").EntryPoints.ShouldHaveSingleItem();

        // Assert
        entry.Arguments.Select(argument => argument.Uses).ShouldBe([Read, Read, None, None]);
    }

    [Fact]
    public void Summarize_WhenArgumentIsPassedOn_ShouldListEachPlaceOnce()
    {
        // Act
        var entry = Summarize(
            "      SUBROUTINE SUB ( X, A, C )",
            "      DOUBLE PRECISION X, A ( * ), DFUN, Y",
            "      CHARACTER*(*) C",
            "      CALL FIRST ( X, A, 3, C )",
            "      CALL FIRST ( X, A, 4, C )",
            "      CALL SECOND ( C ( 2:3 ), A ( 2 ), X + 1 )",
            "      Y = DFUN ( 1, DFUN ( 2, X ) )",
            "      END").EntryPoints.ShouldHaveSingleItem();

        // Assert
        entry.Arguments.Select(argument => argument.Uses).ShouldBe([Read, None, None]);
        entry.Arguments[0].PassedTo.ShouldBe([new ArgumentPass("FIRST", 0), new ArgumentPass("DFUN", 1)]);
        entry.Arguments[1].PassedTo.ShouldBe([new ArgumentPass("FIRST", 1), new ArgumentPass("SECOND", 1)]);
        entry.Arguments[2].PassedTo.ShouldBe([new ArgumentPass("FIRST", 3), new ArgumentPass("SECOND", 0)]);
    }

    [Fact]
    public void Summarize_WhenUnitHasEntries_ShouldCountForEachOnlyWhatItReaches()
    {
        // Act
        var summary = Summarize(
            "      SUBROUTINE STORE ( X, N )",
            "      DOUBLE PRECISION X, SAVED",
            "      INTEGER N",
            "      SAVE SAVED",
            "      CALL COMPLAIN ( N )",
            "      RETURN",
            "      ENTRY PUT ( X, N )",
            "      SAVED = X",
            "      IF ( X .LT. 0 ) THEN",
            "         RETURN",
            "      END IF",
            "      ENTRY COUNT ( N )",
            "      N = 1",
            "      RETURN",
            "      ENTRY GET ( X )",
            "      X = SAVED",
            "      END");

        // Assert
        summary.EntryPoints.Select(Render).ShouldBe(
        [
            "STORE: X None; N None > COMPLAIN#0",
            "PUT: X Read; N Written",
            "COUNT: N Written",
            "GET: X Written",
        ]);
    }

    [Fact]
    public void Summarize_WhenOneEntryRunsOnIntoTheNext_ShouldCountTheLaterStatementsForBoth()
    {
        // Act
        var summary = Summarize(
            "      SUBROUTINE FIRST ( X, Y )",
            "      DOUBLE PRECISION X, Y",
            "      X = 0",
            "      ENTRY SECOND ( Y )",
            "      Y = 1",
            "      END");

        // Assert
        summary.EntryPoints.Select(Render).ShouldBe(["FIRST: X Written; Y Written", "SECOND: Y Written"]);
    }

    private static string Render(ProcedureInterface entry) =>
        entry.Name + ": " + string.Join("; ", entry.Arguments.Select(argument =>
            $"{argument.Name} {argument.Uses}"
            + string.Concat(argument.PassedTo.Select(pass => $" > {pass.Procedure}#{pass.Index}"))));

    private static ArgumentUses UsesOf(string argument, params string[] statements)
    {
        var entry = Summarize([.. Declarations, .. statements.Select(statement => "      " + statement), "      END"])
            .EntryPoints.ShouldHaveSingleItem();
        return entry.Arguments.Single(dummy => dummy.Name == argument).Uses;
    }

    private static UnitSummary Summarize(params string[] lines)
    {
        using var reader = new StringReader(string.Join('\n', lines));
        var unit = SourceParser.Parse(FixedFormReader.Read(reader)).Units.ShouldHaveSingleItem();
        return UnitSummarizer.Summarize(SymbolTableBuilder.Build(unit, _ => null));
    }
}
