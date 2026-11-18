using Dracocephalum.Fortrania.Syntax;

namespace Dracocephalum.Fortrania.Analysis;

/// <summary>
/// Draws from an analysed program unit what the rest of the program has to
/// know about it: how each of its entry points is called, and what it calls
/// in turn.
/// </summary>
public static class UnitSummarizer
{
    /// <summary>Summarizes the program unit <paramref name="table"/> describes.</summary>
    /// <param name="table">The names of a program unit.</param>
    /// <returns>The unit's summary.</returns>
    /// <exception cref="FortranAnalysisException">An argument of a call cannot be typed.</exception>
    public static UnitSummary Summarize(SymbolTable table)
    {
        ArgumentNullException.ThrowIfNull(table);

        var (kind, name) = table.Unit.Header?.Statement switch
        {
            Statement.Subroutine subroutine => (UnitKind.Subroutine, subroutine.Name),
            Statement.Function function => (UnitKind.Function, function.Name),
            Statement.BlockData blockData => (UnitKind.BlockData, blockData.Name),
            Statement.Program program => (UnitKind.MainProgram, program.Name),
            _ => (UnitKind.MainProgram, null),
        };

        var entryPoints = table.EntryPoints.Select(entry => Describe(table, entry)).ToList();

        var collector = new CallCollector(table);
        collector.Collect(table.Unit.Body);

        return new UnitSummary(kind, name, entryPoints, collector.Calls);
    }

    private static ProcedureInterface Describe(SymbolTable table, ParsedStatement entry)
    {
        var (name, parameters) = entry.Statement switch
        {
            Statement.Subroutine subroutine => (subroutine.Name, subroutine.Parameters),
            Statement.Function function => (function.Name, function.Parameters),
            Statement.Entry other => (other.Name, other.Parameters),
            _ => throw new ArgumentOutOfRangeException(nameof(entry)),
        };

        // Inside a function its name, and that of each entry, is the variable
        // that holds the result; inside a subroutine they have no type.
        var result = table.Find(name)!.Type?.Type;
        var arguments = parameters.Select(parameter => Describe(table.Find(parameter)!)).ToList();
        return new ProcedureInterface(name, result, arguments, entry.Source.Line);
    }

    private static DummyArgument Describe(Symbol symbol)
    {
        var kind = symbol.Kind switch
        {
            SymbolKind.ExternalFunction => DummyArgumentKind.Function,
            SymbolKind.Subroutine => DummyArgumentKind.Subroutine,
            SymbolKind.ExternalProcedure => DummyArgumentKind.Procedure,
            _ => symbol.IsArray ? DummyArgumentKind.Array : DummyArgumentKind.Variable,
        };

        return new DummyArgument(symbol.Name, kind, symbol.Type?.Type, symbol.Dimensions.Count);
    }

    private sealed class CallCollector(SymbolTable table)
    {
        private readonly ExpressionTyper _typer = new(table);

        public List<CallSite> Calls { get; } = [];

        public void Collect(IReadOnlyList<Construct> body)
        {
            foreach (var construct in body)
            {
                switch (construct)
                {
                    case Construct.Simple simple:
                        Collect(simple.Statement);
                        break;
                    case Construct.If block:
                        foreach (var branch in block.Branches)
                        {
                            Collect(branch.Header);
                            Collect(branch.Body);
                        }

                        break;
                    case Construct.Loop loop:
                        Collect(loop.Header);
                        Collect(loop.Body);
                        break;
                }
            }
        }

        private void Collect(ParsedStatement statement) =>
            Collect(statement.Statement, new SourceLocation(statement.Source.Line, null));

        // Declarations are passed over: no expression of theirs may reference
        // a function of another program unit.
        private void Collect(Statement statement, SourceLocation at)
        {
            switch (statement)
            {
                case Statement.Assignment assignment:
                    Collect(assignment.Target, at);
                    Collect(assignment.Value, at);
                    break;
                case Statement.Call call:
                    // What a dummy argument stands for is known only where
                    // the unit itself is called.
                    if (!table.Find(call.Name)!.IsDummyArgument)
                    {
                        Calls.Add(new CallSite(call.Name, null, Describe(call.Arguments, at), at.Line));
                    }

                    Collect(call.Arguments, at);
                    break;
                case Statement.BlockIf block:
                    Collect(block.Condition, at);
                    break;
                case Statement.ElseIf block:
                    Collect(block.Condition, at);
                    break;
                case Statement.LogicalIf logical:
                    Collect(logical.Condition, at);
                    Collect(logical.Body, at);
                    break;
                case Statement.Do loop:
                    Collect(loop.First, at);
                    Collect(loop.Last, at);
                    Collect(loop.Step, at);
                    break;
                case Statement.DoWhile loop:
                    Collect(loop.Condition, at);
                    break;
                case Statement.Read read:
                    Collect(read.Controls, at);
                    Collect(read.Items, at);
                    break;
                case Statement.Write write:
                    Collect(write.Controls, at);
                    Collect(write.Items, at);
                    break;
                case Statement.Print print:
                    Collect(print.Format, at);
                    Collect(print.Items, at);
                    break;
                case Statement.Open open:
                    Collect(open.Controls, at);
                    break;
                case Statement.Close close:
                    Collect(close.Controls, at);
                    break;
                case Statement.Inquire inquire:
                    Collect(inquire.Controls, at);
                    break;
                case Statement.Rewind rewind:
                    Collect(rewind.Controls, at);
                    break;
                case Statement.Backspace backspace:
                    Collect(backspace.Controls, at);
                    break;
                case Statement.EndFile endFile:
                    Collect(endFile.Controls, at);
                    break;
            }
        }

        private void Collect(Expression? expression, SourceLocation at)
        {
            switch (expression)
            {
                case Expression.Reference reference:
                    if (table.Find(reference.Identifier) is { Kind: SymbolKind.ExternalFunction, IsDummyArgument: false } function)
                    {
                        Calls.Add(new CallSite(function.Name, function.Type!.Type, Describe(reference.Arguments, at), at.Line));
                    }

                    Collect(reference.Arguments, at);
                    break;
                case Expression.Substring substring:
                    Collect(substring.Target, at);
                    Collect(substring.First, at);
                    Collect(substring.Last, at);
                    break;
                case Expression.Unary unary:
                    Collect(unary.Operand, at);
                    break;
                case Expression.Binary binary:
                    Collect(binary.Left, at);
                    Collect(binary.Right, at);
                    break;
                case Expression.Parenthesized parenthesized:
                    Collect(parenthesized.Inner, at);
                    break;
            }
        }

        private void Collect(IReadOnlyList<Expression> expressions, SourceLocation at)
        {
            foreach (var expression in expressions)
            {
                Collect(expression, at);
            }
        }

        private void Collect(IReadOnlyList<IOControl> controls, SourceLocation at)
        {
            foreach (var control in controls)
            {
                Collect(control.Value, at);
            }
        }

        private void Collect(IReadOnlyList<IOItem> items, SourceLocation at)
        {
            foreach (var item in items)
            {
                switch (item)
                {
                    case IOItem.Value value:
                        Collect(value.Expression, at);
                        break;
                    case IOItem.ImpliedDo loop:
                        Collect(loop.Items, at);
                        Collect(loop.First, at);
                        Collect(loop.Last, at);
                        Collect(loop.Step, at);
                        break;
                }
            }
        }

        private List<ActualArgument> Describe(IReadOnlyList<Expression> arguments, SourceLocation at) =>
            [.. arguments.Select(argument => Describe(argument, at))];

        private ActualArgument Describe(Expression argument, SourceLocation at)
        {
            var type = _typer.TypeOfArgument(argument, at);

            // A substring is passed as what it is cut from is: there is
            // somewhere to store into either way.
            var whole = argument is Expression.Substring substring ? substring.Target : argument;

            switch (whole)
            {
                case Expression.Name name:
                    var symbol = table.Find(name.Identifier)!;
                    return symbol.Kind switch
                    {
                        SymbolKind.Constant => new ActualArgument(ActualArgumentKind.Value, type, null),
                        SymbolKind.Variable => new ActualArgument(
                            symbol.IsArray && whole == argument ? ActualArgumentKind.Array : ActualArgumentKind.Variable,
                            type,
                            symbol.Name),
                        _ => new ActualArgument(ActualArgumentKind.Procedure, symbol.Type?.Type, symbol.Name),
                    };

                case Expression.Reference reference when table.Find(reference.Identifier) is { Kind: SymbolKind.Variable } array:
                    return new ActualArgument(ActualArgumentKind.ArrayElement, type, array.Name);

                default:
                    return new ActualArgument(ActualArgumentKind.Value, type, null);
            }
        }
    }
}
