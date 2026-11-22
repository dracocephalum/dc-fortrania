namespace Dracocephalum.Fortrania.Emit;

/// <summary>
/// A statement of the program tree: what a FORTRAN statement has become
/// once the emitter has decided how it is done, with the expressions it
/// holds as <see cref="TreeExpression"/>. A writer turns a list of them
/// into source text and decides nothing but spelling and layout.
/// </summary>
/// <remarks>
/// Every statement of a method is one of these. Its comments still come as
/// lines of text, <see cref="Text"/> and <see cref="Blank"/>, until the
/// tree has a node that says what a comment is about.
/// </remarks>
internal abstract record TreeStatement
{
    private TreeStatement()
    {
    }

    /// <summary>An expression evaluated for what it does: a call.</summary>
    internal sealed record Evaluate(TreeExpression Expression) : TreeStatement;

    /// <summary>A value stored where an expression says.</summary>
    internal sealed record Assign(TreeExpression Target, TreeExpression Value) : TreeStatement;

    /// <summary>
    /// A local of the method declared, of a type the target language names,
    /// with its first value; or a constant, which keeps that value.
    /// </summary>
    internal sealed record Declare(string Type, string Name, TreeExpression Value, bool IsConstant = false) : TreeStatement;

    /// <summary>
    /// A function of the method's own, which a statement function becomes.
    /// One that is static takes nothing of the instance. Each parameter is
    /// its type and its name, as the target language declares one.
    /// </summary>
    internal sealed record LocalFunction(bool IsStatic, string ReturnType, string Name, IReadOnlyList<string> Parameters, IReadOnlyList<TreeStatement> Body) : TreeStatement;

    /// <summary>A value added to or taken from what an expression says, in place.</summary>
    internal sealed record Advance(TreeExpression Target, TreeExpression Amount, bool IsSubtracted = false) : TreeStatement;

    /// <summary>One taken from a local, in place.</summary>
    internal sealed record Decrement(TreeExpression Target) : TreeStatement;

    /// <summary>The method left, with its result where it has one.</summary>
    internal sealed record Return(TreeExpression? Value) : TreeStatement;

    /// <summary>An exception thrown.</summary>
    internal sealed record Throw(TreeExpression Exception) : TreeStatement;

    /// <summary>Branches tried in order, the first whose condition holds taken; one without a condition is taken when it is reached.</summary>
    internal sealed record If(IReadOnlyList<Branch> Branches) : TreeStatement;

    /// <summary>A body repeated while a condition holds.</summary>
    internal sealed record While(TreeExpression Condition, IReadOnlyList<TreeStatement> Body) : TreeStatement;

    /// <summary>
    /// A body repeated while a condition holds, with what is done once
    /// before, an <see cref="Assign"/> or a <see cref="Declare"/>, and
    /// what is done after each turn, each an <see cref="Advance"/> or a
    /// <see cref="Decrement"/>.
    /// </summary>
    internal sealed record For(TreeStatement Start, TreeExpression Condition, IReadOnlyList<TreeStatement> Step, IReadOnlyList<TreeStatement> Body) : TreeStatement;

    /// <summary>A line of source text: a line of comment, as it is to be written.</summary>
    internal sealed record Text(string Line) : TreeStatement;

    /// <summary>A blank line, between comments or before them.</summary>
    internal sealed record Blank : TreeStatement;

    /// <summary>A branch of an <see cref="If"/>.</summary>
    /// <param name="Condition">What has to hold for it to be taken, or nothing for the branch taken otherwise.</param>
    /// <param name="Body">What it does.</param>
    internal sealed record Branch(TreeExpression? Condition, IReadOnlyList<TreeStatement> Body);
}
