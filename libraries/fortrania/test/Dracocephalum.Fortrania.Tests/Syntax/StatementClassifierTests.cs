using Dracocephalum.Fortrania.Syntax;
using Shouldly;

namespace Dracocephalum.Fortrania.Tests.Syntax;

public class StatementClassifierTests
{
    [Theory]
    [InlineData("X = 1")]
    [InlineData("A(I,J) = B(I) + C(J)")]
    [InlineData("NAME(1:3) = 'ABC'")]
    [InlineData("DO10I = 1.5")]
    [InlineData("IF(I) = 1")]
    [InlineData("IF(I)(2:3) = 'AB'")]
    [InlineData("REAL = 2.0")]
    [InlineData("F(X) = X + MAX(X, 1.0)")]
    public void Classify_WhenEqualsSignIsOutsideParentheses_ShouldReturnAssignment(string text)
    {
        // Act
        var kind = Classify(text);

        // Assert
        kind.ShouldBe(StatementKind.Assignment);
    }

    [Theory]
    [InlineData("DO 10 I = 1, 5")]
    [InlineData("DO I = 1, N")]
    [InlineData("DO 10, I = 1, N, 2")]
    [InlineData("DO X = A(1,2), B")]
    public void Classify_WhenCommaFollowsEqualsSign_ShouldReturnDo(string text)
    {
        // Act
        var kind = Classify(text);

        // Assert
        kind.ShouldBe(StatementKind.Do);
    }

    [Theory]
    [InlineData("IF ( A .GT. B ) THEN", StatementKind.BlockIf)]
    [InlineData("IF ( A .GT. B ) X = 1", StatementKind.LogicalIf)]
    [InlineData("IF ( FAILED() ) RETURN", StatementKind.LogicalIf)]
    [InlineData("IF ( A(I) .GT. 0 ) CALL FOO ( A, B )", StatementKind.LogicalIf)]
    [InlineData("IF ( X ) THEN = 1", StatementKind.LogicalIf)]
    [InlineData("IF ( X - Y ) 10, 20, 30", StatementKind.ArithmeticIf)]
    [InlineData("ELSE IF ( A ) THEN", StatementKind.ElseIf)]
    [InlineData("ELSE", StatementKind.Else)]
    [InlineData("END IF", StatementKind.EndIf)]
    [InlineData("ENDIF", StatementKind.EndIf)]
    [InlineData("DO WHILE ( I .LT. N )", StatementKind.DoWhile)]
    [InlineData("DO 10 WHILE ( I .LT. N )", StatementKind.DoWhile)]
    [InlineData("END DO", StatementKind.EndDo)]
    [InlineData("CONTINUE", StatementKind.Continue)]
    [InlineData("CALL CHKIN ( 'SPKEZ' )", StatementKind.Call)]
    [InlineData("RETURN", StatementKind.Return)]
    [InlineData("STOP 'done'", StatementKind.Stop)]
    [InlineData("PAUSE", StatementKind.Pause)]
    [InlineData("GO TO 10", StatementKind.GoTo)]
    [InlineData("GO TO ( 10, 20 ), I", StatementKind.GoTo)]
    [InlineData("ASSIGN 10 TO I", StatementKind.AssignLabel)]
    [InlineData("END", StatementKind.End)]
    public void Classify_WhenStatementIsControlFlow_ShouldReturnItsKind(string text, StatementKind expected)
    {
        // Act
        var kind = Classify(text);

        // Assert
        kind.ShouldBe(expected);
    }

    [Theory]
    [InlineData("INTEGER I, J", StatementKind.TypeDeclaration)]
    [InlineData("DOUBLE PRECISION STATE ( 6 )", StatementKind.TypeDeclaration)]
    [InlineData("CHARACTER*(*) NAME", StatementKind.TypeDeclaration)]
    [InlineData("REAL*8 X", StatementKind.TypeDeclaration)]
    [InlineData("LOGICAL FIRST", StatementKind.TypeDeclaration)]
    [InlineData("COMPLEX Z", StatementKind.TypeDeclaration)]
    [InlineData("IMPLICIT NONE", StatementKind.Implicit)]
    [InlineData("PARAMETER ( N = 3 )", StatementKind.Parameter)]
    [InlineData("DIMENSION A ( 10 )", StatementKind.Dimension)]
    [InlineData("COMMON / BLK / A, B", StatementKind.Common)]
    [InlineData("EQUIVALENCE ( A, B )", StatementKind.Equivalence)]
    [InlineData("EXTERNAL F", StatementKind.External)]
    [InlineData("INTRINSIC SIN", StatementKind.Intrinsic)]
    [InlineData("SAVE", StatementKind.Save)]
    [InlineData("SAVE FIRST", StatementKind.Save)]
    [InlineData("DATA FIRST / .TRUE. /", StatementKind.Data)]
    [InlineData("INCLUDE 'zzctr.inc'", StatementKind.Include)]
    public void Classify_WhenStatementIsSpecification_ShouldReturnItsKind(string text, StatementKind expected)
    {
        // Act
        var kind = Classify(text);

        // Assert
        kind.ShouldBe(expected);
    }

    [Theory]
    [InlineData("PROGRAM MAIN", StatementKind.Program)]
    [InlineData("SUBROUTINE VADD ( V1, V2, VOUT )", StatementKind.Subroutine)]
    [InlineData("FUNCTION F ( X )", StatementKind.Function)]
    [InlineData("INTEGER FUNCTION F ( X )", StatementKind.Function)]
    [InlineData("DOUBLE PRECISION FUNCTION VNORM ( V1 )", StatementKind.Function)]
    [InlineData("REAL*8 FUNCTION F ( X )", StatementKind.Function)]
    [InlineData("CHARACTER*(*) FUNCTION F ( X )", StatementKind.Function)]
    [InlineData("LOGICAL FUNCTION FAILED ()", StatementKind.Function)]
    [InlineData("ENTRY SETERR ( STATUS )", StatementKind.Entry)]
    [InlineData("BLOCK DATA", StatementKind.BlockData)]
    public void Classify_WhenStatementOpensProgramUnit_ShouldReturnItsKind(string text, StatementKind expected)
    {
        // Act
        var kind = Classify(text, startsProgramUnit: true);

        // Assert
        kind.ShouldBe(expected);
    }

    [Theory]
    [InlineData("INTEGER FUNCTION F ( X )")]
    [InlineData("REAL*8 FUNCTION F ( X )")]
    public void Classify_WhenTypedFunctionTextIsInsideProgramUnit_ShouldReturnTypeDeclaration(string text)
    {
        // Act
        var kind = Classify(text, startsProgramUnit: false);

        // Assert
        kind.ShouldBe(StatementKind.TypeDeclaration);
    }

    [Theory]
    [InlineData("READ ( UNIT, FMT='(A)', IOSTAT=IOSTAT ) LINE", StatementKind.Read)]
    [InlineData("WRITE ( UNIT, * ) ( A(I), I = 1, 3 )", StatementKind.Write)]
    [InlineData("PRINT *, X", StatementKind.Print)]
    [InlineData("OPEN ( UNIT=LUN, FILE=NAME, STATUS='OLD' )", StatementKind.Open)]
    [InlineData("CLOSE ( LUN )", StatementKind.Close)]
    [InlineData("INQUIRE ( FILE=NAME, EXIST=EXISTS )", StatementKind.Inquire)]
    [InlineData("REWIND ( LUN )", StatementKind.Rewind)]
    [InlineData("BACKSPACE LUN", StatementKind.Backspace)]
    [InlineData("END FILE LUN", StatementKind.EndFile)]
    [InlineData("FORMAT ( 1X, A )", StatementKind.Format)]
    public void Classify_WhenStatementIsInputOrOutput_ShouldReturnItsKind(string text, StatementKind expected)
    {
        // Act
        var kind = Classify(text);

        // Assert
        kind.ShouldBe(expected);
    }

    [Fact]
    public void Classify_WhenStatementHasNoTokens_ShouldReturnEmpty()
    {
        // Act
        var kind = Classify(string.Empty);

        // Assert
        kind.ShouldBe(StatementKind.Empty);
    }

    [Theory]
    [InlineData("FROBNICATE X")]
    [InlineData("X = 1, 2")]
    [InlineData("( X )")]
    public void Classify_WhenStatementIsNothingKnown_ShouldThrowWithTheStatementLine(string text)
    {
        // Act & Assert
        var exception = Should.Throw<FortranSyntaxException>(() => Classify(text));
        exception.Line.ShouldBe(7);
    }

    private static StatementKind Classify(string text, bool startsProgramUnit = false) =>
        StatementClassifier.Classify(Lexer.Tokenize(new FixedFormStatement(null, text, 7, [], [])), startsProgramUnit);
}
