using Dracocephalum.Fortrania.Syntax;

namespace Dracocephalum.Fortrania.Analysis;

/// <summary>Lists the names written in executable statements, with no regard to what they stand for.</summary>
internal static class NameWalker
{
    /// <summary>Gives every name in <paramref name="construct"/>, as often as it is written.</summary>
    public static IEnumerable<string> NamesIn(Construct construct) => construct switch
    {
        Construct.Simple simple => NamesIn(simple.Statement.Statement),
        Construct.If block => block.Branches.SelectMany(branch =>
            NamesIn(branch.Header.Statement).Concat(branch.Body.SelectMany(NamesIn))),
        Construct.Loop loop => NamesIn(loop.Header.Statement).Concat(loop.Body.SelectMany(NamesIn)),
        _ => [],
    };

    /// <summary>Gives every name in <paramref name="statement"/>; a declaration has none that count.</summary>
    public static IEnumerable<string> NamesIn(Statement statement) => statement switch
    {
        Statement.Assignment assignment => NamesIn(assignment.Target).Concat(NamesIn(assignment.Value)),
        Statement.Call call => NamesIn(call.Arguments).Prepend(call.Name),
        Statement.BlockIf block => NamesIn(block.Condition),
        Statement.ElseIf block => NamesIn(block.Condition),
        Statement.LogicalIf logical => NamesIn(logical.Condition).Concat(NamesIn(logical.Body)),
        Statement.Do loop => NamesIn([loop.First, loop.Last, loop.Step]).Prepend(loop.Variable),
        Statement.DoWhile loop => NamesIn(loop.Condition),
        Statement.Read read => NamesIn(read.Controls).Concat(NamesIn(read.Items)),
        Statement.Write write => NamesIn(write.Controls).Concat(NamesIn(write.Items)),
        Statement.Print print => NamesIn(print.Format).Concat(NamesIn(print.Items)),
        Statement.Open open => NamesIn(open.Controls),
        Statement.Close close => NamesIn(close.Controls),
        Statement.Inquire inquire => NamesIn(inquire.Controls),
        Statement.Rewind rewind => NamesIn(rewind.Controls),
        Statement.Backspace backspace => NamesIn(backspace.Controls),
        Statement.EndFile endFile => NamesIn(endFile.Controls),
        _ => [],
    };

    private static IEnumerable<string> NamesIn(Expression? expression) => expression switch
    {
        Expression.Name name => [name.Identifier],
        Expression.Reference reference => NamesIn(reference.Arguments).Prepend(reference.Identifier),
        Expression.Substring substring => NamesIn([substring.Target, substring.First, substring.Last]),
        Expression.Unary unary => NamesIn(unary.Operand),
        Expression.Binary binary => NamesIn(binary.Left).Concat(NamesIn(binary.Right)),
        Expression.Parenthesized parenthesized => NamesIn(parenthesized.Inner),
        _ => [],
    };

    private static IEnumerable<string> NamesIn(IEnumerable<Expression?> expressions) => expressions.SelectMany(NamesIn);

    private static IEnumerable<string> NamesIn(IReadOnlyList<IOControl> controls) =>
        controls.SelectMany(control => NamesIn(control.Value));

    private static IEnumerable<string> NamesIn(IReadOnlyList<IOItem> items) => items.SelectMany(item => item switch
    {
        IOItem.Value value => NamesIn(value.Expression),
        IOItem.ImpliedDo loop => NamesIn(loop.Items)
            .Concat(NamesIn([loop.First, loop.Last, loop.Step]))
            .Prepend(loop.ControlVariable),
        _ => [],
    });
}
