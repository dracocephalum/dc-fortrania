namespace Dracocephalum.Fortrania.Syntax;

/// <summary>
/// Decides what kind of statement a tokenized statement is, which the parser
/// has to know before it can split any keyword off a name.
/// </summary>
/// <remarks>
/// It reads the statement's shape only and never its meaning: an assignment
/// and a statement function definition are both
/// <see cref="StatementKind.Assignment"/>, and telling them apart is left to
/// whoever knows what has been declared.
/// </remarks>
public static class StatementClassifier
{
    private const string FunctionKeyword = "FUNCTION";

    // The words a type statement can start with. DOUBLEPRECISION comes before
    // any shorter word it could be confused with; none of the rest is a prefix
    // of another.
    private static readonly string[] TypeKeywords =
    [
        "DOUBLEPRECISION", "DOUBLECOMPLEX", "INTEGER", "REAL", "COMPLEX", "LOGICAL", "CHARACTER",
    ];

    // Keywords recognized by prefix alone, tried in order. No entry is a
    // prefix of a later one, so the order matters only for reading.
    private static readonly (string Keyword, StatementKind Kind)[] PrefixKeywords =
    [
        ("BLOCKDATA", StatementKind.BlockData),
        ("IMPLICIT", StatementKind.Implicit),
        ("PARAMETER", StatementKind.Parameter),
        ("DIMENSION", StatementKind.Dimension),
        ("COMMON", StatementKind.Common),
        ("EQUIVALENCE", StatementKind.Equivalence),
        ("EXTERNAL", StatementKind.External),
        ("INTRINSIC", StatementKind.Intrinsic),
        ("SAVE", StatementKind.Save),
        ("DATA", StatementKind.Data),
        ("INCLUDE", StatementKind.Include),
        ("PROGRAM", StatementKind.Program),
        ("SUBROUTINE", StatementKind.Subroutine),
        (FunctionKeyword, StatementKind.Function),
        ("ENTRY", StatementKind.Entry),
        ("CALL", StatementKind.Call),
        ("RETURN", StatementKind.Return),
        ("STOP", StatementKind.Stop),
        ("PAUSE", StatementKind.Pause),
        ("GOTO", StatementKind.GoTo),
        ("ASSIGN", StatementKind.AssignLabel),
        ("ENDFILE", StatementKind.EndFile),
        ("READ", StatementKind.Read),
        ("WRITE", StatementKind.Write),
        ("PRINT", StatementKind.Print),
        ("OPEN", StatementKind.Open),
        ("CLOSE", StatementKind.Close),
        ("INQUIRE", StatementKind.Inquire),
        ("REWIND", StatementKind.Rewind),
        ("BACKSPACE", StatementKind.Backspace),
        ("FORMAT", StatementKind.Format),
    ];

    /// <summary>Decides what kind of statement <paramref name="statement"/> is.</summary>
    /// <param name="statement">A tokenized statement.</param>
    /// <param name="startsProgramUnit">
    /// Whether this is the first statement of a program unit. Only there can
    /// <c>INTEGER FUNCTION F(X)</c> be a function statement; anywhere else the
    /// same text declares an array.
    /// </param>
    /// <returns>The kind of the statement.</returns>
    /// <exception cref="FortranSyntaxException">The statement is neither an assignment nor starts with a known keyword.</exception>
    public static StatementKind Classify(TokenizedStatement statement, bool startsProgramUnit)
    {
        ArgumentNullException.ThrowIfNull(statement);

        var tokens = statement.Tokens;
        if (tokens.Count == 0)
        {
            return StatementKind.Empty;
        }

        // An equals sign outside every parenthesis settles most statements:
        // only an assignment, a DO, and a logical IF that holds an assignment
        // can have one.
        var assign = FindAtDepthZero(tokens, TokenKind.Assign, 0);
        if (assign >= 0)
        {
            if (StartsWithIfCondition(statement, out var afterCondition) && tokens[afterCondition].Kind is not (TokenKind.Assign or TokenKind.LeftParenthesis))
            {
                return StatementKind.LogicalIf;
            }

            if (FindAtDepthZero(tokens, TokenKind.Comma, assign + 1) < 0)
            {
                return StatementKind.Assignment;
            }

            if (statement.GetText(tokens[0]).StartsWith("DO", StringComparison.Ordinal))
            {
                return StatementKind.Do;
            }

            throw new FortranSyntaxException(statement.Source.Line, "A comma follows an assignment, and the statement is not a DO.");
        }

        if (tokens[0].Kind != TokenKind.Name)
        {
            throw new FortranSyntaxException(statement.Source.Line, "The statement starts with neither a name nor a keyword.");
        }

        return ClassifyKeywordStatement(statement, startsProgramUnit);
    }

    private static StatementKind ClassifyKeywordStatement(TokenizedStatement statement, bool startsProgramUnit)
    {
        var tokens = statement.Tokens;
        var first = statement.GetText(tokens[0]);
        var second = tokens.Count > 1 ? tokens[1].Kind : (TokenKind?)null;

        switch (first)
        {
            case "END":
                return StatementKind.End;
            case "ENDIF":
                return StatementKind.EndIf;
            case "ENDDO":
                return StatementKind.EndDo;
            case "ELSE":
                return StatementKind.Else;
            case "CONTINUE":
                return StatementKind.Continue;
            case "ELSEIF" when second == TokenKind.LeftParenthesis:
                return StatementKind.ElseIf;
            case "IF" when StartsWithIfCondition(statement, out var afterCondition):
                return ClassifyIf(statement, afterCondition);
        }

        if (startsProgramUnit && IsTypedFunction(statement))
        {
            return StatementKind.Function;
        }

        foreach (var keyword in TypeKeywords)
        {
            if (first.StartsWith(keyword, StringComparison.Ordinal))
            {
                return StatementKind.TypeDeclaration;
            }
        }

        foreach (var (keyword, kind) in PrefixKeywords)
        {
            if (first.StartsWith(keyword, StringComparison.Ordinal))
            {
                return kind;
            }
        }

        // DO WHILE has no equals sign, so it arrives here; a label may sit
        // between the two words.
        if (first.StartsWith("DO", StringComparison.Ordinal) && first.EndsWith("WHILE", StringComparison.Ordinal) && second == TokenKind.LeftParenthesis)
        {
            return StatementKind.DoWhile;
        }

        throw new FortranSyntaxException(statement.Source.Line, $"'{first}' does not start any known statement.");
    }

    // What follows the condition of an IF decides which of the three it is.
    private static StatementKind ClassifyIf(TokenizedStatement statement, int afterCondition)
    {
        var tokens = statement.Tokens;
        var next = tokens[afterCondition];

        if (next.Kind == TokenKind.IntegerLiteral)
        {
            return StatementKind.ArithmeticIf;
        }

        var isThen = next.Kind == TokenKind.Name && afterCondition == tokens.Count - 1 && statement.GetText(next) is "THEN";
        return isThen ? StatementKind.BlockIf : StatementKind.LogicalIf;
    }

    // True for "IF ( ... ) something"; afterCondition is the index of the
    // first token past the closing parenthesis.
    private static bool StartsWithIfCondition(TokenizedStatement statement, out int afterCondition)
    {
        var tokens = statement.Tokens;
        afterCondition = -1;

        if (tokens.Count < 2 || tokens[0].Kind != TokenKind.Name || tokens[1].Kind != TokenKind.LeftParenthesis || statement.GetText(tokens[0]) is not "IF")
        {
            return false;
        }

        var close = FindClosingParenthesis(tokens, 1);
        if (close < 0 || close + 1 >= tokens.Count)
        {
            return false;
        }

        afterCondition = close + 1;
        return true;
    }

    // "type [*length] FUNCTION name ( arguments )" and nothing after it.
    private static bool IsTypedFunction(TokenizedStatement statement)
    {
        var tokens = statement.Tokens;
        var rest = statement.GetText(tokens[0]);
        var nameIndex = 0;

        var type = Array.Find(TypeKeywords, keyword => statement.GetText(tokens[0]).StartsWith(keyword, StringComparison.Ordinal));
        if (type is null)
        {
            return false;
        }

        rest = rest[type.Length..];

        // With a length, "REAL*8 FUNCTION F" lexes as REAL, *, 8, FUNCTIONF.
        if (rest.IsEmpty)
        {
            if (tokens.Count < 4 || tokens[1].Kind != TokenKind.Star)
            {
                return false;
            }

            nameIndex = tokens[2].Kind == TokenKind.LeftParenthesis ? FindClosingParenthesis(tokens, 2) + 1 : 3;
            if (nameIndex <= 0 || nameIndex >= tokens.Count || tokens[nameIndex].Kind != TokenKind.Name)
            {
                return false;
            }

            rest = statement.GetText(tokens[nameIndex]);
        }

        return rest.Length > FunctionKeyword.Length
            && rest.StartsWith(FunctionKeyword, StringComparison.Ordinal)
            && nameIndex + 1 < tokens.Count
            && tokens[nameIndex + 1].Kind == TokenKind.LeftParenthesis
            && FindClosingParenthesis(tokens, nameIndex + 1) == tokens.Count - 1;
    }

    private static int FindAtDepthZero(IReadOnlyList<Token> tokens, TokenKind kind, int from)
    {
        var depth = 0;
        for (var index = 0; index < tokens.Count; index++)
        {
            var current = tokens[index].Kind;
            if (current == TokenKind.LeftParenthesis)
            {
                depth++;
            }
            else if (current == TokenKind.RightParenthesis)
            {
                depth--;
            }
            else if (current == kind && depth == 0 && index >= from)
            {
                return index;
            }
        }

        return -1;
    }

    private static int FindClosingParenthesis(IReadOnlyList<Token> tokens, int open)
    {
        var depth = 0;
        for (var index = open; index < tokens.Count; index++)
        {
            if (tokens[index].Kind == TokenKind.LeftParenthesis)
            {
                depth++;
            }
            else if (tokens[index].Kind == TokenKind.RightParenthesis && --depth == 0)
            {
                return index;
            }
        }

        return -1;
    }
}
