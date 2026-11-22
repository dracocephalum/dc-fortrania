namespace Dracocephalum.Fortrania.Emit;

/// <summary>
/// A statement of the program tree: what a FORTRAN statement has become
/// once the emitter has decided how it is done, with the expressions it
/// holds as <see cref="TreeExpression"/>. A writer turns a list of them
/// into source text and decides nothing but spelling and layout.
/// </summary>
/// <remarks>
/// The emitter is moved over to it a part at a time. Input and output, the
/// declarations at the top of a method and the comments still write text,
/// which travels as <see cref="Text"/>, <see cref="TextBlock"/> and
/// <see cref="Blank"/>; the tree is complete when none is made.
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

    /// <summary>A local of the method declared, of a type the target language names, with its first value.</summary>
    internal sealed record Declare(string Type, string Name, TreeExpression Value) : TreeStatement;

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

    /// <summary>A line of source text written by a part of the emitter not yet moved to the tree.</summary>
    internal sealed record Text(string Line) : TreeStatement;

    /// <summary>A block under a heading of source text, written by a part of the emitter not yet moved to the tree.</summary>
    internal sealed record TextBlock(string Heading, IReadOnlyList<TreeStatement> Body) : TreeStatement;

    /// <summary>A blank line, between comments or before them.</summary>
    internal sealed record Blank : TreeStatement;

    /// <summary>A branch of an <see cref="If"/>.</summary>
    /// <param name="Condition">What has to hold for it to be taken, or nothing for the branch taken otherwise.</param>
    /// <param name="Body">What it does.</param>
    internal sealed record Branch(TreeExpression? Condition, IReadOnlyList<TreeStatement> Body);
}
