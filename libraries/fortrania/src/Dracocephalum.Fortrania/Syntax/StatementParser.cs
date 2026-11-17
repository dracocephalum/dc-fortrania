using System.Globalization;

namespace Dracocephalum.Fortrania.Syntax;

/// <summary>
/// Parses a classified statement into its parts. It covers every kind but
/// the input and output statements so far; a kind or a form it does not
/// handle is reported as unsupported, never skipped.
/// </summary>
public static class StatementParser
{
    private const string FunctionKeyword = "FUNCTION";

    // Longest first, so that DOUBLEPRECISION is not read as a name after DO.
    private static readonly (string Keyword, DataType Type)[] TypeKeywords =
    [
        ("DOUBLEPRECISION", DataType.DoublePrecision),
        ("CHARACTER", DataType.Character),
        ("INTEGER", DataType.Integer),
        ("LOGICAL", DataType.Logical),
        ("COMPLEX", DataType.Complex),
        ("REAL", DataType.Real),
    ];

    /// <summary>Parses <paramref name="statement"/> as a statement of the given kind.</summary>
    /// <param name="statement">A tokenized statement.</param>
    /// <param name="kind">Its kind, as <see cref="StatementClassifier"/> decided it.</param>
    /// <returns>The statement's parts.</returns>
    /// <exception cref="FortranSyntaxException">The statement is malformed, or its kind or a part of it is not supported yet.</exception>
    public static Statement Parse(TokenizedStatement statement, StatementKind kind)
    {
        ArgumentNullException.ThrowIfNull(statement);

        return kind switch
        {
            StatementKind.TypeDeclaration => ParseTypeDeclaration(statement),
            StatementKind.Dimension => new Statement.Dimension(ParseEntityList(SplitKeyword(statement, "DIMENSION"))),
            StatementKind.Parameter => ParseParameter(statement),
            StatementKind.Implicit => ParseImplicit(statement),
            StatementKind.Save => new Statement.Save(ParseNameList(SplitKeyword(statement, "SAVE"))),
            StatementKind.External => new Statement.External(ParseNameList(SplitKeyword(statement, "EXTERNAL"))),
            StatementKind.Intrinsic => new Statement.Intrinsic(ParseNameList(SplitKeyword(statement, "INTRINSIC"))),
            StatementKind.Include => ParseInclude(statement),
            StatementKind.Data => ParseData(statement),
            StatementKind.Equivalence => ParseEquivalence(statement),
            StatementKind.Assignment => ParseAssignment(statement),
            StatementKind.Call => ParseCall(statement),
            StatementKind.BlockIf => new Statement.BlockIf(ParseConditionThenWord(statement, "IF")),
            StatementKind.ElseIf => new Statement.ElseIf(ParseConditionThenWord(statement, "ELSEIF")),
            StatementKind.LogicalIf => ParseLogicalIf(statement),
            StatementKind.Do => ParseDo(statement),
            StatementKind.DoWhile => ParseDoWhile(statement),
            StatementKind.Stop => ParseStop(statement),
            StatementKind.Return => ParseReturn(statement),
            StatementKind.Else => ExpectAlone(statement, new Statement.Else()),
            StatementKind.EndIf => ExpectAlone(statement, new Statement.EndIf()),
            StatementKind.EndDo => ExpectAlone(statement, new Statement.EndDo()),
            StatementKind.Continue => ExpectAlone(statement, new Statement.Continue()),
            StatementKind.End => ExpectAlone(statement, new Statement.End()),
            StatementKind.Program => ParseProgram(statement),
            StatementKind.Subroutine => ParseSubroutine(statement),
            StatementKind.Function => ParseFunction(statement),
            StatementKind.Entry => ParseEntry(statement),
            StatementKind.BlockData => ParseBlockData(statement),
            _ => throw new FortranSyntaxException(statement.Source.Line, $"A {kind} statement is not supported yet."),
        };
    }

    // Returns a reader placed just after the keyword.
    private static Reader SplitKeyword(TokenizedStatement statement, string keyword) =>
        new(statement.SplitKeyword(keyword.Length), 1);

    private static Statement.TypeDeclaration ParseTypeDeclaration(TokenizedStatement statement)
    {
        var first = statement.GetText(statement.Tokens[0]);
        foreach (var (keyword, type) in TypeKeywords)
        {
            if (!first.StartsWith(keyword, StringComparison.Ordinal))
            {
                continue;
            }

            var reader = SplitKeyword(statement, keyword);
            var length = reader.TryParseLength();

            // "CHARACTER*80, A" is allowed a comma after the length.
            if (length is not null)
            {
                reader.TryRead(TokenKind.Comma);
            }

            return new Statement.TypeDeclaration(new TypeSpecification(type, length), ParseEntityList(reader));
        }

        throw new FortranSyntaxException(statement.Source.Line, $"'{first}' does not start with a supported type.");
    }

    private static List<DeclaredEntity> ParseEntityList(Reader reader)
    {
        var entities = new List<DeclaredEntity>();
        do
        {
            var name = reader.ReadName("A name to declare is expected.");
            var dimensions = reader.TryRead(TokenKind.LeftParenthesis) ? ParseDimensions(reader) : [];
            entities.Add(new DeclaredEntity(name, dimensions, reader.TryParseLength()));
        }
        while (reader.TryRead(TokenKind.Comma));

        reader.ExpectEnd();
        return entities;
    }

    // Called just inside the opening parenthesis of an array declarator.
    private static List<DimensionBound> ParseDimensions(Reader reader)
    {
        var bounds = new List<DimensionBound>();
        do
        {
            if (reader.TryRead(TokenKind.Star))
            {
                bounds.Add(new DimensionBound(null, null));
                continue;
            }

            var first = reader.ParseExpression();
            if (!reader.TryRead(TokenKind.Colon))
            {
                bounds.Add(new DimensionBound(null, first));
            }
            else if (reader.TryRead(TokenKind.Star))
            {
                bounds.Add(new DimensionBound(first, null));
            }
            else
            {
                bounds.Add(new DimensionBound(first, reader.ParseExpression()));
            }
        }
        while (reader.TryRead(TokenKind.Comma));

        reader.Expect(TokenKind.RightParenthesis, "A closing parenthesis is expected after array bounds.");
        return bounds;
    }

    private static Statement.Parameter ParseParameter(TokenizedStatement statement)
    {
        var reader = SplitKeyword(statement, "PARAMETER");
        reader.Expect(TokenKind.LeftParenthesis, "An opening parenthesis is expected after PARAMETER.");

        var constants = new List<NamedConstant>();
        do
        {
            var name = reader.ReadName("The name of a constant is expected.");
            reader.Expect(TokenKind.Assign, "An equals sign is expected after the name of a constant.");
            constants.Add(new NamedConstant(name, reader.ParseExpression()));
        }
        while (reader.TryRead(TokenKind.Comma));

        reader.Expect(TokenKind.RightParenthesis, "A closing parenthesis is expected after the constants.");
        reader.ExpectEnd();
        return new Statement.Parameter(constants);
    }

    private static Statement.ImplicitNone ParseImplicit(TokenizedStatement statement)
    {
        if (statement.Tokens.Count == 1 && statement.GetText(statement.Tokens[0]) is "IMPLICITNONE")
        {
            return new Statement.ImplicitNone();
        }

        throw new FortranSyntaxException(statement.Source.Line, "An IMPLICIT statement other than IMPLICIT NONE is not supported yet.");
    }

    // A possibly empty list of names separated by commas, to the end of the statement.
    private static List<string> ParseNameList(Reader reader)
    {
        var names = new List<string>();
        if (reader.IsAtEnd)
        {
            return names;
        }

        do
        {
            names.Add(reader.ReadName("A name is expected."));
        }
        while (reader.TryRead(TokenKind.Comma));

        reader.ExpectEnd();
        return names;
    }

    private static Statement.Data ParseData(TokenizedStatement statement)
    {
        var reader = SplitKeyword(statement, "DATA");
        var groups = new List<DataGroup>();

        do
        {
            var targets = new List<DataTarget>();
            do
            {
                targets.Add(ParseDataTarget(reader));
            }
            while (reader.TryRead(TokenKind.Comma));

            reader.Expect(TokenKind.Slash, "A slash is expected before the values of a DATA statement.");

            var values = new List<DataValue>();
            do
            {
                var first = reader.ParseConstant();
                values.Add(reader.TryRead(TokenKind.Star) ? new DataValue(first, reader.ParseConstant()) : new DataValue(null, first));
            }
            while (reader.TryRead(TokenKind.Comma));

            reader.Expect(TokenKind.Slash, "A slash is expected after the values of a DATA statement.");
            groups.Add(new DataGroup(targets, values));

            // The comma between one group and the next is optional.
            reader.TryRead(TokenKind.Comma);
        }
        while (!reader.IsAtEnd);

        return new Statement.Data(groups);
    }

    private static DataTarget ParseDataTarget(Reader reader)
    {
        if (!reader.TryRead(TokenKind.LeftParenthesis))
        {
            return new DataTarget.Variable(reader.ParseVariable());
        }

        // An implied DO: items, then "variable = first, last [, step]". The
        // control variable is the one name in the list followed by an equals
        // sign, which is the only way to tell where the items end.
        var targets = new List<DataTarget>();
        while (!reader.IsNameThenAssign)
        {
            targets.Add(ParseDataTarget(reader));
            reader.Expect(TokenKind.Comma, "A comma is expected after an item of an implied DO.");
        }

        var variable = reader.ReadName("The control variable of an implied DO is expected.");
        reader.Expect(TokenKind.Assign, "An equals sign is expected after the control variable.");
        var first = reader.ParseExpression();
        reader.Expect(TokenKind.Comma, "A comma is expected between the bounds of an implied DO.");
        var last = reader.ParseExpression();
        var step = reader.TryRead(TokenKind.Comma) ? reader.ParseExpression() : null;
        reader.Expect(TokenKind.RightParenthesis, "A closing parenthesis is expected after an implied DO.");

        if (targets.Count == 0)
        {
            throw reader.Fail("An implied DO has nothing to repeat.");
        }

        return new DataTarget.ImpliedDo(targets, variable, first, last, step);
    }

    private static Statement.Equivalence ParseEquivalence(TokenizedStatement statement)
    {
        var reader = SplitKeyword(statement, "EQUIVALENCE");
        var sets = new List<IReadOnlyList<Expression>>();

        do
        {
            reader.Expect(TokenKind.LeftParenthesis, "An opening parenthesis is expected before the names that share storage.");

            var set = new List<Expression>();
            do
            {
                set.Add(reader.ParseVariable());
            }
            while (reader.TryRead(TokenKind.Comma));

            reader.Expect(TokenKind.RightParenthesis, "A closing parenthesis is expected after the names that share storage.");
            if (set.Count < 2)
            {
                throw reader.Fail("At least two names are needed to share storage.");
            }

            sets.Add(set);
        }
        while (reader.TryRead(TokenKind.Comma));

        reader.ExpectEnd();
        return new Statement.Equivalence(sets);
    }

    private static Statement.Assignment ParseAssignment(TokenizedStatement statement)
    {
        var reader = new Reader(statement, 0);
        var target = reader.ParseVariable();
        reader.Expect(TokenKind.Assign, "An equals sign is expected after the target of an assignment.");
        var value = reader.ParseExpression();
        reader.ExpectEnd();
        return new Statement.Assignment(target, value);
    }

    private static Statement.Call ParseCall(TokenizedStatement statement)
    {
        var reader = SplitKeyword(statement, "CALL");

        // A subroutine name with its arguments is written like a function
        // reference, and without any like a plain name.
        var called = reader.ParseVariable();
        reader.ExpectEnd();

        return called switch
        {
            Expression.Name name => new Statement.Call(name.Identifier, []),
            Expression.Reference reference => new Statement.Call(reference.Identifier, reference.Arguments),
            _ => throw reader.Fail("A subroutine name and its arguments are expected after CALL."),
        };
    }

    // "IF ( condition ) THEN" and "ELSE IF ( condition ) THEN".
    private static Expression ParseConditionThenWord(TokenizedStatement statement, string keyword)
    {
        var reader = SplitKeyword(statement, keyword);
        var condition = reader.ParseCondition();

        if (reader.ReadName("THEN is expected after the condition.") is not "THEN")
        {
            throw reader.Fail("THEN is expected after the condition.");
        }

        reader.ExpectEnd();
        return condition;
    }

    private static Statement.LogicalIf ParseLogicalIf(TokenizedStatement statement)
    {
        var reader = SplitKeyword(statement, "IF");
        var condition = reader.ParseCondition();

        // What is left is a statement of its own.
        var rest = reader.TakeRest();
        var kind = StatementClassifier.Classify(rest, startsProgramUnit: false);

        if (kind is StatementKind.LogicalIf or StatementKind.BlockIf or StatementKind.ElseIf or StatementKind.Else
            or StatementKind.EndIf or StatementKind.Do or StatementKind.DoWhile or StatementKind.EndDo
            or StatementKind.End or StatementKind.Empty)
        {
            throw reader.Fail($"A logical IF cannot guard a {kind} statement.");
        }

        return new Statement.LogicalIf(condition, Parse(rest, kind));
    }

    private static Statement.Do ParseDo(TokenizedStatement statement)
    {
        var reader = SplitKeyword(statement, "DO");
        var label = reader.TryReadLabel();
        var variable = reader.ReadName("The loop variable is expected.");
        reader.Expect(TokenKind.Assign, "An equals sign is expected after the loop variable.");
        var first = reader.ParseExpression();
        reader.Expect(TokenKind.Comma, "A comma is expected between the bounds of a DO.");
        var last = reader.ParseExpression();
        var step = reader.TryRead(TokenKind.Comma) ? reader.ParseExpression() : null;
        reader.ExpectEnd();
        return new Statement.Do(label, variable, first, last, step);
    }

    private static Statement.DoWhile ParseDoWhile(TokenizedStatement statement)
    {
        var reader = SplitKeyword(statement, "DO");
        var label = reader.TryReadLabel();

        if (reader.ReadName("WHILE is expected.") is not "WHILE")
        {
            throw reader.Fail("WHILE is expected.");
        }

        var condition = reader.ParseCondition();
        reader.ExpectEnd();
        return new Statement.DoWhile(label, condition);
    }

    private static Statement.Stop ParseStop(TokenizedStatement statement)
    {
        var reader = SplitKeyword(statement, "STOP");
        var code = reader.IsAtEnd ? null : reader.ParseConstant();
        reader.ExpectEnd();
        return new Statement.Stop(code);
    }

    private static Statement.Return ParseReturn(TokenizedStatement statement)
    {
        if (statement.Tokens.Count != 1 || statement.GetText(statement.Tokens[0]) is not "RETURN")
        {
            throw new FortranSyntaxException(statement.Source.Line, "An alternate RETURN is not supported yet.");
        }

        return new Statement.Return();
    }

    // For a statement that is one word and nothing else.
    private static T ExpectAlone<T>(TokenizedStatement statement, T parsed)
        where T : Statement
    {
        if (statement.Tokens.Count != 1)
        {
            throw new FortranSyntaxException(statement.Source.Line, $"'{statement.GetText(statement.Tokens[1])}' is not expected here.");
        }

        return parsed;
    }

    private static Statement.Program ParseProgram(TokenizedStatement statement)
    {
        var reader = SplitKeyword(statement, "PROGRAM");
        var name = reader.ReadName("The name of the program is expected.");
        reader.ExpectEnd();
        return new Statement.Program(name);
    }

    private static Statement.BlockData ParseBlockData(TokenizedStatement statement)
    {
        var reader = SplitKeyword(statement, "BLOCKDATA");
        var name = reader.IsAtEnd ? null : reader.ReadName("The name of the block data subprogram is expected.");
        reader.ExpectEnd();
        return new Statement.BlockData(name);
    }

    private static Statement.Function ParseFunction(TokenizedStatement statement)
    {
        var reader = new Reader(statement, 0);
        TypeSpecification? returnType = null;

        // An untyped function starts with the keyword itself; otherwise a
        // type, and perhaps a length, come first.
        if (!reader.TryReadKeyword(FunctionKeyword))
        {
            foreach (var (keyword, type) in TypeKeywords)
            {
                if (reader.TryReadKeyword(keyword))
                {
                    returnType = new TypeSpecification(type, reader.TryParseLength());
                    break;
                }
            }

            if (returnType is null || !reader.TryReadKeyword(FunctionKeyword))
            {
                throw reader.Fail("FUNCTION is expected, after an optional type.");
            }
        }

        var name = reader.ReadName("The name of the function is expected.");
        return new Statement.Function(returnType, name, ParseParameters(reader));
    }

    // A dummy argument list to the end of the statement; a procedure with no
    // arguments may leave the parentheses out.
    private static List<string> ParseParameters(Reader reader)
    {
        var parameters = new List<string>();
        if (reader.TryRead(TokenKind.LeftParenthesis) && !reader.TryRead(TokenKind.RightParenthesis))
        {
            do
            {
                if (reader.TryRead(TokenKind.Star))
                {
                    throw reader.Fail("An alternate-return dummy argument is not supported yet.");
                }

                parameters.Add(reader.ReadName("The name of a dummy argument is expected."));
            }
            while (reader.TryRead(TokenKind.Comma));

            reader.Expect(TokenKind.RightParenthesis, "A closing parenthesis is expected after the dummy arguments.");
        }

        reader.ExpectEnd();
        return parameters;
    }

    private static (string Name, List<string> Parameters) ParseProcedureHeader(TokenizedStatement statement, string keyword)
    {
        var reader = SplitKeyword(statement, keyword);
        var name = reader.ReadName($"A name is expected after {keyword}.");
        return (name, ParseParameters(reader));
    }

    private static Statement.Subroutine ParseSubroutine(TokenizedStatement statement)
    {
        var (name, parameters) = ParseProcedureHeader(statement, "SUBROUTINE");
        return new Statement.Subroutine(name, parameters);
    }

    private static Statement.Entry ParseEntry(TokenizedStatement statement)
    {
        var (name, parameters) = ParseProcedureHeader(statement, "ENTRY");
        return new Statement.Entry(name, parameters);
    }

    private static Statement.Include ParseInclude(TokenizedStatement statement)
    {
        var tokens = statement.Tokens;
        if (tokens.Count != 2 || tokens[1].Kind != TokenKind.CharacterLiteral || statement.GetText(tokens[0]) is not "INCLUDE")
        {
            throw new FortranSyntaxException(statement.Source.Line, "INCLUDE is followed by one file name in quotes.");
        }

        var quoted = statement.GetText(tokens[1]);
        var delimiter = quoted[0].ToString();
        var fileName = quoted[1..^1].ToString().Replace(delimiter + delimiter, delimiter, StringComparison.Ordinal);
        return new Statement.Include(fileName);
    }

    /// <summary>A position in one statement's tokens, advanced as the statement is read.</summary>
    private sealed class Reader(TokenizedStatement statement, int position)
    {
        public bool IsAtEnd => position >= statement.Tokens.Count;

        private TokenKind? Current => IsAtEnd ? null : statement.Tokens[position].Kind;

        public bool TryRead(TokenKind kind)
        {
            if (Current != kind)
            {
                return false;
            }

            position++;
            return true;
        }

        // Reads a keyword that the lexer left joined to what follows it, by
        // splitting it off the name that is next.
        public bool TryReadKeyword(string keyword)
        {
            if (Current != TokenKind.Name || !statement.GetText(statement.Tokens[position]).StartsWith(keyword, StringComparison.Ordinal))
            {
                return false;
            }

            statement = statement.SplitKeyword(position, keyword.Length);
            position++;
            return true;
        }

        public void Expect(TokenKind kind, string message)
        {
            if (!TryRead(kind))
            {
                throw Fail(message);
            }
        }

        public void ExpectEnd()
        {
            if (!IsAtEnd)
            {
                throw Fail($"'{statement.GetText(statement.Tokens[position])}' is not expected here.");
            }
        }

        public string ReadName(string message)
        {
            if (Current != TokenKind.Name)
            {
                throw Fail(message);
            }

            return statement.GetText(statement.Tokens[position++]).ToString();
        }

        // True when a name is next and an equals sign follows it.
        public bool IsNameThenAssign =>
            Current == TokenKind.Name && position + 1 < statement.Tokens.Count && statement.Tokens[position + 1].Kind == TokenKind.Assign;

        public Expression ParseExpression() => ExpressionParser.Parse(statement, position, out position);

        public Expression ParseVariable() => ExpressionParser.ParseVariable(statement, position, out position);

        // A condition in its parentheses, as IF and DO WHILE write it.
        public Expression ParseCondition()
        {
            Expect(TokenKind.LeftParenthesis, "An opening parenthesis is expected before the condition.");
            var condition = ParseExpression();
            Expect(TokenKind.RightParenthesis, "A closing parenthesis is expected after the condition.");
            return condition;
        }

        // The label a DO may name as its last statement, with the comma that may follow it.
        public int? TryReadLabel()
        {
            if (Current != TokenKind.IntegerLiteral)
            {
                return null;
            }

            var label = int.Parse(statement.GetText(statement.Tokens[position++]), CultureInfo.InvariantCulture);
            TryRead(TokenKind.Comma);
            return label;
        }

        // The tokens not yet read, as a statement of their own.
        public TokenizedStatement TakeRest()
        {
            var rest = statement with { Tokens = [.. statement.Tokens.Skip(position)] };
            position = statement.Tokens.Count;
            return rest;
        }

        // A constant as a DATA statement allows it: a literal or a named
        // constant, with an optional sign. The expression parser cannot be
        // used, because the slash and the asterisk around a value are
        // punctuation here and not operators.
        public Expression ParseConstant()
        {
            if (Current is TokenKind.Plus or TokenKind.Minus)
            {
                var sign = Current == TokenKind.Minus ? UnaryOperator.Minus : UnaryOperator.Plus;
                position++;
                return new Expression.Unary(sign, ParseUnsignedConstant());
            }

            return ParseUnsignedConstant();
        }

        // Reads "*80", "*(N)" or "*(*)" when an asterisk is next.
        public LengthSpecification? TryParseLength()
        {
            if (!TryRead(TokenKind.Star))
            {
                return null;
            }

            switch (Current)
            {
                case TokenKind.IntegerLiteral:
                    return new LengthSpecification(ParseLiteral());

                // "REAL*8 E1" lexes its length and its first name as one number.
                case TokenKind.RealLiteral or TokenKind.DoubleLiteral:
                    throw Fail("A name starting with E or D directly after a length is not supported yet.");

                case TokenKind.LeftParenthesis:
                    position++;
                    var value = TryRead(TokenKind.Star) ? null : ParseExpression();
                    Expect(TokenKind.RightParenthesis, "A closing parenthesis is expected after a length.");
                    return new LengthSpecification(value);

                default:
                    throw Fail("A length is expected after the asterisk.");
            }
        }

        // An unparenthesized length is a bare constant: in "A*4(3)" the bounds
        // that follow belong to the declarator, not to a function reference.
        private Expression.Literal ParseLiteral() =>
            new(DataType.Integer, statement.GetText(statement.Tokens[position++]).ToString());

        public FortranSyntaxException Fail(string message) => new(statement.Source.Line, message);

        private Expression ParseUnsignedConstant()
        {
            DataType? type = Current switch
            {
                TokenKind.IntegerLiteral => DataType.Integer,
                TokenKind.RealLiteral => DataType.Real,
                TokenKind.DoubleLiteral => DataType.DoublePrecision,
                TokenKind.CharacterLiteral => DataType.Character,
                TokenKind.LogicalLiteral => DataType.Logical,
                _ => null,
            };

            if (type is not null)
            {
                return new Expression.Literal(type.Value, statement.GetText(statement.Tokens[position++]).ToString());
            }

            if (Current == TokenKind.Name)
            {
                return new Expression.Name(ReadName("A constant is expected."));
            }

            throw Fail("A constant is expected in the values of a DATA statement.");
        }
    }
}
