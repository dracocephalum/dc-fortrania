namespace Dracocephalum.Fortrania.Emit;

/// <summary>
/// A statement of the program tree: what a FORTRAN statement has become
/// once the emitter has decided how it is done, with the expressions it
/// holds as <see cref="TreeExpression"/>. A writer turns a list of them
/// into source text and decides nothing but spelling and layout.
/// </summary>
/// <remarks>
/// Every statement of a method is one of these, and so is each line of
/// comment that stood among the FORTRAN's and each blank line kept
/// between them. Where a blank line goes is decided as the comments are
/// added, from what stands before them.
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

    /// <summary>One added to a local, in place.</summary>
    internal sealed record Increment(TreeExpression Target) : TreeStatement;

    /// <summary>One taken from a local, in place.</summary>
    internal sealed record Decrement(TreeExpression Target) : TreeStatement;

    /// <summary>The method left, with its result where it has one.</summary>
    internal sealed record Return(TreeExpression? Value) : TreeStatement;

    /// <summary>Statements done, and others done after them however the first end, an exception among the ways.</summary>
    internal sealed record TryFinally(IReadOnlyList<TreeStatement> Body, IReadOnlyList<TreeStatement> After) : TreeStatement;

    /// <summary>An exception thrown.</summary>
    internal sealed record Throw(TreeExpression Exception) : TreeStatement;

    /// <summary>Branches tried in order, the first whose condition holds taken; one without a condition is taken when it is reached.</summary>
    internal sealed record If(IReadOnlyList<Branch> Branches) : TreeStatement;

    /// <summary>A body repeated while a condition holds.</summary>
    internal sealed record While(TreeExpression Condition, IReadOnlyList<TreeStatement> Body) : TreeStatement;

    /// <summary>
    /// A body repeated while a condition holds, with what is done once
    /// before, an <see cref="Assign"/> or a <see cref="Declare"/>, and
    /// what is done after each turn, each an <see cref="Advance"/>, an
    /// <see cref="Increment"/> or a <see cref="Decrement"/>.
    /// </summary>
    internal sealed record For(TreeStatement Start, TreeExpression Condition, IReadOnlyList<TreeStatement> Step, IReadOnlyList<TreeStatement> Body) : TreeStatement;

    /// <summary>A line of comment: its text, with nothing of how a language marks one.</summary>
    internal sealed record Comment(string Text) : TreeStatement;

    /// <summary>
    /// A remark of the translator's own about the statement after it, set
    /// off from the comments that came with the program.
    /// </summary>
    internal sealed record Note(string Text) : TreeStatement;

    /// <summary>A blank line, between comments or before them.</summary>
    internal sealed record Blank : TreeStatement;

    /// <summary>A branch of an <see cref="If"/>.</summary>
    /// <param name="Condition">What has to hold for it to be taken, or nothing for the branch taken otherwise.</param>
    /// <param name="Body">What it does.</param>
    internal sealed record Branch(TreeExpression? Condition, IReadOnlyList<TreeStatement> Body);
}
