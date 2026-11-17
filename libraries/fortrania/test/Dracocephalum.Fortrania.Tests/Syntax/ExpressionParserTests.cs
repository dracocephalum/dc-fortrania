using Dracocephalum.Fortrania.Syntax;
using Shouldly;
using static Dracocephalum.Fortrania.Tests.Syntax.SyntaxRenderer;

namespace Dracocephalum.Fortrania.Tests.Syntax;

public class ExpressionParserTests
{
    [Theory]
    [InlineData("42", "42")]
    [InlineData("1.5", "1.5")]
    [InlineData("1.0D0", "1.0D0")]
    [InlineData("'it''s'", "'it''s'")]
    [InlineData(".TRUE.", ".TRUE.")]
    [InlineData("X", "X")]
    [InlineData("( X )", "(paren X)")]
    public void Parse_WhenTextIsSingleOperand_ShouldReturnIt(string text, string expected)
    {
        // Act
        var expression = Parse(text);

        // Assert
        Render(expression).ShouldBe(expected);
    }

    [Theory]
    [InlineData("A + B * C", "(Add A (Multiply B C))")]
    [InlineData("A * B + C", "(Add (Multiply A B) C)")]
    [InlineData("A - B - C", "(Subtract (Subtract A B) C)")]
    [InlineData("A / B * C", "(Multiply (Divide A B) C)")]
    [InlineData("A ** B ** C", "(Power A (Power B C))")]
    [InlineData("A * B ** C", "(Multiply A (Power B C))")]
    [InlineData("( A + B ) * C", "(Multiply (paren (Add A B)) C)")]
    [InlineData("A // B // C", "(Concatenate (Concatenate A B) C)")]
    [InlineData("A + B .GT. C * D", "(GreaterThan (Add A B) (Multiply C D))")]
    [InlineData("A // B .EQ. C", "(Equal (Concatenate A B) C)")]
    [InlineData("A .OR. B .AND. C", "(Or A (And B C))")]
    [InlineData("A .AND. B .OR. C", "(Or (And A B) C)")]
    [InlineData("A .EQV. B .OR. C", "(Equivalent A (Or B C))")]
    [InlineData("A .LT. B .AND. C .GE. D", "(And (LessThan A B) (GreaterOrEqual C D))")]
    [InlineData("A < B .NEQV. C /= D", "(NotEquivalent (LessThan A B) (NotEqual C D))")]
    public void Parse_WhenOperatorsAreMixed_ShouldFollowPrecedenceAndAssociativity(string text, string expected)
    {
        // Act
        var expression = Parse(text);

        // Assert
        Render(expression).ShouldBe(expected);
    }

    [Theory]
    [InlineData("-A", "(Minus A)")]
    [InlineData("-A + B", "(Add (Minus A) B)")]
    [InlineData("-A * B", "(Minus (Multiply A B))")]
    [InlineData("-A ** 2", "(Minus (Power A 2))")]
    [InlineData("A ** -B", "(Power A (Minus B))")]
    [InlineData("A ** -B * C", "(Multiply (Power A (Minus B)) C)")]
    [InlineData("A ** -B ** C", "(Power A (Minus (Power B C)))")]
    [InlineData("A / -B * C", "(Multiply (Divide A (Minus B)) C)")]
    [InlineData("A * -B ** C", "(Multiply A (Minus (Power B C)))")]
    [InlineData("A - -B * C", "(Subtract A (Minus (Multiply B C)))")]
    [InlineData("-A * B - C", "(Subtract (Minus (Multiply A B)) C)")]
    [InlineData("A - -B", "(Subtract A (Minus B))")]
    [InlineData("+A - B", "(Subtract (Plus A) B)")]
    [InlineData(".NOT. A", "(Not A)")]
    [InlineData(".NOT. A .AND. B", "(And (Not A) B)")]
    [InlineData(".NOT. A .EQ. B", "(Not (Equal A B))")]
    [InlineData("A .OR. .NOT. B", "(Or A (Not B))")]
    public void Parse_WhenOperatorIsUnary_ShouldTakeTheRightOperand(string text, string expected)
    {
        // Act
        var expression = Parse(text);

        // Assert
        Render(expression).ShouldBe(expected);
    }

    [Theory]
    [InlineData("F()", "F[]")]
    [InlineData("A(I)", "A[I]")]
    [InlineData("A(I, J+1)", "A[I, (Add J 1)]")]
    [InlineData("MAX ( A(I), B(J,K) )", "MAX[A[I], B[J, K]]")]
    [InlineData("S(1:3)", "S<1:3>")]
    [InlineData("S(:N)", "S<:N>")]
    [InlineData("S(I:)", "S<I:>")]
    [InlineData("S(:)", "S<:>")]
    [InlineData("NAMES(I)(2:LEN)", "NAMES[I]<2:LEN>")]
    [InlineData("NAMES(I)(:RTRIM(S))", "NAMES[I]<:RTRIM[S]>")]
    [InlineData("A(I) * B(J)(1:1)", "(Multiply A[I] B[J]<1:1>)")]
    public void Parse_WhenNameHasParentheses_ShouldReturnReferenceOrSubstring(string text, string expected)
    {
        // Act
        var expression = Parse(text);

        // Assert
        Render(expression).ShouldBe(expected);
    }

    [Fact]
    public void Parse_WhenExpressionEndsBeforeTheStatement_ShouldReportWhereItStopped()
    {
        // Arrange
        var statement = Lexer.Tokenize(new FixedFormStatement(null, "X = A + 1, N", 7, [], []));

        // Act
        var expression = ExpressionParser.Parse(statement, 2, out var next);

        // Assert
        Render(expression).ShouldBe("(Add A 1)");
        statement.Tokens[next].Kind.ShouldBe(TokenKind.Comma);
    }

    [Theory]
    [InlineData("")]
    [InlineData("A +")]
    [InlineData("* A")]
    [InlineData("( A")]
    [InlineData("A(I")]
    [InlineData("A(I,)")]
    [InlineData("A(I)(J)")]
    [InlineData("( 1.0, 2.0 )")]
    [InlineData("F( *10 )")]
    public void Parse_WhenTextIsMalformedOrUnsupported_ShouldThrowWithTheStatementLine(string text)
    {
        // Act & Assert
        var exception = Should.Throw<FortranSyntaxException>(() => Parse(text));
        exception.Line.ShouldBe(7);
    }

    private static Expression Parse(string text)
    {
        var statement = Lexer.Tokenize(new FixedFormStatement(null, text, 7, [], []));
        var expression = ExpressionParser.Parse(statement, 0, out var next);
        next.ShouldBe(statement.Tokens.Count);
        return expression;
    }
}
