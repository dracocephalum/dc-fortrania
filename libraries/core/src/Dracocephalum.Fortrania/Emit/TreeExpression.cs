using Dracocephalum.Fortrania.Analysis;

namespace Dracocephalum.Fortrania.Emit;

/// <summary>
/// An expression of the program tree: what a FORTRAN expression has become
/// once the emitter has decided where every value lies and how it is
/// reached, with nothing of FORTRAN left in it. A writer turns one into
/// source text and decides nothing but spelling and parentheses; a pass
/// takes one and gives another.
/// </summary>
/// <remarks>
/// <c>PENDING.md</c>, <em>A tree between the analysis and the writers</em>,
/// has the plan this is the first piece of: every expression the emitter
/// makes is one of these, and statements are still written as text.
/// </remarks>
internal abstract record TreeExpression
{
    private TreeExpression()
    {
    }

    /// <summary>What a <see cref="Unary"/> or a <see cref="Binary"/> does.</summary>
    internal enum Operation
    {
        Negate,
        Not,
        Complement,
        Add,
        Subtract,
        Multiply,
        Divide,
        Remainder,
        BitAnd,
        BitOr,
        BitXor,
        Equal,
        NotEqual,
        Less,
        LessOrEqual,
        Greater,
        GreaterOrEqual,
        And,
        Or,
    }

    /// <summary>A whole number known when translating.</summary>
    internal sealed record Integer(long Value) : TreeExpression;

    /// <summary>A real, double precision or logical value known when translating.</summary>
    internal sealed record Number(Constant Value) : TreeExpression;

    /// <summary>No address at all.</summary>
    internal sealed record Null : TreeExpression;

    /// <summary>One character, as the target language writes one.</summary>
    internal sealed record Character(char Value) : TreeExpression;

    /// <summary>
    /// So many elements of a type taken from the stack for as long as the
    /// method runs, set to zero where that is asked for and otherwise left
    /// as the stack had them.
    /// </summary>
    internal sealed record StackAllocation(string Type, long Count, bool IsZeroed = false) : TreeExpression;

    /// <summary>A local or a parameter of the method, a constant it declares, or a member of the framework named in full, by its name.</summary>
    internal sealed record Local(string Name) : TreeExpression;

    /// <summary>The bytes of ASCII text, kept in the assembly.</summary>
    internal sealed record Utf8(string Text) : TreeExpression;

    /// <summary>The address of a variable or of an element.</summary>
    internal sealed record AddressOf(TreeExpression Operand) : TreeExpression;

    /// <summary>What lies at an address.</summary>
    internal sealed record Dereference(TreeExpression Address) : TreeExpression;

    /// <summary>The element so many elements on from an address.</summary>
    internal sealed record Indexed(TreeExpression Address, TreeExpression Offset) : TreeExpression;

    /// <summary>An operator with one operand.</summary>
    internal sealed record Unary(Operation Operator, TreeExpression Operand) : TreeExpression;

    /// <summary>
    /// An operator with two operands, grouped as it is here: the operands
    /// are the program's own, whose order of evaluation is kept.
    /// </summary>
    internal sealed record Binary(Operation Operator, TreeExpression Left, TreeExpression Right) : TreeExpression;

    /// <summary>
    /// Terms added and subtracted, in arithmetic of the translator's own
    /// making, an address or a length or a position, where the grouping
    /// does not matter and none is written.
    /// </summary>
    internal sealed record Sum(IReadOnlyList<Term> Terms) : TreeExpression;

    /// <summary>Factors multiplied, in arithmetic of the translator's own making.</summary>
    internal sealed record Product(IReadOnlyList<TreeExpression> Factors) : TreeExpression;

    /// <summary>One of two values, by a condition.</summary>
    internal sealed record Conditional(TreeExpression Condition, TreeExpression WhenTrue, TreeExpression WhenFalse) : TreeExpression;

    /// <summary>A value converted to a type of the target language, named as that language names it.</summary>
    internal sealed record Converted(string Type, TreeExpression Operand) : TreeExpression;

    /// <summary>A call of a method by its name: the runtime's, the framework's, or one of the program's own.</summary>
    internal sealed record Call(string Method, IReadOnlyList<TreeExpression> Arguments) : TreeExpression;

    /// <summary>A call of a method of a value, as each step of a concatenation is of the one before.</summary>
    internal sealed record MemberCall(TreeExpression Target, string Method, IReadOnlyList<TreeExpression> Arguments) : TreeExpression;

    /// <summary>An object of a type of the runtime's or the framework's made, which is how an exception to be thrown comes to be.</summary>
    internal sealed record New(string Type, IReadOnlyList<TreeExpression> Arguments) : TreeExpression;

    /// <summary>A call through the address of a procedure.</summary>
    internal sealed record Invoke(TreeExpression Target, IReadOnlyList<TreeExpression> Arguments) : TreeExpression;

    /// <summary>
    /// A value in parentheses that are to be written whatever stands
    /// around it: the program's own, which fix how a real expression is
    /// evaluated, or the translator's around a part of its arithmetic.
    /// </summary>
    internal sealed record Grouped(TreeExpression Inner) : TreeExpression;

    /// <summary>A term of a <see cref="Sum"/>.</summary>
    /// <param name="Value">The term.</param>
    /// <param name="IsSubtracted">Whether it is taken away and not added.</param>
    internal sealed record Term(TreeExpression Value, bool IsSubtracted = false);
}
