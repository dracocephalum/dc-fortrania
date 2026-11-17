using Dracocephalum.Fortrania.Syntax;
using Shouldly;
using static Dracocephalum.Fortrania.Tests.Syntax.SyntaxRenderer;

namespace Dracocephalum.Fortrania.Tests.Syntax;

public class ProgramUnitHeaderParserTests
{
    [Theory]
    [InlineData("PROGRAM TSPICE", "PROGRAM TSPICE")]
    [InlineData("SUBROUTINE VADD ( V1, V2, VOUT )", "SUBROUTINE VADD(V1, V2, VOUT)")]
    [InlineData("SUBROUTINE RESET", "SUBROUTINE RESET()")]
    [InlineData("SUBROUTINE RESET ()", "SUBROUTINE RESET()")]
    [InlineData("FUNCTION F ( X )", "FUNCTION F(X)")]
    [InlineData("INTEGER FUNCTION RTRIM ( STRING )", "Integer FUNCTION RTRIM(STRING)")]
    [InlineData("DOUBLE PRECISION FUNCTION VNORM ( V1 )", "DoublePrecision FUNCTION VNORM(V1)")]
    [InlineData("LOGICAL FUNCTION FAILED ()", "Logical FUNCTION FAILED()")]
    [InlineData("CHARACTER*(*) FUNCTION UCASE ( IN )", "Character*(*) FUNCTION UCASE(IN)")]
    [InlineData("CHARACTER*80 FUNCTION LINE ( N )", "Character*80 FUNCTION LINE(N)")]
    [InlineData("REAL*8 FUNCTION F ( X, Y )", "Real*8 FUNCTION F(X, Y)")]
    [InlineData("ENTRY SETERR ( STATUS )", "ENTRY SETERR(STATUS)")]
    [InlineData("ENTRY RESET", "ENTRY RESET()")]
    [InlineData("BLOCK DATA", "BLOCK DATA")]
    [InlineData("BLOCK DATA INIT", "BLOCK DATA INIT")]
    public void Parse_WhenStatementOpensProgramUnitOrEntry_ShouldReturnNameTypeAndParameters(string text, string expected)
    {
        // Act
        var statement = Parse(text);

        // Assert
        Render(statement).ShouldBe(expected);
    }

    [Theory]
    [InlineData("PROGRAM")]
    [InlineData("PROGRAM A ( X )")]
    [InlineData("SUBROUTINE")]
    [InlineData("SUBROUTINE S ( A,")]
    [InlineData("SUBROUTINE S ( A, * )")]
    [InlineData("SUBROUTINE S ( A ) B")]
    [InlineData("SUBROUTINE S ( 1 )")]
    [InlineData("FUNCTION ( X )")]
    [InlineData("ENTRY E ( A + B )")]
    public void Parse_WhenHeaderIsMalformedOrUnsupported_ShouldThrowWithTheStatementLine(string text)
    {
        // Act & Assert
        var exception = Should.Throw<FortranSyntaxException>(() => Parse(text));
        exception.Line.ShouldBe(7);
    }

    private static Statement Parse(string text)
    {
        var statement = Lexer.Tokenize(new FixedFormStatement(null, text, 7, [], []));
        return StatementParser.Parse(statement, StatementClassifier.Classify(statement, startsProgramUnit: true));
    }
}
