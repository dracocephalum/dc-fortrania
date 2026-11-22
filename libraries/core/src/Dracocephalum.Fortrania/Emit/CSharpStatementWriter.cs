namespace Dracocephalum.Fortrania.Emit;

/// <summary>
/// Writes statements of the program tree as C#, into source text that is
/// being collected. It chooses spelling and layout and nothing else.
/// </summary>
internal static class CSharpStatementWriter
{
    /// <summary>Writes methods one after another, a blank line apart.</summary>
    public static void Write(CodeWriter text, IReadOnlyList<TreeMethod> methods)
    {
        for (var index = 0; index < methods.Count; index++)
        {
            if (index > 0)
            {
                text.Blank();
            }

            Write(text, methods[index]);
        }
    }

    /// <summary>Writes a method, with the comments about it before it.</summary>
    public static void Write(CodeWriter text, TreeMethod method)
    {
        Write(text, method.Comments);
        text.Open($"public {method.ReturnType} {method.Name}({string.Join(", ", method.Parameters)})");
        if (method.Preamble.Count > 0)
        {
            Write(text, method.Preamble);
            text.Blank();
        }

        // Comments that open the code are a block of their own after what is
        // declared, as anywhere else after code.
        Write(text, method.Declarations);
        if (method.Declarations.Count > 0 && method.Body is [TreeStatement.Comment, ..])
        {
            text.Blank();
        }

        Write(text, method.Body);
        text.Close();
    }

    /// <summary>Writes statements one after another, at the depth the text is at.</summary>
    public static void Write(CodeWriter text, IReadOnlyList<TreeStatement> statements)
    {
        foreach (var statement in statements)
        {
            Write(text, statement);
        }
    }

    /// <summary>Writes a statement that is one line, as that line.</summary>
    /// <returns>The line, or nothing for a statement that is a block or a blank line.</returns>
    public static string? WriteLine(TreeStatement statement) => statement switch
    {
        TreeStatement.Comment comment => comment.Text.Length == 0 ? "//" : "// " + comment.Text,
        TreeStatement.Return { Value: null } => "return;",
        TreeStatement.Return returned => $"return {Expression(returned.Value!)};",
        TreeStatement.Throw thrown => $"throw {Expression(thrown.Exception)};",
        TreeStatement.Evaluate or TreeStatement.Assign or TreeStatement.Declare or TreeStatement.Advance or TreeStatement.Decrement
            => WriteInline(statement) + ";",
        _ => null,
    };

    private static void Write(CodeWriter text, TreeStatement statement)
    {
        switch (statement)
        {
            case TreeStatement.Blank:
                text.Blank();
                break;

            case TreeStatement.If branched:
                for (var index = 0; index < branched.Branches.Count; index++)
                {
                    var branch = branched.Branches[index];
                    var heading = branch.Condition is null
                        ? "else"
                        : $"{(index == 0 ? "if" : "else if")} ({Expression(branch.Condition)})";
                    WriteBlock(text, heading, branch.Body);
                }

                break;

            case TreeStatement.LocalFunction function:
                // One that only gives a value is written as that value.
                var signature = $"{(function.IsStatic ? "static " : string.Empty)}{function.ReturnType} {function.Name}({string.Join(", ", function.Parameters)})";
                if (function.Body is [TreeStatement.Return { Value: { } value }])
                {
                    text.Line($"{signature} => {Expression(value)};");
                }
                else
                {
                    WriteBlock(text, signature, function.Body);
                }

                break;

            case TreeStatement.While loop:
                WriteBlock(text, $"while ({Expression(loop.Condition)})", loop.Body);
                break;

            case TreeStatement.For loop:
                var steps = string.Join(", ", loop.Step.Select(WriteInline));
                WriteBlock(text, $"for ({WriteInline(loop.Start)}; {Expression(loop.Condition)}; {steps})", loop.Body);
                break;

            default:
                text.Line(WriteLine(statement) ?? throw new ArgumentOutOfRangeException(nameof(statement)));
                break;
        }
    }

    private static void WriteBlock(CodeWriter text, string heading, IReadOnlyList<TreeStatement> body)
    {
        text.Open(heading);
        Write(text, body);
        text.Close();
    }

    // A statement as it stands inside the heading of a loop, or before the
    // semicolon that ends it on a line of its own.
    private static string WriteInline(TreeStatement statement) => statement switch
    {
        TreeStatement.Evaluate evaluated => Expression(evaluated.Expression),
        TreeStatement.Assign assigned => $"{Expression(assigned.Target)} = {Expression(assigned.Value)}",
        TreeStatement.Declare declared => $"{(declared.IsConstant ? "const " : string.Empty)}{declared.Type} {declared.Name} = {Expression(declared.Value)}",
        TreeStatement.Advance advanced => $"{Expression(advanced.Target)} {(advanced.IsSubtracted ? "-=" : "+=")} {Expression(advanced.Amount)}",
        TreeStatement.Decrement decremented => Expression(decremented.Target) + "--",
        _ => throw new ArgumentOutOfRangeException(nameof(statement)),
    };

    private static string Expression(TreeExpression expression) => CSharpExpressionWriter.Write(expression);
}
