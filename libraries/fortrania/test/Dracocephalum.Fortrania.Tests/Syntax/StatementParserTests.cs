using Dracocephalum.Fortrania.Syntax;
using Shouldly;

namespace Dracocephalum.Fortrania.Tests.Syntax;

public class StatementParserTests
{
    [Theory]
    [InlineData("INTEGER I", "Integer: I")]
    [InlineData("INTEGER I, J, K", "Integer: I, J, K")]
    [InlineData("DOUBLE PRECISION STATE ( 6 )", "DoublePrecision: STATE(6)")]
    [InlineData("DOUBLE PRECISION XFORM ( 6, 6 ), LT", "DoublePrecision: XFORM(6, 6), LT")]
    [InlineData("INTEGER CELL ( LBCELL:* )", "Integer: CELL(LBCELL:*)")]
    [InlineData("REAL A ( * ), B ( 0:N-1, 2 )", "Real: A(*), B(0:(Subtract N 1), 2)")]
    [InlineData("LOGICAL FIRST", "Logical: FIRST")]
    [InlineData("COMPLEX Z", "Complex: Z")]
    [InlineData("REAL*8 X", "Real*8: X")]
    [InlineData("INTEGERI", "Integer: I")]
    public void Parse_WhenStatementDeclaresNumbers_ShouldReturnTypeAndEntities(string text, string expected)
    {
        // Act
        var statement = Parse(text);

        // Assert
        Render(statement).ShouldBe(expected);
    }

    [Theory]
    [InlineData("CHARACTER C", "Character: C")]
    [InlineData("CHARACTER*80 LINE", "Character*80: LINE")]
    [InlineData("CHARACTER*(*) NAME", "Character*(*): NAME")]
    [InlineData("CHARACTER*( LNSIZE ) LINE", "Character*LNSIZE: LINE")]
    [InlineData("CHARACTER*( N + 1 ) A, B", "Character*(Add N 1): A, B")]
    [InlineData("CHARACTER*80, A", "Character*80: A")]
    [InlineData("CHARACTER A*8, B ( 3 )*( * ), C", "Character: A*8, B(3)*(*), C")]
    [InlineData("CHARACTER*32 NAMES ( MAXN )", "Character*32: NAMES(MAXN)")]
    public void Parse_WhenStatementDeclaresCharacters_ShouldReturnLengths(string text, string expected)
    {
        // Act
        var statement = Parse(text);

        // Assert
        Render(statement).ShouldBe(expected);
    }

    [Theory]
    [InlineData("PARAMETER ( N = 3 )", "PARAMETER N=3")]
    [InlineData("PARAMETER ( N = 3, M = N * 2 )", "PARAMETER N=3, M=(Multiply N 2)")]
    [InlineData("PARAMETER ( RNAME = 'SPKEZ' )", "PARAMETER RNAME='SPKEZ'")]
    [InlineData("DIMENSION A ( 10 ), B ( 2, * )", "DIMENSION A(10), B(2, *)")]
    [InlineData("IMPLICIT NONE", "IMPLICIT NONE")]
    [InlineData("SAVE", "SAVE")]
    [InlineData("SAVE FIRST, PASS1", "SAVE FIRST, PASS1")]
    [InlineData("EXTERNAL UDFUNC, UDSTEP", "EXTERNAL UDFUNC, UDSTEP")]
    [InlineData("INTRINSIC MAX", "INTRINSIC MAX")]
    [InlineData("INCLUDE 'zzctr.inc'", "INCLUDE zzctr.inc")]
    [InlineData("INCLUDE 'it''s.inc'", "INCLUDE it's.inc")]
    public void Parse_WhenStatementIsSpecification_ShouldReturnItsParts(string text, string expected)
    {
        // Act
        var statement = Parse(text);

        // Assert
        Render(statement).ShouldBe(expected);
    }

    [Theory]
    [InlineData("DATA FIRST / .TRUE. /", "DATA FIRST / .TRUE. /")]
    [InlineData("DATA A, B / 1, 2 /", "DATA A, B / 1, 2 /")]
    [InlineData("DATA X / -1.0D0 /, Y / +2 /", "DATA X / (Minus 1.0D0) /; Y / (Plus 2) /")]
    [InlineData("DATA X / 1 / Y / 2 /", "DATA X / 1 /; Y / 2 /")]
    [InlineData("DATA V / 3 * 0.0D0 /", "DATA V / 3*0.0D0 /")]
    [InlineData("DATA V / N * -1 /", "DATA V / N*(Minus 1) /")]
    [InlineData("DATA NAMES / 'A/B', 'C' /", "DATA NAMES / 'A/B', 'C' /")]
    [InlineData("DATA A(1), S(2:3) / ZERO, 'ab' /", "DATA A[1], S<2:3> / ZERO, 'ab' /")]
    [InlineData("DATA ( A(I), I = 1, 3 ) / 1, 2, 3 /", "DATA (A[I], I=1..3) / 1, 2, 3 /")]
    [InlineData("DATA ( A(I), B(I), I = 1, N, 2 ) / 4 * 0 /", "DATA (A[I], B[I], I=1..N step 2) / 4*0 /")]
    [InlineData("DATA ( ( M(I,J), I = 1, 2 ), J = 1, 2 ) / 4 * 1 /", "DATA ((M[I, J], I=1..2), J=1..2) / 4*1 /")]
    [InlineData("EQUIVALENCE ( A, B )", "EQUIVALENCE A = B")]
    [InlineData("EQUIVALENCE ( A(1), B, C ), ( S(1:1), T )", "EQUIVALENCE A[1] = B = C; S<1:1> = T")]
    public void Parse_WhenStatementIsDataOrEquivalence_ShouldReturnItsParts(string text, string expected)
    {
        // Act
        var statement = Parse(text);

        // Assert
        Render(statement).ShouldBe(expected);
    }

    [Theory]
    [InlineData("DATA X")]
    [InlineData("DATA X / 1")]
    [InlineData("DATA X / /")]
    [InlineData("DATA / 1 /")]
    [InlineData("DATA X / 1 + 2 /")]
    [InlineData("DATA X / ( 1.0, 2.0 ) /")]
    [InlineData("DATA ( I = 1, 3 ) / 3 * 0 /")]
    [InlineData("DATA ( A(I), I = 1 ) / 1 /")]
    [InlineData("EQUIVALENCE ( A )")]
    [InlineData("EQUIVALENCE A, B")]
    [InlineData("INTEGER")]
    [InlineData("INTEGER I,")]
    [InlineData("INTEGER A ( 3")]
    [InlineData("INTEGER X / 5 /")]
    [InlineData("REAL*8 E1")]
    [InlineData("CHARACTER* NAME")]
    [InlineData("PARAMETER N = 3")]
    [InlineData("PARAMETER ( N 3 )")]
    [InlineData("IMPLICIT REAL ( A-H )")]
    [InlineData("SAVE / BLOCK /")]
    [InlineData("INCLUDE ZZCTR")]
    public void Parse_WhenStatementIsMalformedOrUnsupported_ShouldThrowWithTheStatementLine(string text)
    {
        // Act & Assert
        var exception = Should.Throw<FortranSyntaxException>(() => Parse(text));
        exception.Line.ShouldBe(7);
    }

    [Fact]
    public void Parse_WhenKindHasNoParserYet_ShouldThrow()
    {
        // Arrange
        var statement = Lexer.Tokenize(new FixedFormStatement(null, "GO TO 10", 7, [], []));

        // Act & Assert
        Should.Throw<FortranSyntaxException>(() => StatementParser.Parse(statement, StatementKind.GoTo));
    }

    private static Statement Parse(string text)
    {
        var statement = Lexer.Tokenize(new FixedFormStatement(null, text, 7, [], []));
        return StatementParser.Parse(statement, StatementClassifier.Classify(statement, startsProgramUnit: false));
    }

    private static string Render(Statement statement) => statement switch
    {
        Statement.TypeDeclaration declaration =>
            $"{declaration.Type.Type}{Render(declaration.Type.Length)}: {string.Join(", ", declaration.Entities.Select(Render))}",
        Statement.Dimension dimension => $"DIMENSION {string.Join(", ", dimension.Entities.Select(Render))}",
        Statement.Parameter parameter =>
            $"PARAMETER {string.Join(", ", parameter.Constants.Select(constant => $"{constant.Name}={Render(constant.Value)}"))}",
        Statement.ImplicitNone => "IMPLICIT NONE",
        Statement.Save save => string.Join(' ', new[] { "SAVE", string.Join(", ", save.Names) }).TrimEnd(),
        Statement.External external => $"EXTERNAL {string.Join(", ", external.Names)}",
        Statement.Intrinsic intrinsic => $"INTRINSIC {string.Join(", ", intrinsic.Names)}",
        Statement.Include include => $"INCLUDE {include.FileName}",
        Statement.Data data => $"DATA {string.Join("; ", data.Groups.Select(Render))}",
        Statement.Equivalence equivalence =>
            $"EQUIVALENCE {string.Join("; ", equivalence.Sets.Select(set => string.Join(" = ", set.Select(Render))))}",
        _ => throw new ArgumentOutOfRangeException(nameof(statement)),
    };

    private static string Render(DeclaredEntity entity)
    {
        var dimensions = entity.Dimensions.Count == 0 ? string.Empty : $"({string.Join(", ", entity.Dimensions.Select(Render))})";
        return $"{entity.Name}{dimensions}{Render(entity.Length)}";
    }

    private static string Render(DimensionBound bound)
    {
        var upper = bound.Upper is null ? "*" : Render(bound.Upper);
        return bound.Lower is null ? upper : $"{Render(bound.Lower)}:{upper}";
    }

    private static string Render(LengthSpecification? length) => length switch
    {
        null => string.Empty,
        { IsAssumed: true } => "*(*)",
        _ => $"*{Render(length.Value)}",
    };

    private static string Render(Expression? expression) => expression switch
    {
        null => string.Empty,
        Expression.Literal literal => literal.Text,
        Expression.Name name => name.Identifier,
        Expression.Binary binary => $"({binary.Operator} {Render(binary.Left)} {Render(binary.Right)})",
        Expression.Unary unary => $"({unary.Operator} {Render(unary.Operand)})",
        Expression.Reference reference => $"{reference.Identifier}[{string.Join(", ", reference.Arguments.Select(Render))}]",
        Expression.Substring substring => $"{Render(substring.Target)}<{Render(substring.First)}:{Render(substring.Last)}>",
        _ => throw new ArgumentOutOfRangeException(nameof(expression)),
    };

    private static string Render(DataGroup group)
    {
        var values = group.Values.Select(value => value.Repeat is null ? Render(value.Value) : $"{Render(value.Repeat)}*{Render(value.Value)}");
        return $"{string.Join(", ", group.Targets.Select(Render))} / {string.Join(", ", values)} /";
    }

    private static string Render(DataTarget target) => target switch
    {
        DataTarget.Variable variable => Render(variable.Designator),
        DataTarget.ImpliedDo loop =>
            $"({string.Join(", ", loop.Targets.Select(Render))}, {loop.ControlVariable}={Render(loop.First)}..{Render(loop.Last)}{(loop.Step is null ? string.Empty : $" step {Render(loop.Step)}")})",
        _ => throw new ArgumentOutOfRangeException(nameof(target)),
    };
}
