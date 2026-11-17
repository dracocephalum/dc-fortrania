using Dracocephalum.Fortrania.Syntax;

namespace Dracocephalum.Fortrania.Analysis;

/// <summary>
/// Types every expression of a program unit and checks each against the
/// place it stands in: what is assigned against what it is assigned to, a
/// condition, a subscript, a loop bound.
/// </summary>
public static class TypeChecker
{
    /// <summary>Checks the program unit <paramref name="table"/> describes.</summary>
    /// <param name="table">The names of a program unit.</param>
    /// <exception cref="FortranAnalysisException">An expression has a type its place cannot take, or cannot be typed at all.</exception>
    public static void Check(SymbolTable table)
    {
        ArgumentNullException.ThrowIfNull(table);

        var checker = new Checker(table);
        checker.CheckDeclarations();
        checker.Check(table.Unit.Body);
    }

    private sealed class Checker(SymbolTable table)
    {
        private readonly ExpressionTyper _typer = new(table);

        public void CheckDeclarations()
        {
            foreach (var symbol in table.Symbols)
            {
                var at = new SourceLocation(symbol.Line, symbol.IncludeFile);

                foreach (var bound in symbol.Dimensions)
                {
                    ExpectInteger(bound.Lower, $"A bound of {symbol.Name}", at);
                    ExpectInteger(bound.Upper, $"A bound of {symbol.Name}", at);
                }

                ExpectInteger(symbol.Type?.Length?.Value, $"The length of {symbol.Name}", at);

                if (symbol.ConstantValue is not null)
                {
                    ExpectAssignable(symbol.Type!.Type, symbol.ConstantValue, symbol.Name, at);
                }
            }

            foreach (var function in table.StatementFunctions)
            {
                var at = new SourceLocation(function.Source.Line, null);
                var definition = (Statement.Assignment)function.Statement;
                var name = ((Expression.Reference)definition.Target).Identifier;
                ExpectAssignable(table.Find(name)!.Type!.Type, definition.Value, name, at);
            }

            foreach (var item in table.Equivalences.SelectMany(set => set))
            {
                // Lines of EQUIVALENCE and DATA are not kept; the unit's first
                // line stands in.
                _typer.TypeOf(item, UnitStart);
            }

            foreach (var group in table.Data)
            {
                Check(group.Targets, UnitStart);
                foreach (var value in group.Values)
                {
                    ExpectInteger(value.Repeat, "A repeat count in DATA", UnitStart);
                    _typer.TypeOf(value.Value, UnitStart);
                }
            }
        }

        private SourceLocation UnitStart => new((table.Unit.Header ?? table.Unit.End).Source.Line, null);

        public void Check(IReadOnlyList<Construct> body)
        {
            foreach (var construct in body)
            {
                switch (construct)
                {
                    case Construct.Simple simple:
                        Check(simple.Statement.Statement, At(simple.Statement));
                        break;
                    case Construct.If block:
                        foreach (var branch in block.Branches)
                        {
                            Check(branch.Header.Statement, At(branch.Header));
                            Check(branch.Body);
                        }

                        break;
                    case Construct.Loop loop:
                        Check(loop.Header.Statement, At(loop.Header));
                        Check(loop.Body);
                        break;
                }
            }
        }

        private static SourceLocation At(ParsedStatement statement) => new(statement.Source.Line, null);

        private void Check(Statement statement, SourceLocation at)
        {
            switch (statement)
            {
                case Statement.Assignment assignment:
                    // A statement function definition is checked with the
                    // declarations; its target is not an expression.
                    if (assignment.Target is Expression.Reference target && table.Find(target.Identifier)?.Kind == SymbolKind.StatementFunction)
                    {
                        break;
                    }

                    ExpectAssignable(_typer.TypeOf(assignment.Target, at), assignment.Value, "The target", at);
                    break;
                case Statement.Call call:
                    foreach (var argument in call.Arguments)
                    {
                        _typer.TypeOfArgument(argument, at);
                    }

                    break;
                case Statement.BlockIf block:
                    ExpectLogical(block.Condition, at);
                    break;
                case Statement.ElseIf block:
                    ExpectLogical(block.Condition, at);
                    break;
                case Statement.LogicalIf logical:
                    ExpectLogical(logical.Condition, at);
                    Check(logical.Body, at);
                    break;
                case Statement.Do loop:
                    CheckLoopControl(loop.Variable, loop.First, loop.Last, loop.Step, at);
                    break;
                case Statement.DoWhile loop:
                    ExpectLogical(loop.Condition, at);
                    break;
                case Statement.Read read:
                    Check(read.Controls, at);
                    Check(read.Items, at);
                    break;
                case Statement.Write write:
                    Check(write.Controls, at);
                    Check(write.Items, at);
                    break;
                case Statement.Print print:
                    if (print.Format is not null)
                    {
                        _typer.TypeOf(print.Format, at);
                    }

                    Check(print.Items, at);
                    break;
                case Statement.Open open:
                    Check(open.Controls, at);
                    break;
                case Statement.Close close:
                    Check(close.Controls, at);
                    break;
                case Statement.Inquire inquire:
                    Check(inquire.Controls, at);
                    break;
                case Statement.Rewind rewind:
                    Check(rewind.Controls, at);
                    break;
                case Statement.Backspace backspace:
                    Check(backspace.Controls, at);
                    break;
                case Statement.EndFile endFile:
                    Check(endFile.Controls, at);
                    break;
            }
        }

        private void Check(IReadOnlyList<IOControl> controls, SourceLocation at)
        {
            foreach (var control in controls)
            {
                if (control.Value is not null)
                {
                    _typer.TypeOf(control.Value, at);
                }
            }
        }

        private void Check(IReadOnlyList<IOItem> items, SourceLocation at)
        {
            foreach (var item in items)
            {
                switch (item)
                {
                    case IOItem.Value value:
                        _typer.TypeOf(value.Expression, at);
                        break;
                    case IOItem.ImpliedDo loop:
                        Check(loop.Items, at);
                        CheckLoopControl(loop.ControlVariable, loop.First, loop.Last, loop.Step, at);
                        break;
                }
            }
        }

        private void Check(IReadOnlyList<DataTarget> targets, SourceLocation at)
        {
            foreach (var target in targets)
            {
                switch (target)
                {
                    case DataTarget.Variable variable:
                        _typer.TypeOf(variable.Designator, at);
                        break;
                    case DataTarget.ImpliedDo loop:
                        Check(loop.Targets, at);
                        CheckLoopControl(loop.ControlVariable, loop.First, loop.Last, loop.Step, at);
                        break;
                }
            }
        }

        // A loop counts in integers, reals or double precision, and so do its
        // limits; they need not agree with each other.
        private void CheckLoopControl(string variable, Expression first, Expression last, Expression? step, SourceLocation at)
        {
            ExpectCounting(new Expression.Name(variable), $"The loop variable {variable}", at);
            ExpectCounting(first, "A loop's first value", at);
            ExpectCounting(last, "A loop's last value", at);
            if (step is not null)
            {
                ExpectCounting(step, "A loop's increment", at);
            }
        }

        private void ExpectCounting(Expression expression, string what, SourceLocation at)
        {
            if (_typer.TypeOf(expression, at) is not (DataType.Integer or DataType.Real or DataType.DoublePrecision))
            {
                throw at.Error($"{what} is not an integer, a real or a double precision value.");
            }
        }

        private void ExpectLogical(Expression condition, SourceLocation at)
        {
            if (_typer.TypeOf(condition, at) != DataType.Logical)
            {
                throw at.Error("A condition is not a logical value.");
            }
        }

        private void ExpectInteger(Expression? expression, string what, SourceLocation at)
        {
            if (expression is not null && _typer.TypeOf(expression, at) != DataType.Integer)
            {
                throw at.Error($"{what} is not an integer.");
            }
        }

        // Numbers convert among themselves on assignment; a logical or a
        // character value goes only into its own kind.
        private void ExpectAssignable(DataType target, Expression value, string what, SourceLocation at)
        {
            var type = _typer.TypeOf(value, at);
            var fits = ExpressionTyper.IsNumeric(target) ? ExpressionTyper.IsNumeric(type) : target == type;
            if (!fits)
            {
                throw at.Error($"{what} is of type {target}, and what is assigned to it is of type {type}.");
            }
        }
    }
}
