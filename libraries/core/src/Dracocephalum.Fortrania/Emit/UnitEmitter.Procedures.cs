using System.Globalization;
using Dracocephalum.Fortrania.Analysis;
using Dracocephalum.Fortrania.Syntax;

namespace Dracocephalum.Fortrania.Emit;

/// <content>
/// Writes procedures passed as arguments. One is passed as the address of
/// a static method that takes the program's context first, since C# gives
/// no address of an instance method: a forwarder that finds the procedure's
/// library in the context and calls the procedure, written in the file of
/// the unit that passes it. A dummy procedure is a pointer to such a
/// method. Its type says what it takes where the unit's own calls show
/// that, and nothing where the unit only passes it on, as the language
/// itself asks nothing there.
/// </content>
internal sealed partial class UnitEmitter
{
    private const string Untyped = "void*";

    // What the unit's own calls show of each of its dummy procedures.
    private readonly Dictionary<string, ProcedureShape?> _ownShapes = new(StringComparer.Ordinal);

    // The forwarders the unit needs, by the procedure each forwards to,
    // with the type of a pointer to each.
    private readonly Dictionary<string, (string Name, string Type)> _forwarders = new(StringComparer.Ordinal);
    private readonly List<TreeMethod> _forwarderMethods = [];

    private ExpressionTyper? _typer;

    private static bool IsProcedure(Symbol symbol) =>
        symbol.Kind is SymbolKind.ExternalFunction or SymbolKind.Subroutine or SymbolKind.ExternalProcedure or SymbolKind.IntrinsicFunction;

    private void CollectDummyProcedures()
    {
        if (!_table.Symbols.Any(symbol => symbol.IsDummyArgument && IsProcedure(symbol)))
        {
            return;
        }

        foreach (var argument in UnitSummarizer.Summarize(_table).EntryPoints.SelectMany(entry => entry.Arguments))
        {
            _ownShapes[argument.Name] = argument.Shape;
        }
    }

    // The type of a dummy procedure of this unit, as its methods take it.
    private string OwnProcedureType(Symbol dummy) => ProcedureType(_ownShapes.GetValueOrDefault(dummy.Name));

    private string ProcedureType(ProcedureShape? shape) => shape is null
        ? Untyped
        : PointerType(shape.ResultType, shape.Arguments.Select(type => type is { } data ? AddressType(data) : Untyped));

    private static string AddressType(DataType type) => type.ToCSharpStorageType() + "*";

    private const string Context = "FortranContext";

    // The type of a pointer to a method that takes the context and then
    // what a procedure takes, each a type as a parameter has it. A
    // character argument, which is the one of type byte*, brings its
    // length, placed as the convention has it.
    private string PointerType(DataType? result, IEnumerable<string> arguments)
    {
        var addresses = new List<string> { Context };
        var lengths = new List<string>();

        if (result == DataType.Character)
        {
            addresses.Add("byte*");
            addresses.Add("int");
        }

        foreach (var argument in arguments)
        {
            addresses.Add(argument);
            if (argument == "byte*")
            {
                (_options.Convention.TextLengthsComeLast ? lengths : addresses).Add("int");
            }
        }

        var returns = result is { } type ? ResultType(type) : "void";
        return $"delegate*<{string.Join(", ", addresses.Concat(lengths))}, {returns}>";
    }

    // A procedure named as an actual argument: a dummy procedure of this
    // unit as it came, any other as the address of a forwarder to it. The
    // pointer is cast where the procedure called takes it as another type
    // than it has here, which costs nothing when the program runs.
    private TreeExpression PassedProcedure(Symbol procedure, DummyArgument? dummy)
    {
        if (_ownArguments is not null)
        {
            throw Unsupported("a statement function that passes a procedure on");
        }

        var wanted = dummy is null ? Untyped : ProcedureType(dummy.Shape);

        if (procedure.IsDummyArgument)
        {
            var name = new TreeExpression.Local(Local(procedure));
            return OwnProcedureType(procedure) == wanted ? name : new TreeExpression.Converted(wanted, name);
        }

        if (procedure.Kind == SymbolKind.IntrinsicFunction)
        {
            throw Unsupported("a function of the language as an argument");
        }

        var target = FindAnywhere(procedure.Name)
            ?? throw Unsupported("a procedure as an argument that the program does not define");

        var forwarder = Forwarder(target);
        var address = new TreeExpression.AddressOf(new TreeExpression.Local(forwarder.Name));
        return forwarder.Type == wanted
            ? address
            : new TreeExpression.Converted(wanted, new TreeExpression.Converted(forwarder.Type, address));
    }

    private (string Name, string Type) Forwarder(ProcedureInterface target)
    {
        if (_forwarders.TryGetValue(target.Name, out var known))
        {
            return known;
        }

        var parameters = new List<string> { $"{Context} context" };
        var parameterLengths = new List<string>();
        var passed = new List<string>();
        var passedLengths = new List<string>();
        var types = new List<string>();
        var lengthsComeLast = _options.Convention.TextLengthsComeLast;

        if (target.ResultType == DataType.Character)
        {
            parameters.Add("byte* result");
            parameters.Add("int result_len");
            passed.Add("result");
            passed.Add("result_len");
        }

        for (var index = 0; index < target.Arguments.Count; index++)
        {
            var argument = target.Arguments[index];
            var name = string.Create(CultureInfo.InvariantCulture, $"a{index + 1}");
            var type = argument.Kind is DummyArgumentKind.Variable or DummyArgumentKind.Array
                ? AddressType(argument.Type!.Value)
                : ProcedureType(argument.Shape);

            types.Add(type);
            parameters.Add($"{type} {name}");
            passed.Add(name);
            if (type == "byte*")
            {
                (lengthsComeLast ? parameterLengths : parameters).Add($"int {name}_len");
                (lengthsComeLast ? passedLengths : passed).Add($"{name}_len");
            }
        }

        var forwarder = (Name: $"_{UnitName}_passes_{target.Name}", Type: PointerType(target.ResultType, types));
        var returns = target.ResultType is { } result ? ResultType(result) : "void";
        var library = new TreeExpression.Call($"context.Get<{OwnerOf(target.Name)?.QualifiedName ?? _options.ClassName}>", []);
        var forwarded = new TreeExpression.MemberCall(
            library,
            _options.Convention.ProcedureName(target.Name),
            [.. passed.Concat(passedLengths).Select(name => new TreeExpression.Local(name))]);
        _forwarderMethods.Add(new TreeMethod(
            [],
            returns,
            forwarder.Name,
            [.. parameters.Concat(parameterLengths)],
            [],
            [],
            [
                // A procedure reached through an argument runs in a copy of
                // the arena of its own, since it may call back into a library
                // whose chain of calls is still running; the copy is closed
                // however the call ends.
                new TreeStatement.Evaluate(new TreeExpression.Call("context.Arena.Push", [])),
                new TreeStatement.TryFinally(
                    [target.ResultType is null ? new TreeStatement.Evaluate(forwarded) : new TreeStatement.Return(forwarded)],
                    [new TreeStatement.Evaluate(new TreeExpression.Call("context.Arena.Pop", []))]),
            ])
        {
            IsPublic = false,
            IsStatic = true,
        });

        _forwarders[target.Name] = forwarder;
        return forwarder;
    }

    // A call of a dummy procedure: through the pointer, with the context
    // first. The pointer is taken as what this call needs it to be, and
    // cast where the method's parameter does not say so already.
    private TreeExpression.Invoke CallThrough(Symbol dummy, IReadOnlyList<Expression> arguments, IEnumerable<TreeExpression> passed)
    {
        if (_ownArguments is not null)
        {
            throw Unsupported("a statement function that calls a procedure passed as an argument");
        }

        _typer ??= new ExpressionTyper(_table);
        var at = new SourceLocation(_line, null);
        var shape = new ProcedureShape(
            dummy.Kind == SymbolKind.ExternalFunction ? dummy.Type!.Type : null,
            [.. arguments.Select(argument => _typer.TypeOfArgument(argument, at))]);

        var needed = ProcedureType(shape);
        TreeExpression name = new TreeExpression.Local(Local(dummy));
        var pointer = OwnProcedureType(dummy) == needed ? name : new TreeExpression.Converted(needed, name);
        return new TreeExpression.Invoke(pointer, [new TreeExpression.Local("_context"), .. passed]);
    }
}
