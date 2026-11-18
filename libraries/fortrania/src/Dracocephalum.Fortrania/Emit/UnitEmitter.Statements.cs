using System.Globalization;
using Dracocephalum.Fortrania.Analysis;
using Dracocephalum.Fortrania.Syntax;

namespace Dracocephalum.Fortrania.Emit;

/// <content>Writes statements, blocks and loops.</content>
internal sealed partial class UnitEmitter
{
    private void Write(IReadOnlyList<Construct> body, Symbol? result)
    {
        foreach (var construct in body)
        {
            Write(construct, result);
        }
    }

    private void Write(Construct construct, Symbol? result)
    {
        switch (construct)
        {
            case Construct.Simple simple:
                _line = simple.Statement.Source.Line;
                Write(simple.Statement.Statement, result);
                break;

            case Construct.If block:
                foreach (var branch in block.Branches)
                {
                    _line = branch.Header.Source.Line;
                    ReleaseSlots();
                    _body.Open(branch.Header.Statement switch
                    {
                        Statement.BlockIf first => $"if ({Condition(first.Condition)})",
                        Statement.ElseIf next => $"else if ({Condition(next.Condition)})",
                        _ => "else",
                    });
                    Write(branch.Body, result);
                    _body.Close();
                }

                break;

            case Construct.Loop loop:
                _line = loop.Header.Source.Line;
                ReleaseSlots();
                _body.Open(loop.Header.Statement switch
                {
                    Statement.Do counted => CountedLoop(counted),
                    Statement.DoWhile conditional => $"while ({Condition(conditional.Condition)})",
                    _ => throw new ArgumentOutOfRangeException(nameof(construct)),
                });
                Write(loop.Body, result);
                _body.Close();
                break;
        }
    }

    private void Write(Statement statement, Symbol? result)
    {
        ReleaseSlots();

        switch (statement)
        {
            // The definition of a statement function declares, and is
            // written where a method refers to the function.
            case Statement.Assignment definition when IsStatementFunctionDefinition(definition):
                break;

            case Statement.Assignment assignment:
                var target = Target(assignment.Target);
                if (target.Type == DataType.Character)
                {
                    AssignText(target, assignment);
                    break;
                }

                _body.Line($"{target.Text} = {Stored(Value(assignment.Value), target.Type)};");
                break;

            case Statement.Call call:
                _body.Line($"{Invocation(call.Name, call.Arguments)};");
                break;

            case Statement.LogicalIf logical:
                _body.Open($"if ({Condition(logical.Condition)})");
                Write(logical.Body, result);
                _body.Close();
                break;

            case Statement.Return:
                _body.Line(result is null ? "return;" : ReturnOf(result));
                break;

            case Statement.Stop stop:
                _body.Line(stop.Code switch
                {
                    null => "throw new FortranStopException();",
                    Expression.Literal { Type: DataType.Integer } code => $"throw new FortranStopException({Value(code).Text});",
                    _ => throw Unsupported("STOP with a character constant"),
                });
                break;

            case Statement.Read or Statement.Write or Statement.Print or Statement.Open or Statement.Close
                or Statement.Inquire or Statement.Rewind or Statement.Backspace or Statement.EndFile:
                throw Unsupported("an input or output statement");

            // What is left declares, and the symbol table has taken it in,
            // or does nothing: CONTINUE, and the END that closes the unit. An
            // ENTRY is where another method starts, and is not met here.
        }
    }

    // Inside a function its name is a variable; what it holds on the way
    // out is the result. A logical one is stored as an integer, and
    // returned as a bool unless the convention has it stay one.
    private string ReturnOf(Symbol result)
    {
        var name = Local(result);
        return result.Type!.Type == DataType.Logical && !_options.Convention.ReturnsLogicalAsInteger
            ? $"return {name} != 0;"
            : $"return {name};";
    }

    private string Condition(Expression condition) => Value(condition).Text;

    // A DO loop runs a number of times fixed before it starts, and leaves
    // its variable one step past the last value used. With an integer
    // variable and a step known when translating, a plain comparison against
    // the limit does both. Otherwise the count is worked out first, as the
    // language defines it.
    private string CountedLoop(Statement.Do loop)
    {
        var variable = Target(new Expression.Name(loop.Variable));
        var first = Stored(Value(loop.First), variable.Type);
        var last = Stored(Value(loop.Last), variable.Type);
        var type = variable.Type.ToCSharpStorageType();
        var number = (++_loops).ToString(CultureInfo.InvariantCulture);

        var step = loop.Step is null
            ? 1
            : (_evaluator.TryEvaluate(loop.Step, _line) as Constant.Integer)?.Value;

        if (variable.Type == DataType.Integer && step is { } known && known != 0)
        {
            var limit = last;
            if (_evaluator.TryEvaluate(loop.Last, _line) is null)
            {
                limit = $"_last{number}";
                _body.Line($"{type} {limit} = {last};");
            }

            var comparison = known > 0 ? "<=" : ">=";
            var advance = known > 0
                ? string.Create(CultureInfo.InvariantCulture, $"+= {known}")
                : string.Create(CultureInfo.InvariantCulture, $"-= {-(long)known}");
            return $"for ({variable.Text} = {first}; {variable.Text} {comparison} {limit}; {variable.Text} {advance})";
        }

        var increment = loop.Step is null ? "1" : Stored(Value(loop.Step), variable.Type);
        _body.Line($"{type} _step{number} = {increment};");
        _body.Line($"{type} _last{number} = {last};");
        _body.Line($"{variable.Text} = {first};");
        var count = $"(int)((_last{number} - {variable.Text} + _step{number}) / _step{number})";
        return $"for (int _count{number} = {count}; _count{number} > 0; _count{number}--, {variable.Text} += _step{number})";
    }
}
