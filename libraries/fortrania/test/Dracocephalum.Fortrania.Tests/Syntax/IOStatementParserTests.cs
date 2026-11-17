using Dracocephalum.Fortrania.Syntax;
using Shouldly;
using static Dracocephalum.Fortrania.Tests.Syntax.SyntaxRenderer;

namespace Dracocephalum.Fortrania.Tests.Syntax;

public class IOStatementParserTests
{
    [Theory]
    [InlineData("READ ( UNIT, FMT='(A)', IOSTAT=IOSTAT ) LINE", "READ(UNIT, FMT='(A)', IOSTAT=IOSTAT) LINE")]
    [InlineData("READ ( UNIT=LUN, REC=RECNO, IOSTAT=IOSTAT ) BUFFER", "READ(UNIT=LUN, REC=RECNO, IOSTAT=IOSTAT) BUFFER")]
    [InlineData("READ ( 5, * ) A, B(I), S(1:3)", "READ(5, *) A, B[I], S<1:3>")]
    [InlineData("READ ( LUN, *, END=100, ERR=200 ) X", "READ(LUN, *, END=100, ERR=200) X")]
    [InlineData("READ ( LUN )", "READ(LUN)")]
    [InlineData("READ *, X, Y", "READ(FMT=*) X, Y")]
    [InlineData("READ '(A)', LINE", "READ(FMT='(A)') LINE")]
    [InlineData("READ 100", "READ(FMT=100)")]
    public void Parse_WhenStatementIsRead_ShouldReturnControlsAndItems(string text, string expected)
    {
        // Act
        var statement = Parse(text);

        // Assert
        Render(statement).ShouldBe(expected);
    }

    [Theory]
    [InlineData("WRITE ( *, * ) 'Hello'", "WRITE(*, *) 'Hello'")]
    [InlineData("WRITE ( UNIT, FMT='(A)', IOSTAT=IOSTAT ) LINE(:RTRIM(LINE))", "WRITE(UNIT, FMT='(A)', IOSTAT=IOSTAT) LINE<:RTRIM[LINE]>")]
    [InlineData("WRITE ( LUN, REC=N ) ( A(I), I = 1, 128 )", "WRITE(LUN, REC=N) (A[I], I=1..128)")]
    [InlineData("WRITE ( 6, * ) ( ( M(I,J), I = 1, 3 ), J = 1, 3, 2 )", "WRITE(6, *) ((M[I, J], I=1..3), J=1..3 step 2)")]
    [InlineData("WRITE ( 6, * ) ( A + B ), ( C(I), D(I), I = 1, N )", "WRITE(6, *) (paren (Add A B)), (C[I], D[I], I=1..N)")]
    [InlineData("WRITE ( 6, 100 ) X * 2, -Y", "WRITE(6, 100) (Multiply X 2), (Minus Y)")]
    [InlineData("WRITE ( UNIT = *, FMT = * )", "WRITE(UNIT=*, FMT=*)")]
    [InlineData("PRINT *, 'A', X", "PRINT *: 'A', X")]
    [InlineData("PRINT *", "PRINT *:")]
    [InlineData("PRINT '(1X,A)', NAME", "PRINT '(1X,A)': NAME")]
    [InlineData("PRINT 100, X", "PRINT 100: X")]
    public void Parse_WhenStatementWrites_ShouldReturnControlsAndItems(string text, string expected)
    {
        // Act
        var statement = Parse(text);

        // Assert
        Render(statement).ShouldBe(expected);
    }

    [Theory]
    [InlineData(
        "OPEN ( UNIT=LUN, FILE=NAME, STATUS='OLD', ACCESS='DIRECT', RECL=1024, IOSTAT=IOSTAT )",
        "OPEN(UNIT=LUN, FILE=NAME, STATUS='OLD', ACCESS='DIRECT', RECL=1024, IOSTAT=IOSTAT)")]
    [InlineData("OPEN ( LUN, STATUS='SCRATCH' )", "OPEN(LUN, STATUS='SCRATCH')")]
    [InlineData("CLOSE ( LUN )", "CLOSE(LUN)")]
    [InlineData("CLOSE ( UNIT=LUN, STATUS='DELETE' )", "CLOSE(UNIT=LUN, STATUS='DELETE')")]
    [InlineData("INQUIRE ( FILE=NAME, EXIST=EXISTS, OPENED=OPENED )", "INQUIRE(FILE=NAME, EXIST=EXISTS, OPENED=OPENED)")]
    [InlineData("REWIND ( LUN )", "REWIND(LUN)")]
    [InlineData("REWIND LUN", "REWIND(LUN)")]
    [InlineData("REWIND ( UNIT=LUN, IOSTAT=IOSTAT )", "REWIND(UNIT=LUN, IOSTAT=IOSTAT)")]
    [InlineData("BACKSPACE 5", "BACKSPACE(5)")]
    [InlineData("END FILE ( LUN )", "ENDFILE(LUN)")]
    public void Parse_WhenStatementManagesFile_ShouldReturnControls(string text, string expected)
    {
        // Act
        var statement = Parse(text);

        // Assert
        Render(statement).ShouldBe(expected);
    }

    [Theory]
    [InlineData("WRITE 6, X")]
    [InlineData("WRITE ( 6, * X")]
    [InlineData("WRITE ( 6, * ) X,")]
    [InlineData("WRITE ( 6, * ) ( I = 1, 3 )")]
    [InlineData("WRITE ( 6, * ) ( A(I), I = 1 )")]
    [InlineData("READ ( UNIT= ) X")]
    [InlineData("PRINT *  X")]
    [InlineData("OPEN LUN")]
    [InlineData("CLOSE ( LUN ) X")]
    [InlineData("REWIND")]
    public void Parse_WhenStatementIsMalformed_ShouldThrowWithTheStatementLine(string text)
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
