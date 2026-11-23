namespace Dracocephalum.Fortrania.Emit;

/// <summary>
/// The pass that has a <c>DO</c> loop which only copies one array to
/// another, or gives every element of one the same value, done at once by
/// the runtime where the run is long, and by the loop as it was where it
/// is short or where doing it at once would give something else.
/// </summary>
/// <remarks>
/// <para>
/// Only the plainest loop is taken: from the first element, one at a
/// time, to a last that is not known when translating, with one statement that stores to the element the loop's
/// variable names, from the same element of another array or from a
/// value the loop does not change. The loop is kept beside what stands
/// for it, since which is right is known only when it runs: how many
/// elements there are, and whether the two arrays overlap.
/// </para>
/// <para>
/// <c>DESIGN.md</c>, <em>A loop that copies or fills an array is done at
/// once where it is long</em>, has the measurements.
/// </para>
/// </remarks>
internal static class ArrayLoopPass
{
    private const string CanMove = "CanMoveElements";
    private const string Move = "MoveElements";
    private const string Fill = "FillElements";
    private const string LongRun = "LongRun";

    /// <summary>Rewrites the loops of a file's methods that only copy or fill.</summary>
    /// <param name="file">The file as the emitter made it.</param>
    public static TreeFile Apply(TreeFile file)
    {
        // The runtime's procedures by their own names, or by the name the
        // file gives the runtime's class where it has to.
        var runtime = file.RuntimeAlias is { } alias ? alias + "." : string.Empty;
        return file with
        {
            Members =
            [
                .. file.Members.Select(member => member is TreeMethod method
                    ? method with { Body = TreeRewriter.RewriteStatements(method.Body, statement => Replace(statement, runtime)) }
                    : member),
            ],
        };
    }

    private static TreeStatement Replace(TreeStatement statement, string runtime)
    {
        if (statement is not TreeStatement.For loop
            || loop.Start is not TreeStatement.Assign { Target: TreeExpression.Local variable, Value: TreeExpression.Integer { Value: 1 } }
            || loop.Condition is not TreeExpression.Binary { Operator: TreeExpression.Operation.LessOrEqual, Left: TreeExpression.Local counted, Right: var last }
            || loop.Step is not [TreeStatement.Advance { Target: TreeExpression.Local advanced, Amount: TreeExpression.Integer { Value: 1 }, IsSubtracted: false }]
            || loop.Body is not [TreeStatement.Assign { Target: TreeExpression.Indexed { Address: TreeExpression.Local target } stored, Value: var value }]
            || counted != variable
            || advanced != variable
            || last is not TreeExpression.Local
            || last == variable
            || !IsElementOfTheTurn(stored, variable))
        {
            return statement;
        }

        TreeExpression condition;
        TreeExpression atOnce;
        if (value is TreeExpression.Indexed { Address: TreeExpression.Local source } read && IsElementOfTheTurn(read, variable) && source != target)
        {
            condition = new TreeExpression.Call(runtime + CanMove, [source, target, last]);
            atOnce = new TreeExpression.Call(runtime + Move, [source, target, last]);
        }
        else if (IsUnchangedByTheLoop(value, variable, target))
        {
            condition = new TreeExpression.Binary(TreeExpression.Operation.GreaterOrEqual, last, new TreeExpression.Local(runtime + LongRun));
            atOnce = new TreeExpression.Call(runtime + Fill, [target, last, value]);
        }
        else
        {
            return statement;
        }

        // The runtime gives back where the loop leaves its variable, one
        // past the last.
        return new TreeStatement.If(
        [
            new TreeStatement.Branch(condition, [new TreeStatement.Assign(variable, atOnce)]),
            new TreeStatement.Branch(null, [loop]),
        ]);
    }

    // Whether an element is the one the loop's variable names in its
    // turn, the first for one, of an array that has one subscript from
    // one: the offset is the variable less one, however the emitter came
    // to build it.
    private static bool IsElementOfTheTurn(TreeExpression.Indexed element, TreeExpression.Local variable) =>
        CSharpExpressionWriter.Write(element.Offset) == variable.Name + " - 1";

    // A constant, or a variable that is neither the loop's nor the array
    // stored to. One reached through a pointer may lie in the array, and
    // then the loop stores its own value back to it, which changes
    // nothing.
    private static bool IsUnchangedByTheLoop(TreeExpression value, TreeExpression.Local variable, TreeExpression.Local target) => value switch
    {
        TreeExpression.Integer or TreeExpression.Number => true,
        TreeExpression.Unary { Operator: TreeExpression.Operation.Negate, Operand: TreeExpression.Integer or TreeExpression.Number } => true,
        TreeExpression.Local local => local != variable && local != target,
        TreeExpression.Dereference { Address: TreeExpression.Local pointer } => pointer != variable && pointer != target,
        _ => false,
    };
}
