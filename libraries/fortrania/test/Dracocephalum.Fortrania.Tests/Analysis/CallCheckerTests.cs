using Dracocephalum.Fortrania.Analysis;
using Dracocephalum.Fortrania.Syntax;
using Shouldly;

namespace Dracocephalum.Fortrania.Tests.Analysis;

public class CallCheckerTests
{
    // What the calls below reach: a subroutine with an entry, and two functions.
    private static readonly string[] Definitions =
    [
        "      SUBROUTINE TAKE ( N, X, TABLE, NAME, F, S )",
        "      INTEGER N",
        "      DOUBLE PRECISION X, TABLE ( N ), F",
        "      CHARACTER*(*) NAME",
        "      EXTERNAL F, S",
        "      X = F ( N )",
        "      CALL S",
        "      RETURN",
        "      ENTRY TAKE1 ( N )",
        "      END",
        "      DOUBLE PRECISION FUNCTION DFUN ( X )",
        "      DOUBLE PRECISION X",
        "      DFUN = X",
        "      END",
        "      LOGICAL FUNCTION STATE ( N )",
        "      INTEGER N",
        "      STATE = N .GT. 0",
        "      END",
    ];

    [Theory]
    [InlineData("CALL TAKE ( I, D, DA, C, DFUN, SUB )")]
    [InlineData("CALL TAKE ( 3, D, DA ( 2 ), 'ABC', DFUN, SUB )")]
    [InlineData("CALL TAKE1 ( I )")]
    [InlineData("D = DFUN ( D ) + DFUN ( 2.0D0 )")]
    public void Check_WhenCallsFitTheirProcedures_ShouldPass(string statement) =>
        Check(statement).ShouldBeEmpty();

    [Theory]
    [InlineData("CALL TAKE ( I, D, D, C, DFUN, SUB )")]
    [InlineData("CALL TAKE ( I, D, 1.0D0, C, DFUN, SUB )")]
    [InlineData("CALL TAKE ( I, DA, DA, C, DFUN, SUB )")]
    [InlineData("CALL TAKE ( D, I, DA, C, DFUN, SUB )")]
    [InlineData("CALL TAKE ( L, D, IA, C, DFUN, SUB )")]
    [InlineData("CALL TAKE ( I, 1.0, DA, C, DFUN, SUB )")]
    [InlineData("CALL TAKE ( I, D, DA, C, UNTYPD, UNTYPD )")]
    [InlineData("CALL STATE ( I )")]
    public void Check_WhenCallOnlyBendsTheRules_ShouldPass(string statement) =>
        Check(statement).ShouldBeEmpty();

    [Theory]
    [InlineData("CALL TAKE1 ( )", "Line 7: TAKE1 takes 1 arguments and is given 0.")]
    [InlineData("CALL TAKE1 ( I, I )", "Line 7: TAKE1 takes 1 arguments and is given 2.")]
    [InlineData("D = DFUN ( )", "Line 7: DFUN takes 1 arguments and is given 0.")]
    [InlineData("CALL TAKE1 ( SUB )", "Line 7: Argument 1 of TAKE1 is a procedure, and N there is not.")]
    [InlineData("CALL TAKE ( I, D, DA, C, D, SUB )", "Line 7: Argument 5 of TAKE is not a procedure, and F there is.")]
    [InlineData("CALL TAKE1 ( C )", "Line 7: Argument 1 of TAKE1 is of type Character, and N there is of type Integer.")]
    [InlineData("CALL TAKE ( I, D, DA, I, DFUN, SUB )", "Line 7: Argument 4 of TAKE is of type Integer, and NAME there is of type Character.")]
    [InlineData("I = TAKE1 ( I )", "Line 7: TAKE1 is referenced as a function, and it is a subroutine.")]
    [InlineData("I = STATE ( I )", "Line 7: STATE is referenced as a function of type Real, and it is of type Logical.")]
    public void Check_WhenCallCannotBeHonoured_ShouldThrow(string statement, string message) =>
        Should.Throw<FortranAnalysisException>(() => Check(statement)).Message.ShouldBe(message);

    [Fact]
    public void Check_WhenFunctionIsGivenItsOwnType_ShouldPass() =>
        Check("LOGICAL STATE", "L = STATE ( I )").ShouldBeEmpty();

    [Fact]
    public void Check_WhenProceduresAreNotDefined_ShouldReturnEachOnceInOrder() =>
        Check("CALL ZEBRA ( APPLE ( I ), APPLE ( 2 ) )", "CALL ZEBRA ( MANGO ( D ) )", "CALL TAKE1 ( I )")
            .ShouldBe(["ZEBRA", "APPLE", "MANGO"]);

    // The statements start on line 7 of a caller that declares every name
    // they use.
    private static IReadOnlyList<string> Check(params string[] statements)
    {
        var catalogue = new ProcedureCatalogue();
        foreach (var unit in Summarize(Definitions))
        {
            catalogue.Add(unit);
        }

        string[] caller =
        [
            "      SUBROUTINE CALLER",
            "      INTEGER I, IA ( 3 )",
            "      DOUBLE PRECISION D, DA ( 3 ), DFUN",
            "      LOGICAL L",
            "      CHARACTER*(8) C",
            "      EXTERNAL DFUN, SUB, UNTYPD",
            .. statements.Select(statement => "      " + statement),
            "      END",
        ];

        return CallChecker.Check(Summarize(caller).ShouldHaveSingleItem(), catalogue.Find);
    }

    private static List<UnitSummary> Summarize(string[] lines)
    {
        using var reader = new StringReader(string.Join('\n', lines));
        return SourceParser.Parse(FixedFormReader.Read(reader)).Units
            .Select(unit => UnitSummarizer.Summarize(SymbolTableBuilder.Build(unit, _ => null)))
            .ToList();
    }
}
