using System.Globalization;
using Dracocephalum.Fortrania.Analysis;
using Dracocephalum.Fortrania.Syntax;

namespace Dracocephalum.Fortrania.Emit;

/// <content>Turns statements, blocks and loops into the program tree.</content>
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
                WriteCommentsOf(simple.Statement, IsExecutable(simple.Statement.Statement));
                Write(simple.Statement.Statement, result);
                break;

            // The comments before an ELSE or ELSE IF are about the arm it
            // opens, and go inside it; those before the END IF or END DO
            // close the block they are in.
            case Construct.If block:
                var branches = new List<TreeStatement.Branch>();
                for (var index = 0; index < block.Branches.Count; index++)
                {
                    var branch = block.Branches[index];
                    _line = branch.Header.Source.Line;
                    ReleaseSlots();
                    if (index == 0)
                    {
                        WriteCommentsOf(branch.Header, isExecutable: true);
                    }

                    var condition = branch.Header.Statement switch
                    {
                        Statement.BlockIf first => Condition(first.Condition),
                        Statement.ElseIf next => Condition(next.Condition),
                        _ => null,
                    };
                    _body.Begin();
                    if (index > 0)
                    {
                        WriteComments(_body, CommentsOf(branch.Header));
                    }

                    Write(branch.Body, result);
                    if (index == block.Branches.Count - 1)
                    {
                        WriteComments(_body, WithoutTrailingBlanks(CommentsOf(block.End)));
                    }

                    branches.Add(new TreeStatement.Branch(condition, _body.End()));
                }

                _body.Add(new TreeStatement.If(branches));
                break;

            case Construct.Loop loop:
                _line = loop.Header.Source.Line;
                ReleaseSlots();
                WriteCommentsOf(loop.Header, isExecutable: true);
                Func<IReadOnlyList<TreeStatement>, TreeStatement> looped = loop.Header.Statement switch
                {
                    Statement.Do counted => CountedLoop(counted),
                    Statement.DoWhile conditional => While(Condition(conditional.Condition)),
                    _ => throw new ArgumentOutOfRangeException(nameof(construct)),
                };
                _body.Begin();
                Write(loop.Body, result);
                WriteComments(_body, WithoutTrailingBlanks(CommentsOf(loop.End)));
                _body.Add(looped(_body.End()));
                break;
        }

        static Func<IReadOnlyList<TreeStatement>, TreeStatement> While(TreeExpression condition) =>
            body => new TreeStatement.While(condition, body);
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

                _body.Add(new TreeStatement.Assign(target.Node, StoredNode(Value(assignment.Value), target.Type)));
                break;

            case Statement.Call call:
                _body.Add(new TreeStatement.Evaluate(InvocationNode(call.Name, call.Arguments)));
                break;

            case Statement.LogicalIf logical:
                var condition = Condition(logical.Condition);
                _body.Begin();
                Write(logical.Body, result);
                _body.Add(new TreeStatement.If([new TreeStatement.Branch(condition, _body.End())]));
                break;

            case Statement.Return:
                _body.Add(result is null ? new TreeStatement.Return(null) : ReturnOf(result));
                break;

            case Statement.Stop stop:
                _body.Add(new TreeStatement.Throw(new TreeExpression.New("FortranStopException", stop.Code switch
                {
                    null => [],
                    Expression.Literal { Type: DataType.Integer } code => [Value(code).Node],
                    _ => throw Unsupported("STOP with a character constant"),
                })));
                break;

            case Statement.Read or Statement.Write or Statement.Print or Statement.Open or Statement.Close
                or Statement.Inquire or Statement.Rewind or Statement.Backspace or Statement.EndFile:
                WriteInputOutput(statement);
                break;

            // What is left declares, and the symbol table has taken it in,
            // or does nothing: CONTINUE, and the END that closes the unit. An
            // ENTRY is where another method starts, and is not met here.
        }
    }

    // Inside a function its name is a variable; what it holds on the way
    // out is the result. A logical one is stored as an integer, and
    // returned as a bool unless the convention has it stay one.
    private TreeStatement.Return ReturnOf(Symbol result)
    {
        TreeExpression name = new TreeExpression.Local(Local(result));
        return new TreeStatement.Return(result.Type!.Type == DataType.Logical && !_options.Convention.ReturnsLogicalAsInteger
            ? new TreeExpression.Binary(TreeExpression.Operation.NotEqual, name, Zero)
            : name);
    }

    private TreeExpression Condition(Expression condition) => Value(condition).Node;

    // A DO loop runs a number of times fixed before it starts, and leaves
    // its variable one step past the last value used. With an integer
    // variable and a step known when translating, a plain comparison against
    // the limit does both. Otherwise the count is worked out first, as the
    // language defines it. What has to be worked out before the loop is
    // added here; the loop itself is given once its body is known.
    private Func<IReadOnlyList<TreeStatement>, TreeStatement> CountedLoop(Statement.Do loop)
    {
        var counter = Target(new Expression.Name(loop.Variable));
        var variable = counter.Node;
        var variableType = counter.Type;
        var first = StoredNode(Value(loop.First), variableType);
        var last = StoredNode(Value(loop.Last), variableType);
        var type = variableType.ToCSharpStorageType();
        var number = (++_loops).ToString(CultureInfo.InvariantCulture);

        var step = loop.Step is null
            ? 1
            : (_evaluator.TryEvaluate(loop.Step, _line) as Constant.Integer)?.Value;

        if (variableType == DataType.Integer && step is { } known && known != 0)
        {
            var limit = last;
            if (_evaluator.TryEvaluate(loop.Last, _line) is null)
            {
                limit = new TreeExpression.Local($"_last{number}");
                _body.Add(new TreeStatement.Declare(type, $"_last{number}", last));
            }

            var comparison = known > 0 ? TreeExpression.Operation.LessOrEqual : TreeExpression.Operation.GreaterOrEqual;
            var advance = new TreeStatement.Advance(variable, new TreeExpression.Integer(Math.Abs((long)known)), IsSubtracted: known < 0);
            return body => new TreeStatement.For(
                new TreeStatement.Assign(variable, first),
                new TreeExpression.Binary(comparison, variable, limit),
                [advance],
                body);
        }

        var increment = loop.Step is null ? One : StoredNode(Value(loop.Step), variableType);
        var stepName = new TreeExpression.Local($"_step{number}");
        var lastName = new TreeExpression.Local($"_last{number}");
        var countName = new TreeExpression.Local($"_count{number}");
        _body.Add(new TreeStatement.Declare(type, stepName.Name, increment));
        _body.Add(new TreeStatement.Declare(type, lastName.Name, last));
        _body.Add(new TreeStatement.Assign(variable, first));

        var span = new TreeExpression.Grouped(Add(Plus(lastName), Minus(variable), Plus(stepName)));
        var count = new TreeExpression.Converted(
            "int",
            new TreeExpression.Grouped(new TreeExpression.Binary(TreeExpression.Operation.Divide, span, stepName)));
        return body => new TreeStatement.For(
            new TreeStatement.Declare("int", countName.Name, count),
            new TreeExpression.Binary(TreeExpression.Operation.Greater, countName, Zero),
            [new TreeStatement.Decrement(countName), new TreeStatement.Advance(variable, stepName)],
            body);
    }
}
