using Dracocephalum.Fortrania.Syntax;
using Shouldly;
using static Dracocephalum.Fortrania.Tests.Syntax.SyntaxRenderer;

namespace Dracocephalum.Fortrania.Tests.Syntax;

public class ExecutableStatementParserTests
{
    [Theory]
    [InlineData("X = 1", "X = 1")]
    [InlineData("VOUT(1) = V1(1) + V2(1)", "VOUT[1] = (Add V1[1] V2[1])")]
    [InlineData("NAME(1:3) = 'ABC'", "NAME<1:3> = 'ABC'")]
    [InlineData("LINES(I)(:N) = S // T", "LINES[I]<:N> = (Concatenate S T)")]
    [InlineData("FOUND = .NOT. FAILED()", "FOUND = (Not FAILED[])")]
    [InlineData("DO10I = 1.5", "DO10I = 1.5")]
    [InlineData("DO I = 1", "DOI = 1")]
    [InlineData("PARAMETER N = 3", "PARAMETERN = 3")]
    [InlineData("X = 1 2", "X = 12")]
    [InlineData("IF(I) = 1", "IF[I] = 1")]
    public void Parse_WhenStatementIsAssignment_ShouldReturnTargetAndValue(string text, string expected)
    {
        // Act
        var statement = Parse(text);

        // Assert
        Render(statement).ShouldBe(expected);
    }

    [Theory]
    [InlineData("CALL CHKOUT ( 'SPKEZ' )", "CALL CHKOUT['SPKEZ']")]
    [InlineData("CALL VADD ( V1, V2, VOUT )", "CALL VADD[V1, V2, VOUT]")]
    [InlineData("CALL MXV ( XFORM(1,I), STATE, TEMP(I+1) )", "CALL MXV[XFORM[1, I], STATE, TEMP[(Add I 1)]]")]
    [InlineData("CALL RESET", "CALL RESET[]")]
    [InlineData("CALL RESET ()", "CALL RESET[]")]
    public void Parse_WhenStatementIsCall_ShouldReturnNameAndArguments(string text, string expected)
    {
        // Act
        var statement = Parse(text);

        // Assert
        Render(statement).ShouldBe(expected);
    }

    [Theory]
    [InlineData("IF ( A .GT. B ) THEN", "IF (GreaterThan A B) THEN")]
    [InlineData("IF ( ( A .OR. B ) .AND. F(X) ) THEN", "IF (And (paren (Or A B)) F[X]) THEN")]
    [InlineData("ELSE IF ( N .EQ. 0 ) THEN", "ELSE IF (Equal N 0) THEN")]
    [InlineData("ELSE", "ELSE")]
    [InlineData("END IF", "END IF")]
    [InlineData("IF ( FAILED() ) RETURN", "IF FAILED[] => RETURN")]
    [InlineData("IF ( N .LT. 0 ) N = 0", "IF (LessThan N 0) => N = 0")]
    [InlineData("IF ( BAD ) CALL SIGERR ( 'SPICE(BAD)' )", "IF BAD => CALL SIGERR['SPICE(BAD)']")]
    [InlineData("IF ( X ) THEN = 1", "IF X => THEN = 1")]
    public void Parse_WhenStatementIsIf_ShouldReturnConditionAndWhatFollows(string text, string expected)
    {
        // Act
        var statement = Parse(text);

        // Assert
        Render(statement).ShouldBe(expected);
    }

    [Theory]
    [InlineData("DO I = 1, N", "DO I = 1, N")]
    [InlineData("DO I = N, 1, -1", "DO I = N, 1, (Minus 1)")]
    [InlineData("DO 10 I = 1, 5", "DO 10 I = 1, 5")]
    [InlineData("DO 10, I = 1, MAX(A,B), 2", "DO 10 I = 1, MAX[A, B], 2")]
    [InlineData("DO WHILE ( I .LE. N )", "DO WHILE (LessOrEqual I N)")]
    [InlineData("DO 20 WHILE ( MORE )", "DO 20 WHILE MORE")]
    [InlineData("END DO", "END DO")]
    [InlineData("CONTINUE", "CONTINUE")]
    public void Parse_WhenStatementIsLoop_ShouldReturnItsParts(string text, string expected)
    {
        // Act
        var statement = Parse(text);

        // Assert
        Render(statement).ShouldBe(expected);
    }

    [Theory]
    [InlineData("RETURN", "RETURN")]
    [InlineData("STOP", "STOP")]
    [InlineData("STOP 1", "STOP 1")]
    [InlineData("STOP 'finished'", "STOP 'finished'")]
    [InlineData("END", "END")]
    public void Parse_WhenStatementEndsExecution_ShouldReturnIt(string text, string expected)
    {
        // Act
        var statement = Parse(text);

        // Assert
        Render(statement).ShouldBe(expected);
    }

    [Theory]
    [InlineData("X = ")]
    [InlineData("CALL")]
    [InlineData("CALL S(1:2)")]
    [InlineData("CALL FOO ( *10 )")]
    [InlineData("IF ( A ) THEN X")]
    [InlineData("IF ( A ) IF ( B ) RETURN")]
    [InlineData("IF ( A ) DO I = 1, 2")]
    [InlineData("IF ( A ) END")]
    [InlineData("DO WHILE X")]
    [InlineData("RETURN 1")]
    [InlineData("STOP X + 1")]
    [InlineData("GO TO 10")]
    [InlineData("IF ( X ) 10, 20, 30")]
    public void Parse_WhenStatementIsMalformedOrUnsupported_ShouldThrowWithTheStatementLine(string text)
    {
        // Act & Assert
        var exception = Should.Throw<FortranSyntaxException>(() => Parse(text));
        exception.Line.ShouldBe(7);
    }

    private static Statement Parse(string text)
    {
        var statement = Lexer.Tokenize(new FixedFormStatement(null, text, 7, [], []));
        return StatementParser.Parse(statement, StatementClassifier.Classify(statement, startsProgramUnit: false));
    }
}
