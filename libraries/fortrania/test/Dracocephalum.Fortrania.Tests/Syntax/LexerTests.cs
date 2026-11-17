using Dracocephalum.Fortrania.Syntax;
using Shouldly;

namespace Dracocephalum.Fortrania.Tests.Syntax;

public class LexerTests
{
    [Fact]
    public void Tokenize_WhenStatementHasBlanksAndLowerCase_ShouldNormalizeTheText()
    {
        // Act
        var statement = Tokenize("call vadd ( v1, V2, vout )");

        // Assert
        statement.Text.ShouldBe("CALLVADD(V1,V2,VOUT)");
    }

    [Fact]
    public void Tokenize_WhenKeywordRunsIntoName_ShouldLeaveThemAsOneName()
    {
        // Act
        var statement = Tokenize("DO 10 I = 1, 5");

        // Assert
        Describe(statement).ShouldBe(
        [
            "Name:DO10I", "Assign:=", "IntegerLiteral:1", "Comma:,", "IntegerLiteral:5",
        ]);
    }

    [Theory]
    [InlineData("1", "IntegerLiteral:1")]
    [InlineData("1 000", "IntegerLiteral:1000")]
    [InlineData("1.", "RealLiteral:1.")]
    [InlineData(".5", "RealLiteral:.5")]
    [InlineData("1.5", "RealLiteral:1.5")]
    [InlineData("1E5", "RealLiteral:1E5")]
    [InlineData("1.E-5", "RealLiteral:1.E-5")]
    [InlineData("1.5e+10", "RealLiteral:1.5E+10")]
    [InlineData("1D0", "DoubleLiteral:1D0")]
    [InlineData("1.D0", "DoubleLiteral:1.D0")]
    [InlineData("0.25d-3", "DoubleLiteral:0.25D-3")]
    public void Tokenize_WhenTextIsNumber_ShouldReturnOneLiteral(string text, string expected)
    {
        // Act
        var statement = Tokenize(text);

        // Assert
        Describe(statement).ShouldBe([expected]);
    }

    [Theory]
    [InlineData("1.EQ.2", "IntegerLiteral:1", "Equal:.EQ.", "IntegerLiteral:2")]
    [InlineData("1..EQ.2.", "RealLiteral:1.", "Equal:.EQ.", "RealLiteral:2.")]
    [InlineData("1.E2.LT.X", "RealLiteral:1.E2", "LessThan:.LT.", "Name:X")]
    [InlineData("X.GT..5", "Name:X", "GreaterThan:.GT.", "RealLiteral:.5")]
    [InlineData("8E", "IntegerLiteral:8", "Name:E")]
    public void Tokenize_WhenPeriodCouldStartOperatorOrFraction_ShouldChooseByWhatFollows(string text, params string[] expected)
    {
        // Act
        var statement = Tokenize(text);

        // Assert
        Describe(statement).ShouldBe(expected);
    }

    [Theory]
    [InlineData(".TRUE.", TokenKind.LogicalLiteral)]
    [InlineData(".false.", TokenKind.LogicalLiteral)]
    [InlineData(".EQ.", TokenKind.Equal)]
    [InlineData(".NE.", TokenKind.NotEqual)]
    [InlineData(".LT.", TokenKind.LessThan)]
    [InlineData(".LE.", TokenKind.LessOrEqual)]
    [InlineData(".GT.", TokenKind.GreaterThan)]
    [InlineData(".GE.", TokenKind.GreaterOrEqual)]
    [InlineData(".AND.", TokenKind.And)]
    [InlineData(". OR .", TokenKind.Or)]
    [InlineData(".NOT.", TokenKind.Not)]
    [InlineData(".EQV.", TokenKind.Equivalent)]
    [InlineData(".NEQV.", TokenKind.NotEquivalent)]
    [InlineData("+", TokenKind.Plus)]
    [InlineData("-", TokenKind.Minus)]
    [InlineData("*", TokenKind.Star)]
    [InlineData("/", TokenKind.Slash)]
    [InlineData("**", TokenKind.Power)]
    [InlineData("//", TokenKind.Concatenate)]
    [InlineData("(", TokenKind.LeftParenthesis)]
    [InlineData(")", TokenKind.RightParenthesis)]
    [InlineData(",", TokenKind.Comma)]
    [InlineData("=", TokenKind.Assign)]
    [InlineData(":", TokenKind.Colon)]
    [InlineData("==", TokenKind.Equal)]
    [InlineData("/=", TokenKind.NotEqual)]
    [InlineData("<", TokenKind.LessThan)]
    [InlineData("<=", TokenKind.LessOrEqual)]
    [InlineData(">", TokenKind.GreaterThan)]
    [InlineData(">=", TokenKind.GreaterOrEqual)]
    public void Tokenize_WhenTextIsOperatorOrPunctuation_ShouldReturnItsKind(string text, TokenKind expected)
    {
        // Act
        var statement = Tokenize(text);

        // Assert
        statement.Tokens.ShouldHaveSingleItem().Kind.ShouldBe(expected);
    }

    [Theory]
    [InlineData("'It''s  here'")]
    [InlineData("\"say 'hi'\"")]
    [InlineData("''")]
    public void Tokenize_WhenTextIsCharacterConstant_ShouldKeepItAsWritten(string text)
    {
        // Act
        var statement = Tokenize(text);

        // Assert
        var token = statement.Tokens.ShouldHaveSingleItem();
        token.Kind.ShouldBe(TokenKind.CharacterLiteral);
        statement.GetText(token).ToString().ShouldBe(text);
    }

    [Fact]
    public void Tokenize_WhenStatementMixesEverything_ShouldReturnTokensInOrder()
    {
        // Act
        var statement = Tokenize("IF ( A(I) .GE. 1.0D0 .AND. NAME(1:2) // 'x' .NE. S ) Y = -X ** 2");

        // Assert
        Describe(statement).ShouldBe(
        [
            "Name:IF", "LeftParenthesis:(", "Name:A", "LeftParenthesis:(", "Name:I", "RightParenthesis:)",
            "GreaterOrEqual:.GE.", "DoubleLiteral:1.0D0", "And:.AND.", "Name:NAME", "LeftParenthesis:(",
            "IntegerLiteral:1", "Colon::", "IntegerLiteral:2", "RightParenthesis:)", "Concatenate://",
            "CharacterLiteral:'x'", "NotEqual:.NE.", "Name:S", "RightParenthesis:)", "Name:Y", "Assign:=",
            "Minus:-", "Name:X", "Power:**", "IntegerLiteral:2",
        ]);
    }

    [Fact]
    public void Tokenize_WhenStatementIsEmpty_ShouldReturnNoTokens()
    {
        // Act
        var statement = Tokenize(string.Empty);

        // Assert
        statement.Tokens.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("X = 'open")]
    [InlineData("X = #")]
    [InlineData("X .FOO. Y")]
    [InlineData("X = .")]
    public void Tokenize_WhenTextIsNotTokens_ShouldThrowWithTheStatementLine(string text)
    {
        // Act & Assert
        var exception = Should.Throw<FortranSyntaxException>(() => Tokenize(text));
        exception.Line.ShouldBe(7);
    }

    private static TokenizedStatement Tokenize(string text) =>
        Lexer.Tokenize(new FixedFormStatement(null, text, 7, [], []));

    private static string[] Describe(TokenizedStatement statement) =>
        [.. statement.Tokens.Select(token => $"{token.Kind}:{statement.GetText(token)}")];
}
