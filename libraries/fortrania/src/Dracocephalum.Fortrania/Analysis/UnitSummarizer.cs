using Dracocephalum.Fortrania.Syntax;

namespace Dracocephalum.Fortrania.Analysis;

/// <summary>
/// Draws from an analysed program unit what the rest of the program has to
/// know about it: how each of its entry points is called, what it does with
/// its arguments, and what it calls in turn.
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

        var collector = new Collector(table);
        collector.Run();

        var entryPoints = table.EntryPoints.Select(entry => Describe(table, entry, collector)).ToList();

        return new UnitSummary(kind, name, entryPoints, collector.Calls);
    }

    private static ProcedureInterface Describe(SymbolTable table, ParsedStatement entry, Collector collector)
    {
        var (name, parameters) = EntryLayout.Heading(entry.Statement);
        var reach = collector.ReachOf(name);

        // Inside a function its name, and that of each entry, is the variable
        // that holds the result; inside a subroutine they have no type.
        var result = table.Find(name)!.Type?.Type;
        var arguments = parameters.Select(parameter => Describe(table.Find(parameter)!, reach)).ToList();
        return new ProcedureInterface(name, result, arguments, entry.Source.Line);
    }

    private static DummyArgument Describe(Symbol symbol, Reach reach)
    {
        var kind = symbol.Kind switch
        {
            SymbolKind.ExternalFunction => DummyArgumentKind.Function,
            SymbolKind.Subroutine => DummyArgumentKind.Subroutine,
            SymbolKind.ExternalProcedure => DummyArgumentKind.Procedure,
            _ => symbol.IsArray ? DummyArgumentKind.Array : DummyArgumentKind.Variable,
        };

        return new DummyArgument(
            symbol.Name,
            kind,
            symbol.Type?.Type,
            symbol.Dimensions.Count,
            reach.Uses.GetValueOrDefault(symbol.Name),
            reach.Passes.GetValueOrDefault(symbol.Name) ?? []);
    }

    // What the statements one entry point reaches do with dummy arguments,
    // by the names of the arguments.
    private sealed class Reach(EntryBody body)
    {
        public EntryBody Body { get; } = body;

        public Dictionary<string, ArgumentUses> Uses { get; } = new(StringComparer.Ordinal);

        public Dictionary<string, List<ArgumentPass>> Passes { get; } = new(StringComparer.Ordinal);
    }

    // Walks the statements, noting the calls made and what is done with each
    // dummy argument that is a variable or an array.
    private sealed class Collector(SymbolTable table)
    {
        private const ArgumentUses Both = ArgumentUses.Read | ArgumentUses.Written;

        private readonly ExpressionTyper _typer = new(table);

        private readonly Dictionary<string, Reach> _reaches = new(StringComparer.Ordinal);

        // The entry points whose arguments the statement being read counts
        // for.
        private readonly List<Reach> _open = [];

        // The dummy arguments of the statement function being read, which
        // are not the unit's variables of the same names.
        private IReadOnlyList<Expression> _shadowed = [];

        // Whether calls are being listed, which is done in a pass of its
        // own so that each is listed once, whatever reaches it.
        private bool _listsCalls;

        public List<CallSite> Calls { get; } = [];

        public Reach ReachOf(string entryPoint) => _reaches[entryPoint];

        public void Run()
        {
            foreach (var body in EntryLayout.Of(table).Bodies)
            {
                _reaches.Add(body.Name, new Reach(body));
            }

            // A declaration belongs to every entry point.
            _open.AddRange(_reaches.Values);
            CollectDeclarations();
            _open.Clear();

            _listsCalls = true;
            Collect(table.Unit.Body);
            _listsCalls = false;

            foreach (var reach in _reaches.Values)
            {
                _open.Add(reach);
                Collect(reach.Body.Statements);
                _open.Clear();
            }
        }

        // A dummy argument is read where it gives the bound of an array or
        // the length of a character variable.
        private void CollectDeclarations()
        {
            foreach (var symbol in table.Symbols)
            {
                var at = new SourceLocation(symbol.Line, symbol.IncludeFile);

                foreach (var bound in symbol.Dimensions)
                {
                    Collect(bound.Lower, at);
                    Collect(bound.Upper, at);
                }

                Collect(symbol.Type?.Length?.Value, at);
            }
        }

        private void Collect(IReadOnlyList<Construct> body)
        {
            foreach (var construct in body)
            {
                Collect(construct);
            }
        }

        private void Collect(Construct construct)
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

        private void Collect(ParsedStatement statement) =>
            Collect(statement.Statement, new SourceLocation(statement.Source.Line, null));

        // Declarations are passed over here: no expression of theirs may
        // reference a function of another program unit.
        private void Collect(Statement statement, SourceLocation at)
        {
            switch (statement)
            {
                case Statement.Assignment assignment:
                    if (assignment.Target is Expression.Reference target && table.Find(target.Identifier)?.Kind == SymbolKind.StatementFunction)
                    {
                        _shadowed = target.Arguments;
                        Collect(assignment.Value, at);
                        _shadowed = [];
                        break;
                    }

                    CollectTarget(assignment.Target, at);
                    Collect(assignment.Value, at);
                    break;
                case Statement.Call call:
                    CollectCall(table.Find(call.Name)!, null, call.Arguments, at);
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
                    Mark(loop.Variable, ArgumentUses.Written);
                    Collect(loop.First, at);
                    Collect(loop.Last, at);
                    Collect(loop.Step, at);
                    break;
                case Statement.DoWhile loop:
                    Collect(loop.Condition, at);
                    break;
                case Statement.Read read:
                    Collect(read.Controls, statement, at);
                    Collect(read.Items, isInput: true, at);
                    break;
                case Statement.Write write:
                    Collect(write.Controls, statement, at);
                    Collect(write.Items, isInput: false, at);
                    break;
                case Statement.Print print:
                    Collect(print.Format, at);
                    Collect(print.Items, isInput: false, at);
                    break;
                case Statement.Open open:
                    Collect(open.Controls, statement, at);
                    break;
                case Statement.Close close:
                    Collect(close.Controls, statement, at);
                    break;
                case Statement.Inquire inquire:
                    Collect(inquire.Controls, statement, at);
                    break;
                case Statement.Rewind rewind:
                    Collect(rewind.Controls, statement, at);
                    break;
                case Statement.Backspace backspace:
                    Collect(backspace.Controls, statement, at);
                    break;
                case Statement.EndFile endFile:
                    Collect(endFile.Controls, statement, at);
                    break;
            }
        }

        // An expression whose value is taken.
        private void Collect(Expression? expression, SourceLocation at)
        {
            switch (expression)
            {
                case Expression.Name name:
                    Mark(name.Identifier, ArgumentUses.Read);
                    break;
                case Expression.Reference reference:
                    var symbol = table.Find(reference.Identifier)!;
                    if (symbol.Kind == SymbolKind.ExternalFunction)
                    {
                        CollectCall(symbol, symbol.Type!.Type, reference.Arguments, at);
                        break;
                    }

                    // An array element, or a function that only takes values:
                    // an intrinsic or a statement function.
                    Mark(reference.Identifier, ArgumentUses.Read);
                    foreach (var argument in reference.Arguments)
                    {
                        Collect(argument, at);
                    }

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

        // Something given a value: the name is written, and only what
        // selects the element or the substring is read.
        private void CollectTarget(Expression target, SourceLocation at)
        {
            switch (target)
            {
                case Expression.Name name:
                    Mark(name.Identifier, ArgumentUses.Written);
                    break;
                case Expression.Reference reference:
                    Mark(reference.Identifier, ArgumentUses.Written);
                    foreach (var subscript in reference.Arguments)
                    {
                        Collect(subscript, at);
                    }

                    break;
                case Expression.Substring substring:
                    CollectTarget(substring.Target, at);
                    Collect(substring.First, at);
                    Collect(substring.Last, at);
                    break;
            }
        }

        // A call, or a reference to a function that is not the language's
        // own. A variable passed is in the hands of the procedure called.
        private void CollectCall(Symbol procedure, DataType? resultType, IReadOnlyList<Expression> arguments, SourceLocation at)
        {
            // What a dummy argument stands for is known only where the unit
            // itself is called, so such a call is no call site.
            if (!procedure.IsDummyArgument && _listsCalls)
            {
                Calls.Add(new CallSite(procedure.Name, resultType, [.. arguments.Select(argument => Describe(argument, at))], at.Line));
            }

            for (var index = 0; index < arguments.Count; index++)
            {
                var whole = arguments[index];
                if (whole is Expression.Substring substring)
                {
                    whole = substring.Target;
                    Collect(substring.First, at);
                    Collect(substring.Last, at);
                }

                switch (whole)
                {
                    case Expression.Name name when IsVariable(name.Identifier):
                        Pass(name.Identifier, procedure, index);
                        break;
                    case Expression.Reference reference when IsVariable(reference.Identifier):
                        Pass(reference.Identifier, procedure, index);
                        foreach (var subscript in reference.Arguments)
                        {
                            Collect(subscript, at);
                        }

                        break;
                    default:
                        Collect(whole, at);
                        break;
                }
            }
        }

        private bool IsVariable(string name) => table.Find(name)!.Kind == SymbolKind.Variable;

        private bool IsShadowed(string name) =>
            _shadowed.Any(dummy => dummy is Expression.Name shadow && shadow.Identifier == name);

        private bool IsDummyVariable(string name) =>
            table.Find(name) is { IsDummyArgument: true, Kind: SymbolKind.Variable } && !IsShadowed(name);

        private void Mark(string name, ArgumentUses uses)
        {
            if (!IsDummyVariable(name))
            {
                return;
            }

            foreach (var reach in _open)
            {
                reach.Uses[name] = reach.Uses.GetValueOrDefault(name) | uses;
            }
        }

        private void Pass(string name, Symbol procedure, int index)
        {
            if (procedure.IsDummyArgument)
            {
                Mark(name, Both);
                return;
            }

            if (!IsDummyVariable(name))
            {
                return;
            }

            var pass = new ArgumentPass(procedure.Name, index);
            foreach (var reach in _open)
            {
                if (!reach.Passes.TryGetValue(name, out var passes))
                {
                    reach.Passes[name] = passes = [];
                }

                if (!passes.Contains(pass))
                {
                    passes.Add(pass);
                }
            }
        }

        // Most of what stands in a control list is read. What the statement
        // stores into is the status, everything INQUIRE asks after, and the
        // character variable a WRITE takes for its unit.
        private void Collect(IReadOnlyList<IOControl> controls, Statement statement, SourceLocation at)
        {
            for (var index = 0; index < controls.Count; index++)
            {
                var control = controls[index];
                if (control.Value is null)
                {
                    continue;
                }

                var keyword = control.Keyword ?? (index == 0 ? "UNIT" : "FMT");
                var stores = keyword switch
                {
                    "IOSTAT" => true,
                    "UNIT" => statement is Statement.Write && _typer.TypeOf(control.Value, at) == DataType.Character,
                    "FILE" or "ERR" => false,
                    _ => statement is Statement.Inquire,
                };

                if (stores)
                {
                    CollectTarget(control.Value, at);
                }
                else
                {
                    Collect(control.Value, at);
                }
            }
        }

        private void Collect(IReadOnlyList<IOItem> items, bool isInput, SourceLocation at)
        {
            foreach (var item in items)
            {
                switch (item)
                {
                    case IOItem.Value value when isInput:
                        CollectTarget(value.Expression, at);
                        break;
                    case IOItem.Value value:
                        Collect(value.Expression, at);
                        break;
                    case IOItem.ImpliedDo loop:
                        Collect(loop.Items, isInput, at);
                        Mark(loop.ControlVariable, ArgumentUses.Written);
                        Collect(loop.First, at);
                        Collect(loop.Last, at);
                        Collect(loop.Step, at);
                        break;
                }
            }
        }

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
