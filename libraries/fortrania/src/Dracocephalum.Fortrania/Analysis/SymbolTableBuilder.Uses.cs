using Dracocephalum.Fortrania.Syntax;

namespace Dracocephalum.Fortrania.Analysis;

/// <content>Notes how each name is used, wherever one is written.</content>
public static partial class SymbolTableBuilder
{
    private sealed partial class Builder
    {
        private void Use(Statement statement, Location location)
        {
            switch (statement)
            {
                case Statement.Assignment assignment:
                    UseAsTarget(assignment.Target, location);
                    Use(assignment.Value, location);
                    break;
                case Statement.Call call:
                    Get(call.Name, location).CalledAt ??= location;
                    Use(call.Arguments, location);
                    break;
                case Statement.BlockIf block:
                    Use(block.Condition, location);
                    break;
                case Statement.ElseIf block:
                    Use(block.Condition, location);
                    break;
                case Statement.LogicalIf logical:
                    Use(logical.Condition, location);
                    Use(logical.Body, location);
                    break;
                case Statement.Do loop:
                    Get(loop.Variable, location).AssignedAt ??= location;
                    Use(loop.First, location);
                    Use(loop.Last, location);
                    Use(loop.Step, location);
                    break;
                case Statement.DoWhile loop:
                    Use(loop.Condition, location);
                    break;
                case Statement.Stop stop:
                    Use(stop.Code, location);
                    break;
                case Statement.Read read:
                    Use(read.Controls, location);
                    Use(read.Items, location);
                    break;
                case Statement.Write write:
                    Use(write.Controls, location);
                    Use(write.Items, location);
                    break;
                case Statement.Print print:
                    Use(print.Format, location);
                    Use(print.Items, location);
                    break;
                case Statement.Open open:
                    Use(open.Controls, location);
                    break;
                case Statement.Close close:
                    Use(close.Controls, location);
                    break;
                case Statement.Inquire inquire:
                    Use(inquire.Controls, location);
                    break;
                case Statement.Rewind rewind:
                    Use(rewind.Controls, location);
                    break;
                case Statement.Backspace backspace:
                    Use(backspace.Controls, location);
                    break;
                case Statement.EndFile endFile:
                    Use(endFile.Controls, location);
                    break;
                case Statement.Data data:
                    foreach (var group in data.Groups)
                    {
                        Use(group.Targets, location);
                        foreach (var value in group.Values)
                        {
                            Use(value.Repeat, location);
                            Use(value.Value, location);
                        }
                    }

                    break;
            }
        }

        private void Use(Expression? expression, Location location)
        {
            switch (expression)
            {
                case Expression.Name name:
                    Get(name.Identifier, location).UsedAsValueAt ??= location;
                    break;
                case Expression.Reference reference:
                    Get(reference.Identifier, location).ReferencedAt ??= location;
                    Use(reference.Arguments, location);
                    break;
                case Expression.Substring substring:
                    Use(substring.Target, location);
                    Use(substring.First, location);
                    Use(substring.Last, location);
                    break;
                case Expression.Unary unary:
                    Use(unary.Operand, location);
                    break;
                case Expression.Binary binary:
                    Use(binary.Left, location);
                    Use(binary.Right, location);
                    break;
                case Expression.Parenthesized parenthesized:
                    Use(parenthesized.Inner, location);
                    break;
            }
        }

        private void Use(IReadOnlyList<Expression> expressions, Location location)
        {
            foreach (var expression in expressions)
            {
                Use(expression, location);
            }
        }

        // The left side of an assignment: the name is assigned to, and only
        // what selects the element or the substring is read.
        private void UseAsTarget(Expression target, Location location)
        {
            switch (target)
            {
                case Expression.Name name:
                    Get(name.Identifier, location).AssignedAt ??= location;
                    break;
                case Expression.Reference reference:
                    var entry = Get(reference.Identifier, location);
                    entry.AssignedAt ??= location;
                    entry.ReferencedAt ??= location;
                    Use(reference.Arguments, location);
                    break;
                case Expression.Substring substring:
                    UseAsTarget(substring.Target, location);
                    Use(substring.First, location);
                    Use(substring.Last, location);
                    break;
            }
        }

        private void Use(IReadOnlyList<IOControl> controls, Location location)
        {
            foreach (var control in controls)
            {
                Use(control.Value, location);
            }
        }

        private void Use(IReadOnlyList<IOItem> items, Location location)
        {
            foreach (var item in items)
            {
                switch (item)
                {
                    case IOItem.Value value:
                        Use(value.Expression, location);
                        break;
                    case IOItem.ImpliedDo loop:
                        Use(loop.Items, location);
                        Get(loop.ControlVariable, location).AssignedAt ??= location;
                        Use(loop.First, location);
                        Use(loop.Last, location);
                        Use(loop.Step, location);
                        break;
                }
            }
        }

        private void Use(IReadOnlyList<DataTarget> targets, Location location)
        {
            foreach (var target in targets)
            {
                switch (target)
                {
                    case DataTarget.Variable variable:
                        Use(variable.Designator, location);
                        break;
                    case DataTarget.ImpliedDo loop:
                        Use(loop.Targets, location);
                        Get(loop.ControlVariable, location).UsedAsValueAt ??= location;
                        Use(loop.First, location);
                        Use(loop.Last, location);
                        Use(loop.Step, location);
                        break;
                }
            }
        }
    }
}
