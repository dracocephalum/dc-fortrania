using Dracocephalum.Fortrania.Syntax;

namespace Dracocephalum.Fortrania.Tests.Syntax;

/// <summary>
/// Renders syntax trees in a compact form for tests to assert on: operators
/// in prefix form, argument lists in brackets, substring ranges in angle
/// brackets.
/// </summary>
internal static class SyntaxRenderer
{
    public static string Render(Expression? expression) => expression switch
    {
        null => string.Empty,
        Expression.Literal literal => literal.Text,
        Expression.Name name => name.Identifier,
        Expression.Reference reference => $"{reference.Identifier}[{RenderList(reference.Arguments)}]",
        Expression.Substring substring => $"{Render(substring.Target)}<{Render(substring.First)}:{Render(substring.Last)}>",
        Expression.Unary unary => $"({unary.Operator} {Render(unary.Operand)})",
        Expression.Binary binary => $"({binary.Operator} {Render(binary.Left)} {Render(binary.Right)})",
        Expression.Parenthesized parenthesized => $"(paren {Render(parenthesized.Inner)})",
        _ => throw new ArgumentOutOfRangeException(nameof(expression)),
    };

    public static string Render(Statement statement) => statement switch
    {
        Statement.TypeDeclaration declaration =>
            $"{declaration.Type.Type}{Render(declaration.Type.Length)}: {string.Join(", ", declaration.Entities.Select(Render))}",
        Statement.Dimension dimension => $"DIMENSION {string.Join(", ", dimension.Entities.Select(Render))}",
        Statement.Parameter parameter =>
            $"PARAMETER {string.Join(", ", parameter.Constants.Select(constant => $"{constant.Name}={Render(constant.Value)}"))}",
        Statement.ImplicitNone => "IMPLICIT NONE",
        Statement.Save save => string.Join(' ', new[] { "SAVE", string.Join(", ", save.Names) }).TrimEnd(),
        Statement.External external => $"EXTERNAL {string.Join(", ", external.Names)}",
        Statement.Intrinsic intrinsic => $"INTRINSIC {string.Join(", ", intrinsic.Names)}",
        Statement.Include include => $"INCLUDE {include.FileName}",
        Statement.Data data => $"DATA {string.Join("; ", data.Groups.Select(Render))}",
        Statement.Equivalence equivalence =>
            $"EQUIVALENCE {string.Join("; ", equivalence.Sets.Select(set => string.Join(" = ", set.Select(Render))))}",
        Statement.Assignment assignment => $"{Render(assignment.Target)} = {Render(assignment.Value)}",
        Statement.Call call => $"CALL {call.Name}[{RenderList(call.Arguments)}]",
        Statement.BlockIf block => $"IF {Render(block.Condition)} THEN",
        Statement.ElseIf block => $"ELSE IF {Render(block.Condition)} THEN",
        Statement.Else => "ELSE",
        Statement.EndIf => "END IF",
        Statement.LogicalIf logical => $"IF {Render(logical.Condition)} => {Render(logical.Body)}",
        Statement.Do loop =>
            $"DO{RenderLabel(loop.EndLabel)} {loop.Variable} = {Render(loop.First)}, {Render(loop.Last)}{(loop.Step is null ? string.Empty : $", {Render(loop.Step)}")}",
        Statement.DoWhile loop => $"DO{RenderLabel(loop.EndLabel)} WHILE {Render(loop.Condition)}",
        Statement.EndDo => "END DO",
        Statement.Continue => "CONTINUE",
        Statement.Return => "RETURN",
        Statement.Stop stop => $"STOP {Render(stop.Code)}".TrimEnd(),
        Statement.End => "END",
        Statement.Program program => $"PROGRAM {program.Name}",
        Statement.Subroutine subroutine => $"SUBROUTINE {subroutine.Name}({string.Join(", ", subroutine.Parameters)})",
        Statement.Function function =>
            $"{(function.ReturnType is null ? string.Empty : $"{function.ReturnType.Type}{Render(function.ReturnType.Length)} ")}FUNCTION {function.Name}({string.Join(", ", function.Parameters)})",
        Statement.Entry entry => $"ENTRY {entry.Name}({string.Join(", ", entry.Parameters)})",
        Statement.BlockData block => $"BLOCK DATA {block.Name}".TrimEnd(),
        Statement.Read read => $"READ({Render(read.Controls)}) {Render(read.Items)}".TrimEnd(),
        Statement.Write write => $"WRITE({Render(write.Controls)}) {Render(write.Items)}".TrimEnd(),
        Statement.Print print => $"PRINT {(print.Format is null ? "*" : Render(print.Format))}: {Render(print.Items)}".TrimEnd(),
        Statement.Open open => $"OPEN({Render(open.Controls)})",
        Statement.Close close => $"CLOSE({Render(close.Controls)})",
        Statement.Inquire inquire => $"INQUIRE({Render(inquire.Controls)})",
        Statement.Rewind rewind => $"REWIND({Render(rewind.Controls)})",
        Statement.Backspace backspace => $"BACKSPACE({Render(backspace.Controls)})",
        Statement.EndFile endFile => $"ENDFILE({Render(endFile.Controls)})",
        _ => throw new ArgumentOutOfRangeException(nameof(statement)),
    };

    private static string RenderList(IReadOnlyList<Expression> expressions) => string.Join(", ", expressions.Select(Render));

    private static string Render(IReadOnlyList<IOControl> controls) =>
        string.Join(", ", controls.Select(control =>
            $"{(control.Keyword is null ? string.Empty : $"{control.Keyword}=")}{(control.Value is null ? "*" : Render(control.Value))}"));

    private static string Render(IReadOnlyList<IOItem> items) => string.Join(", ", items.Select(Render));

    private static string Render(IOItem item) => item switch
    {
        IOItem.Value value => Render(value.Expression),
        IOItem.ImpliedDo loop =>
            $"({Render(loop.Items)}, {loop.ControlVariable}={Render(loop.First)}..{Render(loop.Last)}{(loop.Step is null ? string.Empty : $" step {Render(loop.Step)}")})",
        _ => throw new ArgumentOutOfRangeException(nameof(item)),
    };

    private static string RenderLabel(int? label) => label is null ? string.Empty : $" {label}";

    private static string Render(DeclaredEntity entity)
    {
        var dimensions = entity.Dimensions.Count == 0 ? string.Empty : $"({string.Join(", ", entity.Dimensions.Select(Render))})";
        return $"{entity.Name}{dimensions}{Render(entity.Length)}";
    }

    private static string Render(DimensionBound bound)
    {
        var upper = bound.Upper is null ? "*" : Render(bound.Upper);
        return bound.Lower is null ? upper : $"{Render(bound.Lower)}:{upper}";
    }

    private static string Render(LengthSpecification? length) => length switch
    {
        null => string.Empty,
        { IsAssumed: true } => "*(*)",
        _ => $"*{Render(length.Value)}",
    };

    private static string Render(DataGroup group)
    {
        var values = group.Values.Select(value => value.Repeat is null ? Render(value.Value) : $"{Render(value.Repeat)}*{Render(value.Value)}");
        return $"{string.Join(", ", group.Targets.Select(Render))} / {string.Join(", ", values)} /";
    }

    private static string Render(DataTarget target) => target switch
    {
        DataTarget.Variable variable => Render(variable.Designator),
        DataTarget.ImpliedDo loop =>
            $"({string.Join(", ", loop.Targets.Select(Render))}, {loop.ControlVariable}={Render(loop.First)}..{Render(loop.Last)}{(loop.Step is null ? string.Empty : $" step {Render(loop.Step)}")})",
        _ => throw new ArgumentOutOfRangeException(nameof(target)),
    };
}
