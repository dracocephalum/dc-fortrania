using Dracocephalum.Fortrania.Analysis;
using Dracocephalum.Fortrania.Emit;
using Shouldly;

namespace Dracocephalum.Fortrania.Tests.Emit;

public class CSharpExpressionWriterTests
{
    private static readonly TreeExpression A = new TreeExpression.Local("a");
    private static readonly TreeExpression B = new TreeExpression.Local("b");
    private static readonly TreeExpression C = new TreeExpression.Local("c");

    [Fact]
    public void Write_WhenAWeakerOperationIsAnOperand_ShouldPutItInParentheses()
    {
        // Arrange
        var sum = new TreeExpression.Binary(TreeExpression.Operation.Add, A, B);
        var product = new TreeExpression.Binary(TreeExpression.Operation.Multiply, sum, C);

        // Act
        var text = CSharpExpressionWriter.Write(product);

        // Assert
        text.ShouldBe("(a + b) * c");
    }

    [Fact]
    public void Write_WhenTheRightOperandIsOfTheSameStrength_ShouldKeepItGroupedToTheRight()
    {
        // Arrange
        var inner = new TreeExpression.Binary(TreeExpression.Operation.Subtract, B, C);
        var left = new TreeExpression.Binary(TreeExpression.Operation.Subtract, inner, A);
        var right = new TreeExpression.Binary(TreeExpression.Operation.Subtract, A, inner);

        // Act & Assert
        CSharpExpressionWriter.Write(left).ShouldBe("b - c - a");
        CSharpExpressionWriter.Write(right).ShouldBe("a - (b - c)");
    }

    [Fact]
    public void Write_WhenASumHasTermsThatAreSums_ShouldGroupOnlyTheOnesTakenAway()
    {
        // Arrange
        var inner = new TreeExpression.Sum([new(B), new(C)]);
        var sum = new TreeExpression.Sum([new(A), new(inner), new(inner, IsSubtracted: true), new(new TreeExpression.Integer(1))]);

        // Act
        var text = CSharpExpressionWriter.Write(sum);

        // Assert
        text.ShouldBe("a + b + c - (b + c) + 1");
    }

    [Fact]
    public void Write_WhenAProductHasASumAmongItsFactors_ShouldGroupTheSum()
    {
        // Arrange
        var product = new TreeExpression.Product([new TreeExpression.Sum([new(A), new(B, IsSubtracted: true)]), new TreeExpression.Integer(8), C]);

        // Act
        var text = CSharpExpressionWriter.Write(product);

        // Assert
        text.ShouldBe("(a - b) * 8 * c");
    }

    [Fact]
    public void Write_WhenWhatIsNegatedStartsWithAMinusSign_ShouldKeepTheTwoApart()
    {
        // Arrange
        var once = new TreeExpression.Unary(TreeExpression.Operation.Negate, A);
        var twice = new TreeExpression.Unary(TreeExpression.Operation.Negate, once);
        var ofNegative = new TreeExpression.Unary(TreeExpression.Operation.Negate, new TreeExpression.Integer(-3));

        // Act & Assert
        CSharpExpressionWriter.Write(once).ShouldBe("-a");
        CSharpExpressionWriter.Write(twice).ShouldBe("-(-a)");
        CSharpExpressionWriter.Write(ofNegative).ShouldBe("-(-3)");
    }

    [Fact]
    public void Write_WhenGivenAddressesAndElements_ShouldSpellThemAsCSharpDoes()
    {
        // Arrange
        var element = new TreeExpression.Indexed(A, new TreeExpression.Sum([new(B), new(new TreeExpression.Integer(2), IsSubtracted: true)]));
        var stored = new TreeExpression.Dereference(A);
        var isSet = new TreeExpression.Binary(TreeExpression.Operation.NotEqual, stored, new TreeExpression.Integer(0));

        // Act & Assert
        CSharpExpressionWriter.Write(element).ShouldBe("a[b - 2]");
        CSharpExpressionWriter.Write(isSet).ShouldBe("*a != 0");
        CSharpExpressionWriter.Write(new TreeExpression.Unary(TreeExpression.Operation.Not, isSet)).ShouldBe("!(*a != 0)");
    }

    [Fact]
    public void Write_WhenGivenACallAConversionAndAChoice_ShouldSpellEach()
    {
        // Arrange
        var call = new TreeExpression.Call("Fortran.AddrOf", [new TreeExpression.Utf8("J2000")]);
        var converted = new TreeExpression.Converted("int", new TreeExpression.Binary(TreeExpression.Operation.Divide, A, B));
        var choice = new TreeExpression.Conditional(new TreeExpression.Binary(TreeExpression.Operation.Less, A, B), new TreeExpression.Integer(1), new TreeExpression.Integer(0));

        // Act & Assert
        CSharpExpressionWriter.Write(call).ShouldBe("Fortran.AddrOf(\"J2000\"u8)");
        CSharpExpressionWriter.Write(converted).ShouldBe("(int)(a / b)");
        CSharpExpressionWriter.Write(choice).ShouldBe("a < b ? 1 : 0");
    }

    [Fact]
    public void Write_WhenAnAddressIsTakenAndCast_ShouldWriteTheCastBeforeTheAddress()
    {
        // Arrange
        var element = new TreeExpression.Indexed(A, B);
        var cast = new TreeExpression.Converted("double*", new TreeExpression.AddressOf(element));

        // Act
        var text = CSharpExpressionWriter.Write(cast);

        // Assert
        text.ShouldBe("(double*)&a[b]");
    }

    [Fact]
    public void Write_WhenBitsAreCombined_ShouldGroupEachOperandThatIsAnOperation()
    {
        // Arrange
        var sum = new TreeExpression.Binary(TreeExpression.Operation.Add, A, B);
        var both = new TreeExpression.Binary(TreeExpression.Operation.BitAnd, sum, C);
        var flipped = new TreeExpression.Unary(TreeExpression.Operation.Complement, both);

        // Act & Assert
        CSharpExpressionWriter.Write(both).ShouldBe("(a + b) & c");
        CSharpExpressionWriter.Write(new TreeExpression.Binary(TreeExpression.Operation.BitXor, A, B)).ShouldBe("a ^ b");
        CSharpExpressionWriter.Write(new TreeExpression.Binary(TreeExpression.Operation.BitOr, A, B)).ShouldBe("a | b");
        CSharpExpressionWriter.Write(flipped).ShouldBe("~((a + b) & c)");
    }

    [Fact]
    public void Write_WhenARemainderIsTakenOfARemainder_ShouldKeepTheRightOneGrouped()
    {
        // Arrange
        var inner = new TreeExpression.Binary(TreeExpression.Operation.Remainder, B, C);
        var remainder = new TreeExpression.Binary(TreeExpression.Operation.Remainder, A, inner);

        // Act
        var text = CSharpExpressionWriter.Write(remainder);

        // Assert
        text.ShouldBe("a % (b % c)");
    }

    [Fact]
    public void Write_WhenGivenConstants_ShouldWriteThemAsLiterals()
    {
        // Act & Assert
        CSharpExpressionWriter.Write(new TreeExpression.Integer(-12)).ShouldBe("-12");
        CSharpExpressionWriter.Write(new TreeExpression.Number(new Constant.Logical(true))).ShouldBe("true");
        CSharpExpressionWriter.Write(new TreeExpression.Number(new Constant.Real(1.5f))).ShouldBe("1.5F");
    }

    [Fact]
    public void WriteAt_WhenTextOfAnotherPartBindsLessThanNeeded_ShouldPutItInParentheses()
    {
        // Arrange
        var raw = new TreeExpression.Raw("x ? y : z", Level.Conditional);

        // Act & Assert
        CSharpExpressionWriter.WriteAt(raw, Level.Additive).ShouldBe("(x ? y : z)");
        CSharpExpressionWriter.WriteAt(new TreeExpression.Grouped(raw), Level.Primary).ShouldBe("(x ? y : z)");
        CSharpExpressionWriter.WriteAt(A, Level.Primary).ShouldBe("a");
    }
}
