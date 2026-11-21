using Dracocephalum.Fortrania.Analysis;
using Dracocephalum.Fortrania.Syntax;
using Shouldly;

namespace Dracocephalum.Fortrania.Tests.Analysis;

public class ConstantEvaluatorTests
{
    // The expected values in the first three tests are what gfortran 16.2
    // prints for the same PARAMETER statements, to every digit.
    [Theory]
    [InlineData("PI = 3.14159265358979", 3.14159274101257324E+00)]
    [InlineData("THIRD = 1.D0 / 3", 3.33333333333333315E-01)]
    [InlineData("DPOW = 1.5D0 ** 2.5", 2.75567596063107523E+00)]
    [InlineData("MIXED = 1 / 3 * 3.0 + 0.1", 1.00000001490116119E-01)]
    [InlineData("FROMI = 7 / 2", 3.0)]
    [InlineData("DSUM = 0.1 + 0.2D0", 3.00000001490116130E-01)]
    public void ValueOf_WhenConstantIsDoublePrecision_ShouldComputeAsTheReferenceCompilerDoes(string definition, double expected)
    {
        // Act
        var value = ValueOf($"      DOUBLE PRECISION {NameIn(definition)}", definition);

        // Assert
        value.ShouldBeOfType<Constant.DoublePrecision>().Value.ShouldBe(expected);
    }

    [Theory]
    [InlineData("RTHIRD = 1.0 / 3.0", 3.333333433E-01f)]
    [InlineData("RSUM = 0.1 + 0.2", 3.000000119E-01f)]
    [InlineData("RPOW = 1.1 ** 2.5", 1.269058824E+00f)]
    [InlineData("RPOWI = 1.1 ** 7", 1.948717356E+00f)]
    [InlineData("RSQ = 2.0 ** 0.5", 1.414213538E+00f)]
    [InlineData("RWIDE = 1.D0 / 3", 3.333333433E-01f)]
    [InlineData("RINT = 3", 3f)]
    public void ValueOf_WhenConstantIsReal_ShouldComputeInSinglePrecision(string definition, float expected)
    {
        // Act
        var value = ValueOf($"      REAL {NameIn(definition)}", definition);

        // Assert
        value.ShouldBeOfType<Constant.Real>().Value.ShouldBe(expected);
    }

    [Theory]
    [InlineData("HALF = 7 / 2", 3)]
    [InlineData("NHALF = -7 / 2", -3)]
    [InlineData("IPOW = 2 ** 10", 1024)]
    [InlineData("NEGPOW = 10 ** (-2)", 0)]
    [InlineData("ONEPOW = (-1) ** (-3)", -1)]
    [InlineData("EVENPW = (-1) ** (-4)", 1)]
    [InlineData("UNIT = 1 ** (-5)", 1)]
    [InlineData("TRUNC = 2.99D0", 2)]
    [InlineData("NTRUNC = -2.99", -2)]
    [InlineData("MOSTNG = -2147483647 - 1", int.MinValue)]
    [InlineData("LITNEG = -2147483648", int.MinValue)]
    [InlineData("MOST = 2147483647", int.MaxValue)]
    [InlineData("ARITH = ( 1 + 2 ) * 3 - +4", 5)]
    [InlineData("ZEROPW = 5 ** 0", 1)]
    public void ValueOf_WhenConstantIsInteger_ShouldTruncateTowardZeroAndKeepTo32Bits(string definition, int expected)
    {
        // Act
        var value = ValueOf($"      INTEGER {NameIn(definition)}", definition);

        // Assert
        value.ShouldBeOfType<Constant.Integer>().Value.ShouldBe(expected);
    }

    [Theory]
    [InlineData("CHARACTER*5 SHORT", "SHORT = 'AB'", "AB   ")]
    [InlineData("CHARACTER*2 CUT", "CUT = 'XYZ'", "XY")]
    [InlineData("CHARACTER*(*) WHOLE", "WHOLE = 'AB' // 'CD '", "ABCD ")]
    [InlineData("CHARACTER ONE", "ONE = 'XYZ'", "X")]
    [InlineData("CHARACTER*(*) QUOTE", "QUOTE = 'IT''S'", "IT'S")]
    [InlineData("CHARACTER*(2+1) SIZED", "SIZED = 'A'", "A  ")]
    [InlineData("CHARACTER*(*) LOWER", "LOWER = 'a b'", "a b")]
    public void ValueOf_WhenConstantIsCharacter_ShouldCutOrPadToItsLength(string declaration, string definition, string expected)
    {
        // Act
        var value = ValueOf($"      {declaration}", definition);

        // Assert
        value.ShouldBeOfType<Constant.Character>().Value.ShouldBe(expected);
    }

    [Theory]
    [InlineData("LESS = 'AB' .LT. 'AB ' // 'C'", true)]
    [InlineData("SAME = 'AB' .EQ. 'AB  '", true)]
    [InlineData("YES = .TRUE.", true)]
    [InlineData("NO = .NOT. .TRUE.", false)]
    [InlineData("BOTH = 1 .LT. 2 .AND. 2.5 .GE. 2", true)]
    [InlineData("EITHER = 1 .GT. 2 .OR. 1.D0 .NE. 1", false)]
    [InlineData("AGREE = .TRUE. .EQV. .FALSE.", false)]
    [InlineData("DIFFER = .TRUE. .NEQV. .FALSE.", true)]
    [InlineData("LE = 3 .LE. 3", true)]
    public void ValueOf_WhenConstantIsLogical_ShouldCompareAndCombine(string definition, bool expected)
    {
        // Act
        var value = ValueOf($"      LOGICAL {NameIn(definition)}", definition);

        // Assert
        value.ShouldBeOfType<Constant.Logical>().Value.ShouldBe(expected);
    }

    [Fact]
    public void ValueOf_WhenConstantsBuildOnEachOther_ShouldFollowTheChain()
    {
        // Arrange
        string[] lines =
        [
            "      SUBROUTINE S",
            "      INTEGER LBCELL, MAXN, SIZE",
            "      DOUBLE PRECISION SCALE",
            "      PARAMETER ( LBCELL = -5 )",
            "      PARAMETER ( MAXN = 100, SIZE = MAXN - LBCELL + 1 )",
            "      PARAMETER ( SCALE = SIZE / 4 * 1.D-1 )",
            "      END",
        ];
        var table = Build(lines);
        var evaluator = new ConstantEvaluator(table);

        // Act
        var size = evaluator.ValueOf(table.Find("SIZE").ShouldNotBeNull());
        var scale = evaluator.ValueOf(table.Find("SCALE").ShouldNotBeNull());

        // Assert
        size.ShouldBe(new Constant.Integer(106));
        scale.ShouldBe(new Constant.DoublePrecision(26 * 1.0E-1));
        evaluator.ValueOf(table.Find("SIZE").ShouldNotBeNull()).ShouldBeSameAs(size);
    }

    [Fact]
    public void TryEvaluate_WhenExpressionIsABoundOrLength_ShouldEvaluateOnlyWhatIsConstant()
    {
        // Arrange
        string[] lines =
        [
            "      SUBROUTINE S ( N, A )",
            "      INTEGER N, LB, FIXED ( 3 ), K",
            "      PARAMETER ( LB = -5 )",
            "      DOUBLE PRECISION A ( LB : N ), B ( LB : 2 * 4 ),",
            "     .                 C ( FIXED ( 1 ) ), D ( MAX ( 1, 2 ) )",
            "      CHARACTER*( 10 - LB ) NAME",
            "      END",
        ];
        var table = Build(lines);
        var evaluator = new ConstantEvaluator(table);
        Constant? Bound(string name, bool upper)
        {
            var bound = table.Find(name).ShouldNotBeNull().Dimensions[0];
            return evaluator.TryEvaluate((upper ? bound.Upper : bound.Lower).ShouldNotBeNull(), 4);
        }

        // Act & Assert
        Bound("A", upper: false).ShouldBe(new Constant.Integer(-5));
        Bound("A", upper: true).ShouldBeNull();
        Bound("B", upper: true).ShouldBe(new Constant.Integer(8));
        Bound("C", upper: true).ShouldBeNull();
        Bound("D", upper: true).ShouldBeNull();
        evaluator.TryEvaluate(table.Find("NAME").ShouldNotBeNull().Type.ShouldNotBeNull().Length.ShouldNotBeNull().Value.ShouldNotBeNull(), 6)
            .ShouldBe(new Constant.Integer(15));
        evaluator.TryEvaluate(new Expression.Substring(new Expression.Literal(DataType.Character, "'ABC'"), null, null), 1).ShouldBeNull();
    }

    [Theory]
    [InlineData("INTEGER BAD", "BAD = 2147483647 + 1")]
    [InlineData("INTEGER BAD", "BAD = 65536 * 65536")]
    [InlineData("INTEGER BAD", "BAD = 2 ** 31")]
    [InlineData("INTEGER BAD", "BAD = 2147483648")]
    [InlineData("INTEGER BAD", "BAD = 99999999999999999999")]
    [InlineData("INTEGER BAD", "BAD = -( -2147483647 - 1 )")]
    [InlineData("INTEGER BAD", "BAD = 1 / 0")]
    [InlineData("INTEGER BAD", "BAD = 0 ** (-1)")]
    [InlineData("INTEGER BAD", "BAD = 3.D10")]
    [InlineData("INTEGER BAD", "BAD = VAR + 1")]
    [InlineData("INTEGER BAD", "BAD = MAX ( 1, 2 )")]
    [InlineData("INTEGER BAD", "BAD = BAD + 1")]
    [InlineData("INTEGER BAD, WORSE", "BAD = WORSE, WORSE = BAD")]
    [InlineData("REAL BAD", "BAD = 1.0 / 0.0")]
    [InlineData("REAL BAD", "BAD = 1.E30 * 1.E30")]
    [InlineData("REAL BAD", "BAD = 1.E99")]
    [InlineData("REAL BAD", "BAD = 1.D99")]
    [InlineData("DOUBLE PRECISION BAD", "BAD = 1.D0 / 0")]
    [InlineData("DOUBLE PRECISION BAD", "BAD = 1.D999")]
    [InlineData("DOUBLE PRECISION BAD", "BAD = 1.D300 * 1.D300")]
    [InlineData("DOUBLE PRECISION BAD", "BAD = ( -8.D0 ) ** 0.5D0")]
    [InlineData("CHARACTER*(VAR) BAD", "BAD = 'X'")]
    [InlineData("CHARACTER*(0) BAD", "BAD = 'X'")]
    public void ValueOf_WhenConstantHasNoValue_ShouldThrowWhereItIsDeclared(string declaration, string definition)
    {
        // Arrange
        var table = Build(["      SUBROUTINE S ( VAR )", "      INTEGER VAR", $"      {declaration}", $"      PARAMETER ( {definition} )", "      END"]);

        // Act & Assert
        var exception = Should.Throw<FortranAnalysisException>(() => new ConstantEvaluator(table).ValueOf(table.Find("BAD").ShouldNotBeNull()));
        exception.Line.ShouldBe(3);
    }

    [Theory]
    [InlineData("'A' + 1")]
    [InlineData("1 // 'A'")]
    [InlineData(".TRUE. .AND. 1")]
    [InlineData(".TRUE. .LT. .FALSE.")]
    [InlineData("-'A'")]
    [InlineData("+.TRUE.")]
    [InlineData(".NOT. 1")]
    public void TryEvaluate_WhenConstantOperandsDoNotCombine_ShouldThrow(string expression)
    {
        // Arrange: PRINT takes any expression, typed or not, since nothing
        // here runs the type checker.
        var table = Build(["      SUBROUTINE S", $"      PRINT *, {expression}", "      END"]);
        var print = (Statement.Print)((Construct.Simple)table.Unit.Body[0]).Statement.Statement;
        var item = ((IOItem.Value)print.Items[0]).Expression;

        // Act & Assert
        Should.Throw<FortranAnalysisException>(() => new ConstantEvaluator(table).TryEvaluate(item, 2)).Line.ShouldBe(2);
    }

    [Fact]
    public void ValueOf_WhenSymbolIsNotAConstantOfTheUnit_ShouldRefuseIt()
    {
        // Arrange
        string[] lines = ["      SUBROUTINE S", "      INTEGER N, V", "      PARAMETER ( N = 1 )", "      V = N", "      END"];
        var table = Build(lines);
        var other = Build(lines);
        var evaluator = new ConstantEvaluator(table);

        // Act & Assert
        Should.Throw<ArgumentException>(() => evaluator.ValueOf(table.Find("V").ShouldNotBeNull()));
        Should.Throw<ArgumentException>(() => evaluator.ValueOf(other.Find("N").ShouldNotBeNull()));
    }

    private static string NameIn(string definition) => definition[..definition.IndexOf(' ', StringComparison.Ordinal)];

    private static Constant ValueOf(string declaration, string definition)
    {
        var table = Build(["      SUBROUTINE S", declaration, $"      PARAMETER ( {definition} )", "      END"]);
        return new ConstantEvaluator(table).ValueOf(table.Find(NameIn(definition)).ShouldNotBeNull());
    }

    private static SymbolTable Build(string[] lines)
    {
        using var reader = new StringReader(string.Join('\n', lines));
        var unit = SourceParser.Parse(FixedFormReader.Read(reader)).Units.ShouldHaveSingleItem();
        return SymbolTableBuilder.Build(unit, _ => null);
    }
}
