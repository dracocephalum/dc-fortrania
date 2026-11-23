namespace Dracocephalum.Fortrania.Emit;

/// <summary>
/// Gives a pass every expression of a list of statements to change: each
/// is offered once its own parts have been, and what the pass gives back
/// stands in its place. What a pass does not change it gives back as it
/// came.
/// </summary>
/// <remarks>
/// A function of the method's own is left as it is, statements and
/// expressions both: it may be one that takes nothing of the method, and
/// so can use nothing a pass declares there.
/// </remarks>
internal static class TreeRewriter
{
    /// <summary>Rewrites the expressions of statements.</summary>
    /// <param name="statements">The statements.</param>
    /// <param name="change">Given an expression whose parts have been changed already, gives what is to stand for it.</param>
    public static IReadOnlyList<TreeStatement> Rewrite(IReadOnlyList<TreeStatement> statements, Func<TreeExpression, TreeExpression> change) =>
        [.. statements.Select(statement => Rewrite(statement, change))];

    /// <summary>Rewrites statements, those inside another's body before the one that holds them.</summary>
    /// <param name="statements">The statements.</param>
    /// <param name="change">Given a statement whose own bodies have been changed already, gives what is to stand for it: itself, another, or several.</param>
    public static IReadOnlyList<TreeStatement> RewriteStatements(IReadOnlyList<TreeStatement> statements, Func<TreeStatement, IReadOnlyList<TreeStatement>> change) =>
        [.. statements.SelectMany(statement => RewriteStatement(statement, change))];

    private static IReadOnlyList<TreeStatement> RewriteStatement(TreeStatement statement, Func<TreeStatement, IReadOnlyList<TreeStatement>> change) => change(statement switch
    {
        TreeStatement.TryFinally guarded => new TreeStatement.TryFinally(RewriteStatements(guarded.Body, change), RewriteStatements(guarded.After, change)),
        TreeStatement.If branches => new TreeStatement.If([.. branches.Branches.Select(branch => branch with { Body = RewriteStatements(branch.Body, change) })]),
        TreeStatement.While loop => loop with { Body = RewriteStatements(loop.Body, change) },
        TreeStatement.For loop => loop with { Body = RewriteStatements(loop.Body, change) },

        // What holds no statements, and a function of the method's own.
        _ => statement,
    });

    private static TreeStatement Rewrite(TreeStatement statement, Func<TreeExpression, TreeExpression> change) => statement switch
    {
        TreeStatement.Evaluate evaluate => evaluate with { Expression = Rewrite(evaluate.Expression, change) },
        TreeStatement.Assign assign => assign with { Target = Rewrite(assign.Target, change), Value = Rewrite(assign.Value, change) },
        TreeStatement.Declare declare => declare with { Value = Rewrite(declare.Value, change) },
        TreeStatement.Advance advance => advance with { Target = Rewrite(advance.Target, change), Amount = Rewrite(advance.Amount, change) },
        TreeStatement.Increment increment => increment with { Target = Rewrite(increment.Target, change) },
        TreeStatement.Decrement decrement => decrement with { Target = Rewrite(decrement.Target, change) },
        TreeStatement.Return { Value: { } value } => new TreeStatement.Return(Rewrite(value, change)),
        TreeStatement.TryFinally guarded => new TreeStatement.TryFinally(Rewrite(guarded.Body, change), Rewrite(guarded.After, change)),
        TreeStatement.Throw thrown => new TreeStatement.Throw(Rewrite(thrown.Exception, change)),
        TreeStatement.If branches => new TreeStatement.If(
        [
            .. branches.Branches.Select(branch => new TreeStatement.Branch(
                branch.Condition is { } condition ? Rewrite(condition, change) : null,
                Rewrite(branch.Body, change))),
        ]),
        TreeStatement.While loop => new TreeStatement.While(Rewrite(loop.Condition, change), Rewrite(loop.Body, change)),
        TreeStatement.For loop => new TreeStatement.For(
            Rewrite(loop.Start, change),
            Rewrite(loop.Condition, change),
            Rewrite(loop.Step, change),
            Rewrite(loop.Body, change)),

        // A return of nothing, a comment, a blank line, and a function of
        // the method's own.
        _ => statement,
    };

    private static TreeExpression Rewrite(TreeExpression node, Func<TreeExpression, TreeExpression> change)
    {
        TreeExpression[] RewriteAll(IReadOnlyList<TreeExpression> nodes) => [.. nodes.Select(each => Rewrite(each, change))];

        return change(node switch
        {
            TreeExpression.AddressOf address => new TreeExpression.AddressOf(Rewrite(address.Operand, change)),
            TreeExpression.Dereference dereference => new TreeExpression.Dereference(Rewrite(dereference.Address, change)),
            TreeExpression.Indexed indexed => new TreeExpression.Indexed(Rewrite(indexed.Address, change), Rewrite(indexed.Offset, change)),
            TreeExpression.Unary unary => unary with { Operand = Rewrite(unary.Operand, change) },
            TreeExpression.Binary binary => binary with { Left = Rewrite(binary.Left, change), Right = Rewrite(binary.Right, change) },
            TreeExpression.Sum sum => new TreeExpression.Sum([.. sum.Terms.Select(term => term with { Value = Rewrite(term.Value, change) })]),
            TreeExpression.Product product => new TreeExpression.Product(RewriteAll(product.Factors)),
            TreeExpression.Conditional conditional => new TreeExpression.Conditional(
                Rewrite(conditional.Condition, change),
                Rewrite(conditional.WhenTrue, change),
                Rewrite(conditional.WhenFalse, change)),
            TreeExpression.Converted converted => converted with { Operand = Rewrite(converted.Operand, change) },
            TreeExpression.Call call => call with { Arguments = RewriteAll(call.Arguments) },
            TreeExpression.MemberCall member => member with { Target = Rewrite(member.Target, change), Arguments = RewriteAll(member.Arguments) },
            TreeExpression.New made => made with { Arguments = RewriteAll(made.Arguments) },
            TreeExpression.NamedArgument named => named with { Value = Rewrite(named.Value, change) },
            TreeExpression.Bytes bytes => new TreeExpression.Bytes(Rewrite(bytes.Address, change), Rewrite(bytes.Length, change)),
            TreeExpression.Invoke invoke => new TreeExpression.Invoke(Rewrite(invoke.Target, change), RewriteAll(invoke.Arguments)),
            TreeExpression.Grouped grouped => new TreeExpression.Grouped(Rewrite(grouped.Inner, change)),
            TreeExpression.Noted noted => noted with { Inner = Rewrite(noted.Inner, change) },

            // What has no parts.
            _ => node,
        });
    }
}
