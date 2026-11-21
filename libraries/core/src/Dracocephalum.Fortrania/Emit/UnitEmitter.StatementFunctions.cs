using Dracocephalum.Fortrania.Analysis;
using Dracocephalum.Fortrania.Syntax;

namespace Dracocephalum.Fortrania.Emit;

/// <content>
/// Writes statement functions. Each becomes a local function of the method
/// that refers to it. Its own arguments are passed by value, which is all
/// an expression can do with them; a character one is the address of its
/// first byte, of the length its declaration gives. A variable of the unit that its
/// expression names is passed too, under its own name and in the form the
/// method has it, so that the local function captures nothing: a captured
/// local is moved out of the method's registers for the whole method.
/// </content>
internal sealed partial class UnitEmitter
{
    // The statement functions of the unit by name: the names of their
    // arguments, their expression, and the line they are defined on.
    private readonly Dictionary<string, (IReadOnlyList<string> Arguments, Expression Value, int Line)> _definitions = new(StringComparer.Ordinal);

    // Those the method being written refers to, in the order they have to
    // be declared, each with the variables of the unit it is passed.
    private readonly Dictionary<string, List<Symbol>> _passed = new(StringComparer.Ordinal);
    private readonly List<CodeWriter> _localFunctions = [];
    private readonly HashSet<string> _beingWritten = new(StringComparer.Ordinal);

    // While a statement function's expression is written: the names of its
    // arguments, the variables of the unit met so far, and whether it calls
    // a procedure of the program.
    private IReadOnlyList<string>? _ownArguments;
    private List<Symbol>? _met;
    private bool _callsProcedure;

    private void CollectStatementFunctions()
    {
        foreach (var statement in _table.StatementFunctions)
        {
            var assignment = (Statement.Assignment)statement.Statement;
            var target = (Expression.Reference)assignment.Target;
            _definitions[target.Identifier] = (
                [.. target.Arguments.Select(argument => ((Expression.Name)argument).Identifier)],
                assignment.Value,
                statement.Source.Line);
        }
    }

    private bool IsStatementFunctionDefinition(Statement.Assignment assignment) =>
        assignment.Target is Expression.Reference target && _table.Find(target.Identifier)?.Kind == SymbolKind.StatementFunction;

    // Whether a name is an argument of the statement function being
    // written. There it is a value of the local function's own, whatever
    // the unit's variable of the same name is.
    private bool IsOwnArgument(Symbol symbol) =>
        _ownArguments is not null && _ownArguments.Contains(symbol.Name, StringComparer.Ordinal);

    // A reference to a statement function, as a value.
    private Code StatementFunctionCall(Symbol function, IReadOnlyList<Expression> arguments)
    {
        if (!_passed.TryGetValue(function.Name, out var passed))
        {
            passed = WriteLocalFunction(function);
        }

        var definition = _definitions[function.Name];
        var values = new List<string>();
        for (var index = 0; index < arguments.Count; index++)
        {
            // A character argument is the address of its first byte; its
            // length is the one the function's own declaration gives.
            var type = _table.Find(definition.Arguments[index])!.Type!.Type;
            var value = Value(arguments[index]);
            values.Add(type == DataType.Character ? value.Text : Converted(value, type).Text);
        }

        foreach (var symbol in passed)
        {
            values.Add(Local(symbol));
            if (symbol.Type!.Length is { IsAssumed: true })
            {
                values.Add(LengthName(symbol));
            }
        }

        return new Code($"{function.Name.ToCSharpLocal()}({string.Join(", ", values)})", function.Type!.Type, Level.Primary);
    }

    private List<Symbol> WriteLocalFunction(Symbol function)
    {
        var definition = _definitions[function.Name];
        var callingLine = _line;
        _line = definition.Line;

        if (function.Type!.Type == DataType.Character)
        {
            throw Unsupported("a statement function of type CHARACTER");
        }

        if (!_beingWritten.Add(function.Name))
        {
            throw Unsupported("a statement function that refers to itself");
        }

        // What the statement being written has taken is set aside, and the
        // expression is written as if it were a method of its own.
        var outer = (_ownArguments, _met, _callsProcedure, _slots, _buffers, _buffersInUse);
        _ownArguments = definition.Arguments;
        _met = [];
        _callsProcedure = false;
        _slots = [];
        _buffers = [];
        _buffersInUse = 0;

        var parameters = new List<string>();
        foreach (var name in definition.Arguments)
        {
            var argument = _table.Find(name)!;
            if (argument.Type!.Length is { IsAssumed: true })
            {
                throw Unsupported("a statement function with an argument of assumed length");
            }

            var type = argument.Type.Type;
            parameters.Add(type == DataType.Character ? $"byte* {name.ToCSharpLocal()}" : $"{ValueType(type)} {name.ToCSharpLocal()}");
        }

        var value = Converted(Value(definition.Value), function.Type.Type);
        var met = _met;

        foreach (var symbol in met)
        {
            var type = symbol.Type!.Type.ToCSharpStorageType();
            parameters.Add(IsAddress(symbol) ? $"{type}* {symbol.Name.ToCSharpLocal()}" : $"{type} {symbol.Name.ToCSharpLocal()}");
            if (symbol.Type.Length is { IsAssumed: true })
            {
                parameters.Add($"int {LengthName(symbol)}");
            }
        }

        // One that calls no procedure of the program needs nothing of the
        // instance, and says so, which also has the compiler refuse a
        // capture written by mistake.
        var modifier = _callsProcedure ? string.Empty : "static ";
        var heading = $"{modifier}{ValueType(function.Type.Type)} {function.Name.ToCSharpLocal()}({string.Join(", ", parameters)})";

        var text = new CodeWriter();
        if (_slots.Count == 0 && _buffers.Count == 0)
        {
            text.Line($"{heading} => {value.Text};");
        }
        else
        {
            text.Open(heading);
            WriteScratch(text);
            text.Line($"return {value.Text};");
            text.Close();
        }

        var callsProcedure = _callsProcedure;
        (_ownArguments, _met, _callsProcedure, _slots, _buffers, _buffersInUse) = outer;
        _callsProcedure |= callsProcedure;
        _line = callingLine;
        _beingWritten.Remove(function.Name);

        _localFunctions.Add(text);
        _passed[function.Name] = met;
        return met;
    }

    // Whether the method has a variable as an address and not as a local
    // of its own: a character variable, an array, a dummy argument, and a
    // variable kept between calls or sharing storage.
    private bool IsAddress(Symbol symbol) =>
        symbol.Type!.Type == DataType.Character || symbol.IsArray || symbol.IsDummyArgument || IsPlaced(symbol);
}
