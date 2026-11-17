using System.Diagnostics.CodeAnalysis;

namespace Dracocephalum.Fortrania.Syntax;

/// <summary>
/// A statement as written in the source, parsed into its parts. The node
/// types are nested here and nothing else can derive from this type, so a
/// <see langword="switch"/> over them is complete.
/// </summary>
[SuppressMessage(
    "Naming",
    "CA1716:Identifiers should not match keywords",
    Justification = "Each node is named for the FORTRAN statement it stands for, and CALL, DO, ELSE, END, RETURN and STOP are among them.")]
public abstract record Statement
{
    private Statement()
    {
    }

    /// <summary>A type statement.</summary>
    /// <param name="Type">The type it gives its names.</param>
    /// <param name="Entities">The names declared.</param>
    public sealed record TypeDeclaration(TypeSpecification Type, IReadOnlyList<DeclaredEntity> Entities) : Statement;

    /// <summary><c>DIMENSION</c>.</summary>
    /// <param name="Entities">The arrays declared, each with its bounds.</param>
    public sealed record Dimension(IReadOnlyList<DeclaredEntity> Entities) : Statement;

    /// <summary><c>PARAMETER</c>.</summary>
    /// <param name="Constants">The constants defined, in order; a later one may use an earlier one.</param>
    public sealed record Parameter(IReadOnlyList<NamedConstant> Constants) : Statement;

    /// <summary><c>IMPLICIT NONE</c>.</summary>
    public sealed record ImplicitNone : Statement;

    /// <summary><c>SAVE</c>.</summary>
    /// <param name="Names">The variables saved; empty means everything in the program unit.</param>
    public sealed record Save(IReadOnlyList<string> Names) : Statement;

    /// <summary><c>EXTERNAL</c>.</summary>
    /// <param name="Names">The procedures named.</param>
    public sealed record External(IReadOnlyList<string> Names) : Statement;

    /// <summary><c>INTRINSIC</c>.</summary>
    /// <param name="Names">The intrinsic functions named.</param>
    public sealed record Intrinsic(IReadOnlyList<string> Names) : Statement;

    /// <summary><c>INCLUDE</c>.</summary>
    /// <param name="FileName">The file named, as written between the quotes.</param>
    public sealed record Include(string FileName) : Statement;

    /// <summary><c>DATA</c>.</summary>
    /// <param name="Groups">The pairs of names and values, in order.</param>
    public sealed record Data(IReadOnlyList<DataGroup> Groups) : Statement;

    /// <summary><c>EQUIVALENCE</c>.</summary>
    /// <param name="Sets">The sets of variables, array elements or substrings that share storage; each set has at least two.</param>
    public sealed record Equivalence(IReadOnlyList<IReadOnlyList<Expression>> Sets) : Statement;

    /// <summary>
    /// An assignment. A statement function definition is written the same way
    /// and arrives as this node too; declarations tell the two apart.
    /// </summary>
    /// <param name="Target">What is assigned to: a name, a reference or a substring.</param>
    /// <param name="Value">The value assigned.</param>
    public sealed record Assignment(Expression Target, Expression Value) : Statement;

    /// <summary><c>CALL</c>.</summary>
    /// <param name="Name">The subroutine called, in upper case.</param>
    /// <param name="Arguments">The actual arguments, which may be none.</param>
    public sealed record Call(string Name, IReadOnlyList<Expression> Arguments) : Statement;

    /// <summary>A block <c>IF</c>: <c>IF ( condition ) THEN</c>.</summary>
    /// <param name="Condition">The condition.</param>
    public sealed record BlockIf(Expression Condition) : Statement;

    /// <summary><c>ELSE IF ( condition ) THEN</c>.</summary>
    /// <param name="Condition">The condition.</param>
    public sealed record ElseIf(Expression Condition) : Statement;

    /// <summary><c>ELSE</c>.</summary>
    public sealed record Else : Statement;

    /// <summary><c>END IF</c>.</summary>
    public sealed record EndIf : Statement;

    /// <summary>A logical <c>IF</c>: a condition and the one statement it guards.</summary>
    /// <param name="Condition">The condition.</param>
    /// <param name="Body">The guarded statement, which is never a block, a loop or another <c>IF</c>.</param>
    public sealed record LogicalIf(Expression Condition, Statement Body) : Statement;

    /// <summary>A counted <c>DO</c>.</summary>
    /// <param name="EndLabel">The label of the loop's last statement, or <see langword="null"/> when the loop ends with <c>END DO</c>.</param>
    /// <param name="Variable">The loop variable, in upper case.</param>
    /// <param name="First">Its first value.</param>
    /// <param name="Last">Its last value.</param>
    /// <param name="Step">Its increment, or <see langword="null"/> when omitted, which means one.</param>
    public sealed record Do(int? EndLabel, string Variable, Expression First, Expression Last, Expression? Step) : Statement;

    /// <summary><c>DO WHILE ( condition )</c>.</summary>
    /// <param name="EndLabel">The label of the loop's last statement, or <see langword="null"/> when the loop ends with <c>END DO</c>.</param>
    /// <param name="Condition">The condition tested before each pass.</param>
    public sealed record DoWhile(int? EndLabel, Expression Condition) : Statement;

    /// <summary><c>END DO</c>.</summary>
    public sealed record EndDo : Statement;

    /// <summary><c>CONTINUE</c>.</summary>
    public sealed record Continue : Statement;

    /// <summary><c>RETURN</c>.</summary>
    public sealed record Return : Statement;

    /// <summary><c>STOP</c>.</summary>
    /// <param name="Code">The integer or character constant written after it, or <see langword="null"/>.</param>
    public sealed record Stop(Expression? Code) : Statement;

    /// <summary><c>END</c> of a program unit.</summary>
    public sealed record End : Statement;
}
