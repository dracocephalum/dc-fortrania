using Dracocephalum.Fortrania.Analysis;
using Dracocephalum.Fortrania.Syntax;
using Shouldly;

namespace Dracocephalum.Fortrania.Tests.Analysis;

public class ExpressionTyperTests
{
    // I, J integer; R, S real; D, E double precision; Z complex; L, M
    // logical; C, T character; IA an integer array of rank two; DA a double
    // precision array; CA a character array; IFUN, DFUN, CFUN external
    // functions; SF a statement function of two arguments; N a constant.
    private static readonly string[] Declarations =
    [
        "      SUBROUTINE S0 ( PROC )",
        "      IMPLICIT NONE",
        "      INTEGER I, J, IA ( 3, 4 ), IFUN, N",
        "      REAL R, S",
        "      DOUBLE PRECISION D, E, DA ( 5 ), DFUN, SF",
        "      COMPLEX Z",
        "      LOGICAL L, M",
        "      CHARACTER*(10) C, T, CA ( 2 ), CFUN",
        "      PARAMETER ( N = 4 )",
        "      EXTERNAL PROC, IFUN",
        "      SF ( D, I ) = D * I",
    ];

    [Theory]
    [InlineData("1", DataType.Integer)]
    [InlineData("1.5", DataType.Real)]
    [InlineData("1.5D0", DataType.DoublePrecision)]
    [InlineData("'A'", DataType.Character)]
    [InlineData(".TRUE.", DataType.Logical)]
    [InlineData("I", DataType.Integer)]
    [InlineData("N", DataType.Integer)]
    [InlineData("DA", DataType.DoublePrecision)]
    [InlineData("( R )", DataType.Real)]
    [InlineData("-I", DataType.Integer)]
    [InlineData("+D", DataType.DoublePrecision)]
    [InlineData(".NOT. L", DataType.Logical)]
    public void TypeOf_WhenExpressionIsAPrimaryOrHasASign_ShouldReturnItsType(string expression, DataType expected) =>
        TypeOf(expression).ShouldBe(expected);

    [Theory]
    [InlineData("I + J", DataType.Integer)]
    [InlineData("I / J", DataType.Integer)]
    [InlineData("I + R", DataType.Real)]
    [InlineData("R * I", DataType.Real)]
    [InlineData("R - D", DataType.DoublePrecision)]
    [InlineData("I ** D", DataType.DoublePrecision)]
    [InlineData("D ** I", DataType.DoublePrecision)]
    [InlineData("Z + I", DataType.Complex)]
    [InlineData("R * Z", DataType.Complex)]
    [InlineData("I + J * R - D", DataType.DoublePrecision)]
    [InlineData("C // T", DataType.Character)]
    [InlineData("C // 'X' // T ( 1:2 )", DataType.Character)]
    [InlineData("I .LT. R", DataType.Logical)]
    [InlineData("D .GE. I", DataType.Logical)]
    [InlineData("Z .EQ. R", DataType.Logical)]
    [InlineData("Z .NE. Z", DataType.Logical)]
    [InlineData("C .EQ. T", DataType.Logical)]
    [InlineData("C .LT. 'B'", DataType.Logical)]
    [InlineData("L .AND. M", DataType.Logical)]
    [InlineData("L .OR. I .GT. J", DataType.Logical)]
    [InlineData("L .EQV. M", DataType.Logical)]
    [InlineData("L .NEQV. .NOT. M", DataType.Logical)]
    public void TypeOf_WhenOperatorCombinesTwoOperands_ShouldReturnTheResultType(string expression, DataType expected) =>
        TypeOf(expression).ShouldBe(expected);

    [Theory]
    [InlineData("IA ( I, J + 1 )", DataType.Integer)]
    [InlineData("DA ( IA ( 1, 2 ) )", DataType.DoublePrecision)]
    [InlineData("CA ( 1 )", DataType.Character)]
    [InlineData("CA ( 2 ) ( 3:I )", DataType.Character)]
    [InlineData("C ( :3 )", DataType.Character)]
    [InlineData("C ( I: )", DataType.Character)]
    [InlineData("IFUN ( D, C, PROC )", DataType.Integer)]
    [InlineData("DFUN ( )", DataType.DoublePrecision)]
    [InlineData("CFUN ( IFUN, DA )", DataType.Character)]
    [InlineData("SF ( E, 2 )", DataType.DoublePrecision)]
    public void TypeOf_WhenNameIsGivenSubscriptsOrArguments_ShouldReturnTheTypeOfTheName(string expression, DataType expected) =>
        TypeOf(expression).ShouldBe(expected);

    [Theory]
    [InlineData("INT ( D )", DataType.Integer)]
    [InlineData("NINT ( R )", DataType.Integer)]
    [InlineData("REAL ( I )", DataType.Real)]
    [InlineData("DBLE ( I )", DataType.DoublePrecision)]
    [InlineData("CMPLX ( R, S )", DataType.Complex)]
    [InlineData("ICHAR ( C ( 1:1 ) )", DataType.Integer)]
    [InlineData("CHAR ( I )", DataType.Character)]
    [InlineData("ABS ( I )", DataType.Integer)]
    [InlineData("ABS ( D )", DataType.DoublePrecision)]
    [InlineData("ABS ( Z )", DataType.Real)]
    [InlineData("SQRT ( D )", DataType.DoublePrecision)]
    [InlineData("SQRT ( R )", DataType.Real)]
    [InlineData("SQRT ( Z )", DataType.Complex)]
    [InlineData("DSQRT ( D )", DataType.DoublePrecision)]
    [InlineData("MOD ( I, J )", DataType.Integer)]
    [InlineData("MOD ( D, E )", DataType.DoublePrecision)]
    [InlineData("MAX ( I, J, 3, N )", DataType.Integer)]
    [InlineData("MIN ( D, E )", DataType.DoublePrecision)]
    [InlineData("AMAX0 ( I, J )", DataType.Real)]
    [InlineData("MAX1 ( R, S )", DataType.Integer)]
    [InlineData("DPROD ( R, S )", DataType.DoublePrecision)]
    [InlineData("ATAN2 ( D, E )", DataType.DoublePrecision)]
    [InlineData("LEN ( C )", DataType.Integer)]
    [InlineData("INDEX ( C, 'X' )", DataType.Integer)]
    [InlineData("LGE ( C, T )", DataType.Logical)]
    [InlineData("AIMAG ( Z )", DataType.Real)]
    [InlineData("ISHFT ( I, 8 )", DataType.Integer)]
    [InlineData("BTEST ( I, 3 )", DataType.Logical)]
    public void TypeOf_WhenFunctionIsIntrinsic_ShouldReturnWhatItGivesForThoseArguments(string expression, DataType expected) =>
        TypeOf(expression).ShouldBe(expected);

    [Theory]
    [InlineData("I + L")]
    [InlineData("C + 1")]
    [InlineData("D * Z")]
    [InlineData("C // I")]
    [InlineData("L .LT. M")]
    [InlineData("Z .LT. Z")]
    [InlineData("C .EQ. I")]
    [InlineData("L .EQ. M")]
    [InlineData("I .AND. J")]
    [InlineData(".NOT. I")]
    [InlineData("-L")]
    [InlineData("-C")]
    [InlineData("IA ( 1 )")]
    [InlineData("IA ( 1, 2, 3 )")]
    [InlineData("DA ( R )")]
    [InlineData("I ( 1:2 )")]
    [InlineData("C ( R: )")]
    [InlineData("C ( :D )")]
    [InlineData("SF ( D )")]
    [InlineData("SF ( D, PROC )")]
    [InlineData("PROC")]
    [InlineData("I + PROC")]
    [InlineData("SQRT ( I )")]
    [InlineData("SQRT ( D, E )")]
    [InlineData("MAX ( I )")]
    [InlineData("MAX ( I, D )")]
    [InlineData("MOD ( I )")]
    [InlineData("DSQRT ( R )")]
    [InlineData("LEN ( I )")]
    [InlineData("ICHAR ( I )")]
    [InlineData("ISHFT ( D, 1 )")]
    public void TypeOf_WhenOperandsDoNotFit_ShouldThrowWithTheLineGiven(string expression)
    {
        // Act & Assert
        var exception = Should.Throw<FortranAnalysisException>(() => TypeOf(expression));
        exception.Line.ShouldBe(42);
    }

    [Fact]
    public void TypeOf_WhenNameIsNotOfTheUnit_ShouldThrow()
    {
        // Arrange
        var typer = new ExpressionTyper(Build([.. Declarations, "      END"]));

        // Act & Assert
        var exception = Should.Throw<FortranAnalysisException>(
            () => typer.TypeOf(new Expression.Name("STRANGER"), 7, "there.inc"));
        exception.Message.ShouldBe("Line 7 of include file 'there.inc': STRANGER is not a name of this program unit.");
    }

    // The expression is put in a CALL so that every name in it is one the
    // unit uses, then taken back out of the parsed statement.
    private static DataType TypeOf(string expression)
    {
        var table = Build([.. Declarations, $"      CALL USE ( {expression} )", "      END"]);
        var call = (Statement.Call)((Construct.Simple)table.Unit.Body[^1]).Statement.Statement;
        return new ExpressionTyper(table).TypeOf(call.Arguments[0], 42);
    }

    private static SymbolTable Build(string[] lines)
    {
        using var reader = new StringReader(string.Join('\n', lines));
        var unit = SourceParser.Parse(FixedFormReader.Read(reader)).Units.ShouldHaveSingleItem();
        return SymbolTableBuilder.Build(unit, _ => null);
    }
}
