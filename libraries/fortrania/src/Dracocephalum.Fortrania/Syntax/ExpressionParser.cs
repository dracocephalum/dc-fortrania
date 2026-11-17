namespace Dracocephalum.Fortrania.Syntax;

/// <summary>
/// Parses an expression out of a statement's tokens. Statement parsers call
/// it wherever the grammar has an expression: the two sides of an assignment,
/// a condition, the bounds of a loop, an argument.
/// </summary>
/// <remarks>
/// It accepts one thing the standard does not, a sign directly after an
/// operator as in <c>A ** -B</c>, and it refuses a complex constant and an
/// alternate-return argument, neither of which is supported yet.
/// </remarks>
public static class ExpressionParser
{
    // Binding powers, lowest first. A binary operator with powers (left,
    // right) continues an expression when its left power is at least the
    // minimum asked for, and parses its right operand with its right power as
    // the new minimum; right above left makes it associate to the left.
    private const int NotOperandPower = 7;
    private const int SignOperandPower = 15;

    /// <summary>Parses one expression starting at the token at index <paramref name="start"/>.</summary>
    /// <param name="statement">The statement whose tokens are read.</param>
    /// <param name="start">The index of the expression's first token.</param>
    /// <param name="next">The index of the first token after the expression.</param>
    /// <returns>The expression.</returns>
    /// <exception cref="FortranSyntaxException">No expression starts there, or it is malformed or unsupported.</exception>
    public static Expression Parse(TokenizedStatement statement, int start, out int next)
    {
        ArgumentNullException.ThrowIfNull(statement);
        ArgumentOutOfRangeException.ThrowIfNegative(start);

        var parser = new Parser(statement, start);
        var expression = parser.ParseExpression(0);
        next = parser.Position;
        return expression;
    }

    private static (int Left, int Right)? GetBindingPowers(TokenKind kind) => kind switch
    {
        TokenKind.Equivalent or TokenKind.NotEquivalent => (1, 2),
        TokenKind.Or => (3, 4),
        TokenKind.And => (5, 6),
        TokenKind.Equal or TokenKind.NotEqual or TokenKind.LessThan or TokenKind.LessOrEqual
            or TokenKind.GreaterThan or TokenKind.GreaterOrEqual => (9, 10),
        TokenKind.Concatenate => (11, 12),
        TokenKind.Plus or TokenKind.Minus => (13, 14),
        TokenKind.Star or TokenKind.Slash => (15, 16),

        // Exponentiation associates to the right: A ** B ** C is A ** (B ** C).
        TokenKind.Power => (18, 17),
        _ => null,
    };

    private static BinaryOperator ToBinaryOperator(TokenKind kind) => kind switch
    {
        TokenKind.Plus => BinaryOperator.Add,
        TokenKind.Minus => BinaryOperator.Subtract,
        TokenKind.Star => BinaryOperator.Multiply,
        TokenKind.Slash => BinaryOperator.Divide,
        TokenKind.Power => BinaryOperator.Power,
        TokenKind.Concatenate => BinaryOperator.Concatenate,
        TokenKind.Equal => BinaryOperator.Equal,
        TokenKind.NotEqual => BinaryOperator.NotEqual,
        TokenKind.LessThan => BinaryOperator.LessThan,
        TokenKind.LessOrEqual => BinaryOperator.LessOrEqual,
        TokenKind.GreaterThan => BinaryOperator.GreaterThan,
        TokenKind.GreaterOrEqual => BinaryOperator.GreaterOrEqual,
        TokenKind.And => BinaryOperator.And,
        TokenKind.Or => BinaryOperator.Or,
        TokenKind.Equivalent => BinaryOperator.Equivalent,
        TokenKind.NotEquivalent => BinaryOperator.NotEquivalent,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Not a binary operator."),
    };

    /// <summary>A position in one statement's tokens, advanced as the expression is read.</summary>
    private sealed class Parser(TokenizedStatement statement, int position)
    {
        public int Position { get; private set; } = position;

        private TokenKind? Current => Position < statement.Tokens.Count ? statement.Tokens[Position].Kind : null;

        public Expression ParseExpression(int minimumPower)
        {
            var left = ParseOperand(minimumPower);

            while (Current is { } kind && GetBindingPowers(kind) is var (leftPower, rightPower) && leftPower >= minimumPower)
            {
                Position++;
                var right = ParseExpression(rightPower);
                left = new Expression.Binary(ToBinaryOperator(kind), left, right);
            }

            return left;
        }

        // minimumPower is what the caller is parsing at: the right power of
        // the operator this operand follows, or zero at the start.
        private Expression ParseOperand(int minimumPower)
        {
            if (Current is not { } kind)
            {
                throw Fail("An expression is expected, and the statement ends.");
            }

            switch (kind)
            {
                case TokenKind.IntegerLiteral:
                    return ReadLiteral(DataType.Integer);
                case TokenKind.RealLiteral:
                    return ReadLiteral(DataType.Real);
                case TokenKind.DoubleLiteral:
                    return ReadLiteral(DataType.DoublePrecision);
                case TokenKind.CharacterLiteral:
                    return ReadLiteral(DataType.Character);
                case TokenKind.LogicalLiteral:
                    return ReadLiteral(DataType.Logical);

                // A sign takes a whole term as its operand, so -A*B is -(A*B)
                // and -A**2 is -(A**2). Directly after an operator, which is
                // an extension, it takes no more than that operator's own
                // right operand would: A / -B * C is (A / (-B)) * C, and
                // A ** -B * C is (A ** (-B)) * C, while A ** -B ** C is still
                // A ** (-(B ** C)). This is what gfortran 16.2 computes for
                // each of these.
                case TokenKind.Plus or TokenKind.Minus:
                    Position++;
                    return new Expression.Unary(
                        kind == TokenKind.Minus ? UnaryOperator.Minus : UnaryOperator.Plus,
                        ParseExpression(Math.Max(SignOperandPower, minimumPower)));

                // .NOT. binds tighter than .AND. and looser than a comparison.
                case TokenKind.Not:
                    Position++;
                    return new Expression.Unary(UnaryOperator.Not, ParseExpression(NotOperandPower));

                case TokenKind.LeftParenthesis:
                    return ParseParenthesized();
                case TokenKind.Name:
                    return ParseNamed();
                default:
                    throw Fail($"An expression is expected, and '{statement.GetText(statement.Tokens[Position])}' cannot start one.");
            }
        }

        private Expression.Literal ReadLiteral(DataType type) =>
            new(type, statement.GetText(statement.Tokens[Position++]).ToString());

        private Expression.Parenthesized ParseParenthesized()
        {
            Position++;
            var inner = ParseExpression(0);

            if (Current == TokenKind.Comma)
            {
                throw Fail("A complex constant is not supported yet.");
            }

            Expect(TokenKind.RightParenthesis, "A closing parenthesis is expected.");
            return new Expression.Parenthesized(inner);
        }

        // A name, optionally followed by a list in parentheses, optionally
        // followed by a substring range: NAME, A(I,J), S(1:3), A(I)(1:3).
        private Expression ParseNamed()
        {
            var identifier = statement.GetText(statement.Tokens[Position++]).ToString();
            if (Current != TokenKind.LeftParenthesis)
            {
                return new Expression.Name(identifier);
            }

            Position++;
            if (TryParseSubstringRange(null, out var first, out var last))
            {
                return new Expression.Substring(new Expression.Name(identifier), first, last);
            }

            var arguments = new List<Expression>();
            if (Current != TokenKind.RightParenthesis)
            {
                while (true)
                {
                    var argument = ParseExpression(0);

                    // A colon after the first item means the list was a
                    // substring range all along.
                    if (arguments.Count == 0 && TryParseSubstringRange(argument, out first, out last))
                    {
                        return new Expression.Substring(new Expression.Name(identifier), first, last);
                    }

                    arguments.Add(argument);
                    if (Current != TokenKind.Comma)
                    {
                        break;
                    }

                    Position++;
                }
            }

            Expect(TokenKind.RightParenthesis, "A closing parenthesis or a comma is expected.");
            var reference = new Expression.Reference(identifier, arguments);

            if (Current != TokenKind.LeftParenthesis)
            {
                return reference;
            }

            Position++;
            var start = Current == TokenKind.Colon ? null : ParseExpression(0);
            if (!TryParseSubstringRange(start, out first, out last))
            {
                throw Fail("A substring range is expected after an array element.");
            }

            return new Expression.Substring(reference, first, last);
        }

        // Called just inside an opening parenthesis, or after the first
        // expression there. When a colon is next, reads the rest of the range
        // and its closing parenthesis.
        private bool TryParseSubstringRange(Expression? start, out Expression? first, out Expression? last)
        {
            first = start;
            last = null;

            if (Current != TokenKind.Colon)
            {
                return false;
            }

            Position++;
            if (Current != TokenKind.RightParenthesis)
            {
                last = ParseExpression(0);
            }

            Expect(TokenKind.RightParenthesis, "A closing parenthesis is expected after a substring range.");
            return true;
        }

        private void Expect(TokenKind kind, string message)
        {
            if (Current != kind)
            {
                throw Fail(message);
            }

            Position++;
        }

        private FortranSyntaxException Fail(string message) => new(statement.Source.Line, message);
    }
}
