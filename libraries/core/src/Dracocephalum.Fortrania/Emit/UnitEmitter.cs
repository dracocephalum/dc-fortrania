using System.Globalization;
using Dracocephalum.Fortrania.Analysis;
using Dracocephalum.Fortrania.Syntax;

namespace Dracocephalum.Fortrania.Emit;

/// <summary>
/// Writes one program unit. Every argument is a pointer, as
/// <c>PENDING.md</c> has it under <em>The translated layer passes
/// pointers</em>: a variable is passed by its address, an array by the
/// address of its first element, and a value that is neither through a
/// local variable set aside for it.
/// </summary>
internal sealed partial class UnitEmitter
{
    // A local array up to this many bytes is taken from the stack.
    private const int StackLimit = 4096;

    private const string RuntimeClass = "Dracocephalum.Fortrania.Runtime.Fortran";

    // What a faithful translation raises: a statement that cannot be
    // reached, a variable that is set and never read, and a variable
    // assigned to itself, which a conversion to its own type comes to.
    private static readonly string[] OwnSuppressions = ["CS0162", "CS0219", "CS1717"];

    private readonly SymbolTable _table;

    // The file the unit was read from, for the header, without its folder:
    // a path would put the machine it was translated on into the output.
    private readonly string? _sourceFile;
    private readonly EmitOptions _options;
    private readonly Func<string, ProcedureInterface?> _findProcedure;
    private readonly ConstantEvaluator _evaluator;

    // Whether the short name the options give the runtime's class can be
    // used: inside the emitted class a method is found before a name the
    // file declares, so the short name gives way where the program has a
    // procedure emitted under it. The answer is the same for every unit of
    // the program, since it is asked of the whole.
    private readonly bool _usesAlias;

    // What follows is kept for the method being written, and started afresh
    // for the next: a unit with ENTRY statements is several methods.
    //
    // The named constants the statements use, in the order met; only these
    // are declared, since an include file may bring hundreds no statement
    // names.
    private readonly List<Symbol> _constantsUsed = [];

    // The variables the statements name. Only these are declared: a method
    // for one entry point names few of its unit's variables, and an array it
    // does not name would still be taken from the stack on every call.
    private readonly HashSet<Symbol> _named = [];

    // What was learnt of each method written and of the unit's layout, for
    // the caller.
    private readonly List<EmittedMethod> _methods = [];
    private readonly List<UnalignedVariable> _unaligned = [];
    private readonly List<WidenedArgument> _widened = [];

    // What the method being written calls of the program's procedures, with
    // what it passes each; the bytes it takes from the stack; and whether it
    // calls a procedure it was passed.
    private readonly SortedDictionary<string, (SortedSet<string> Passes, bool PassesOnArgument)> _calls = new(StringComparer.Ordinal);
    private long _stackBytes;
    private bool _callsThroughArgument;

    // For each type, how many locals for passing values are in use in the
    // statement being written, and the most any statement needed.
    private Dictionary<DataType, (int InUse, int Most)> _slots = [];

    private StatementBuilder _body = new();
    private IReadOnlyList<string> _parameters = [];
    private Symbol? _result;
    private int _loops;
    private int _line;

    public UnitEmitter(SymbolTable table, EmitOptions options, Func<string, ProcedureInterface?> findProcedure, string? sourceFile = null)
    {
        _sourceFile = sourceFile is null ? null : Path.GetFileName(sourceFile);
        _table = table;
        _options = options;
        _findProcedure = findProcedure;
        _evaluator = new ConstantEvaluator(table);
        CollectStatementFunctions();
        CollectDummyProcedures();
        _usesAlias = options.RuntimeAlias is { } alias && !IsEmittedProcedure(alias);
        _line = (table.Unit.Header ?? table.Unit.End).Source.Line;
    }

    public EmittedUnit Emit()
    {
        var isFunction = _table.Unit.Header?.Statement switch
        {
            Statement.Function => true,
            Statement.BlockData => throw Unsupported("a block data subprogram"),
            _ => false,
        };

        RefuseWhatIsNotTranslatedYet();
        LayOutState();

        var methods = new CodeWriter();
        if (_table.EntryPoints.Count > 1)
        {
            // Each entry point is a method of its own, holding the statements
            // control passes through from there. Where control runs on from
            // one entry point into the next, both methods hold the statements
            // after the second, which is what running on does.
            var bodies = EntryLayout.Of(_table).Bodies;
            foreach (var entry in bodies)
            {
                WriteMethod(methods, entry.Entry, entry.Name, entry.Parameters, entry.Statements, isFunction, isLast: entry == bodies[^1]);
            }
        }
        else
        {
            // A main program is a method that takes nothing, under its own
            // name or the one every unnamed main program is given.
            var heading = _table.Unit.Header?.Statement is Statement.Subroutine or Statement.Function
                ? _table.Unit.Header.Statement.ProcedureHeading()
                : (Name: UnitName, Parameters: []);
            WriteMethod(methods, _table.Unit.Header, heading.Name, heading.Parameters, _table.Unit.Body, isFunction, isLast: true);
        }

        var file = new CodeWriter();
        file.Line("// <auto-generated>");
        file.Line(_sourceFile is null
            ? $"// Translated from FORTRAN 77 by Fortrania: {UnitName}."
            : $"// Translated from FORTRAN 77 by Fortrania: {UnitName}, from {_sourceFile}.");
        file.Line("// A change made here is lost when the source is translated again.");
        file.Line("// </auto-generated>");
        file.Blank();
        file.Line("#nullable enable");
        file.Line($"#pragma warning disable {string.Join(", ", Suppressed())}");
        file.Blank();
        file.Line("using System;");
        file.Line("using System.Runtime.CompilerServices;");
        file.Line("using Dracocephalum.Fortrania.Runtime;");
        file.Line(_usesAlias
            ? $"using {_options.RuntimeAlias} = {RuntimeClass};"
            : $"using static {RuntimeClass};");
        file.Blank();
        file.Line($"namespace {_options.Namespace};");
        file.Blank();
        file.Open($"public unsafe partial class {_options.ClassName}");

        // What a file says after its last unit, an appendix say, follows
        // the unit's last method.
        if (_table.Unit.TrailingComments.Any(line => !IsBlankComment(line)))
        {
            methods.Blank();
            var trailing = new CodeWriter();
            WriteComments(trailing, WithoutTrailingBlanks(_table.Unit.TrailingComments));
            methods.Append(trailing);
        }

        WriteState(file);
        file.Append(_forwarderText);
        file.Append(methods);

        file.Close();

        // Where control runs on from one entry point into the next, both
        // methods hold the statements after the second, and so their calls.
        return new EmittedUnit(file.ToString(), _methods, _unaligned, [.. _widened.Distinct()]);
    }

    // The statement that names the method, where there is one, gives the
    // comments before it; the last method of the unit is given those before
    // its END.
    private void WriteMethod(
        CodeWriter methods,
        ParsedStatement? heading,
        string name,
        IReadOnlyList<string> parameters,
        IReadOnlyList<Construct> statements,
        bool isFunction,
        bool isLast)
    {
        _body = new StatementBuilder();
        _preamble = new CodeWriter();
        _stackBytes = 0;
        _calls.Clear();
        _callsThroughArgument = false;
        _isInPreamble = true;
        _constantsUsed.Clear();
        _named.Clear();
        _slots.Clear();
        _buffers.Clear();
        _passed.Clear();
        _localFunctions.Clear();
        _loops = 0;
        _parameters = parameters;
        _result = isFunction ? _table.Find(name)! : null;

        Write(statements, _result);
        if (isLast)
        {
            WriteComments(_body, WithoutTrailingBlanks(CommentsOf(_table.Unit.End)));
        }

        // A function gives its result where control runs off its end; one that
        // ends on a RETURN has already done so.
        if (_result is not null && !EndsOnReturn(statements))
        {
            _body.Add(ReturnOf(_result));
        }

        if (!methods.IsEmpty)
        {
            methods.Blank();
        }

        // Methods are a blank line apart already, and a comment before one
        // is about it, so blank lines around such comments are left out.
        if (heading is not null)
        {
            var comments = new CodeWriter();
            WriteComments(comments, WithoutTrailingBlanks(CommentsOf(heading)));
            methods.Append(comments);
        }

        var returns = _result is null ? "void" : ResultType(_result.Type!.Type);
        methods.Open($"public {returns} {_options.Convention.ProcedureName(name)}({string.Join(", ", Parameters(parameters, _result))})");
        if (!_preamble.IsEmpty)
        {
            methods.Append(_preamble);
            methods.Blank();
        }

        // Comments that open the code are a block of their own after what is
        // declared, as anywhere else after code.
        WriteDeclarations(methods);
        _methods.Add(new EmittedMethod(
            name,
            _stackBytes,
            [.. _calls.Select(call => new EmittedCall(call.Key, [.. call.Value.Passes], call.Value.PassesOnArgument))],
            _callsThroughArgument));
        if (methods.LastLine is not ("" or "{") && _body.StartsWithComment)
        {
            methods.Blank();
        }

        CSharpStatementWriter.Write(methods, _body.Statements);
        methods.Close();
    }

    // Records that the method being written names a variable, and gives the
    // variable it is there. In a function with ENTRY statements the results
    // of all its entry points are one variable under several names, so each
    // of those names is the result of the method being written.
    private Symbol Use(Symbol symbol)
    {
        if (_result is not null && symbol is { IsEntryPoint: true, Kind: SymbolKind.Variable } && symbol.Name != _result.Name)
        {
            if (symbol.Type!.Type != _result.Type!.Type)
            {
                throw Unsupported("entry points whose results differ in type, one used through the name of another");
            }

            symbol = _result;
        }

        // An argument of the statement function being written is not the
        // unit's variable of that name, and is not met here unless another
        // statement function is to be passed the variable.
        if (IsOwnArgument(symbol))
        {
            throw Unsupported("a statement function with an argument named as a variable that a statement function it refers to uses");
        }

        if (_met is not null && !_met.Contains(symbol))
        {
            _met.Add(symbol);
        }

        _named.Add(symbol);
        return symbol;
    }

    private string Local(Symbol symbol) => Use(symbol).Name.ToCSharpLocal();

    // A dummy argument of another entry point than the one being written.
    // The language forbids naming it, since nothing was passed for it, and
    // programs do all the same in branches never taken.
    private bool IsStray(Symbol symbol) =>
        symbol.IsDummyArgument && !_parameters.Contains(symbol.Name, StringComparer.Ordinal);

    // Every dummy argument is a pointer. A character one comes with its
    // length, which the procedure takes even where its declaration fixes
    // the length, since the caller cannot know; the convention says whether
    // the length follows its pointer or all lengths follow all pointers.
    private IEnumerable<string> Parameters(IReadOnlyList<string> names, Symbol? result)
    {
        var addresses = new List<string>();
        var lengths = new List<string>();

        // The place for a character function's result comes first, address
        // and length together under every convention.
        if (result?.Type!.Type == DataType.Character)
        {
            addresses.Add($"byte* {result.Name.ToCSharpLocal()}");
            addresses.Add($"int {LengthName(result)}");
        }

        foreach (var name in names)
        {
            var symbol = _table.Find(name)!;
            if (IsProcedure(symbol))
            {
                addresses.Add($"{OwnProcedureType(symbol)} {name.ToCSharpLocal()}");
                continue;
            }

            addresses.Add($"{symbol.Type!.Type.ToCSharpStorageType()}* {name.ToCSharpLocal()}");
            if (symbol.Type.Type == DataType.Character)
            {
                (_options.Convention.TextLengthsComeLast ? lengths : addresses).Add($"int {LengthName(symbol)}");
            }
        }

        return addresses.Concat(lengths);
    }

    // Writes the name of one of the runtime's methods as a call needs it:
    // behind the short name where the options give one, and bare otherwise,
    // which the file's own using directive allows. Either gives way to the
    // full name where the program has a procedure emitted under the name
    // that would be written, since that procedure would be the one found.
    private string Runtime(string method)
    {
        if (_options.RuntimeAlias is { } alias)
        {
            return _usesAlias ? $"{alias}.{method}" : $"{RuntimeClass}.{method}";
        }

        return IsEmittedProcedure(method) ? $"{RuntimeClass}.{method}" : method;
    }

    // The library of the program that defines a procedure, when it is not
    // this one. This library's own are found first, and then the others
    // in the order the options give them.
    private LibraryReference? OwnerOf(string name) => _findProcedure(name) is not null
        ? null
        : _options.References.FirstOrDefault(reference => reference.FindProcedure(name) is not null);

    private ProcedureInterface? FindAnywhere(string name) =>
        _findProcedure(name) ?? OwnerOf(name)?.FindProcedure(name);

    // A FORTRAN name is in upper case, and the convention may keep it or
    // change it, so both spellings are tried.
    private bool IsEmittedProcedure(string name) =>
        new[] { name, name.ToUpperInvariant() }.Any(procedure =>
            _findProcedure(procedure) is not null && _options.Convention.ProcedureName(procedure) == name);

    // The type of a value in an expression: a logical one is a C# bool
    // there, whatever it is stored as.
    private static string ValueType(DataType type) => type == DataType.Logical ? "bool" : type.ToCSharpStorageType();

    private static bool EndsOnReturn(IReadOnlyList<Construct> body)
    {
        // The END that closes the unit is its last statement, and declarations
        // or CONTINUE may stand between it and the RETURN.
        for (var index = body.Count - 1; index >= 0; index--)
        {
            switch (body[index])
            {
                case Construct.Simple { Statement.Statement: Statement.Return }:
                    return true;
                case Construct.Simple { Statement.Statement: Statement.End or Statement.Continue }:
                    continue;
                default:
                    return false;
            }
        }

        return false;
    }

    // The type a function returns. Under some conventions a logical result
    // goes back as the integer it is stored as.
    private string ResultType(DataType type) => type switch
    {
        // A character function is handed the place for its result and gives
        // back that address.
        DataType.Character => "byte*",
        DataType.Logical when _options.Convention.ReturnsLogicalAsInteger => "int",
        _ => ValueType(type),
    };

    private IEnumerable<string> Suppressed() =>
        OwnSuppressions.Concat(_options.SuppressedWarnings).Distinct(StringComparer.Ordinal);

    private void RefuseWhatIsNotTranslatedYet()
    {
        foreach (var symbol in _table.Symbols)
        {
            _line = symbol.Line;

            if (symbol.Kind != SymbolKind.Variable)
            {
                continue;
            }

            if (symbol.Type!.Type == DataType.Complex)
            {
                throw Unsupported("a COMPLEX variable");
            }
        }
    }

    private void WriteDeclarations(CodeWriter file)
    {
        foreach (var constant in _constantsUsed)
        {
            var type = ValueType(constant.Type!.Type);
            file.Line($"const {type} {constant.Name.ToCSharpLocal()} = {_evaluator.ValueOf(constant).ToCSharpLiteral()};");
        }

        WriteStateAccess(file);
        WriteOverlays(file);

        foreach (var symbol in _table.Symbols.Where(symbol => _named.Contains(symbol) && IsStray(symbol)))
        {
            _line = symbol.Line;
            WriteStray(file, symbol);
        }

        foreach (var symbol in _table.Symbols.Where(symbol => symbol is { Kind: SymbolKind.Variable, IsDummyArgument: false } && _named.Contains(symbol)))
        {
            // The result of a character function is an argument, not a local,
            // and a kept variable has been declared with the unit's block.
            if ((symbol.IsEntryPoint && symbol.Type!.Type == DataType.Character) || IsPlaced(symbol))
            {
                continue;
            }

            _line = symbol.Line;
            var type = symbol.Type!.Type.ToCSharpStorageType();
            var name = symbol.Name.ToCSharpLocal();

            var isText = symbol.Type.Type == DataType.Character;
            if (!symbol.IsArray && !isText)
            {
                file.Line($"{type} {name} = 0;");
                continue;
            }

            // A character variable is so many bytes, and an array of them so
            // many times that, end to end.
            long count = isText ? long.Parse(LengthOf(symbol), CultureInfo.InvariantCulture) : 1;
            foreach (var bound in symbol.Dimensions)
            {
                count *= ConstantExtent(bound) ?? throw Unsupported("a local array whose size is not constant");
            }

            file.Line($"{type}* {name} = {TakeFromStack(type, count, ElementCount(symbol) * ElementSize(symbol))};");
        }

        WriteScratch(file);

        foreach (var function in _localFunctions)
        {
            file.Lines(function);
        }
    }

    // The places for made character values and the locals for passing
    // values that the statements of a method, or the expression of a
    // statement function, have taken.
    private void WriteScratch(CodeWriter file)
    {
        for (var number = 1; number <= _buffers.Count; number++)
        {
            file.Line(string.Create(CultureInfo.InvariantCulture, $"byte* _s{number} = {TakeFromStack("byte", _buffers[number - 1], _buffers[number - 1])};"));
        }

        foreach (var (type, (_, most)) in _slots.OrderBy(slot => slot.Key))
        {
            for (var number = 1; number <= most; number++)
            {
                file.Line($"{type.ToCSharpStorageType()} {Slot(type, number)} = 0;");
            }
        }
    }

    // A stray argument is a local that holds zero or blanks: an array has
    // one element, text of assumed length one character, and a procedure
    // is no address at all. The statement
    // that names it then does something harmless, where the original would
    // follow an address nobody set.
    private void WriteStray(CodeWriter file, Symbol symbol)
    {
        var name = symbol.Name.ToCSharpLocal();
        if (IsProcedure(symbol))
        {
            file.Line($"{OwnProcedureType(symbol)} {name} = null;");
            return;
        }

        if (symbol.Type!.Type != DataType.Character)
        {
            var type = symbol.Type.Type.ToCSharpStorageType();
            file.Line($"{type}* {name} = {TakeFromStack(type, 1, symbol.Type.Type.GetStorageSize())} {{ 0 }};");
            return;
        }

        var length = "1";
        if (symbol.Type.Length is { IsAssumed: true })
        {
            file.Line($"int {LengthName(symbol)} = 1;");
        }
        else
        {
            length = LengthOf(symbol);
        }

        var bytes = long.Parse(length, CultureInfo.InvariantCulture);
        file.Line($"byte* {name} = {TakeFromStack("byte", bytes, bytes)};");
        file.Line($"new Span<byte>({name}, {length}).Fill((byte)' ');");
    }

    // Takes so many elements of a type from the stack for the method being
    // written, and counts their bytes against it.
    private string TakeFromStack(string type, long count, long bytes)
    {
        _stackBytes += bytes;
        return string.Create(CultureInfo.InvariantCulture, $"stackalloc {type}[{count}]");
    }

    private static string Slot(DataType type, int number) => type switch
    {
        DataType.Integer => string.Create(CultureInfo.InvariantCulture, $"_i{number}"),
        DataType.Real => string.Create(CultureInfo.InvariantCulture, $"_r{number}"),
        DataType.Character => string.Create(CultureInfo.InvariantCulture, $"_c{number}"),
        _ => string.Create(CultureInfo.InvariantCulture, $"_d{number}"),
    };

    // Names a local set aside for passing one value in the statement being
    // written. An underscore begins no FORTRAN name, so it can meet none.
    private string TakeSlot(DataType type)
    {
        var (inUse, most) = _slots.GetValueOrDefault(type);
        inUse++;
        _slots[type] = (inUse, Math.Max(most, inUse));
        return Slot(type, inUse);
    }

    private void ReleaseSlots()
    {
        _buffersInUse = 0;
        foreach (var type in _slots.Keys.ToList())
        {
            _slots[type] = (0, _slots[type].Most);
        }
    }

    private int? ConstantBound(Expression? bound, int whenOmitted) => bound is null
        ? whenOmitted
        : (_evaluator.TryEvaluate(bound, _line) as Constant.Integer)?.Value;

    // The number of elements along one dimension, when both bounds are
    // constant; an assumed size has none.
    private int? ConstantExtent(DimensionBound bound)
    {
        if (bound.Upper is null)
        {
            return null;
        }

        return ConstantBound(bound.Upper, 0) - ConstantBound(bound.Lower, 1) + 1;
    }

    private UnsupportedConstructException Unsupported(string construct) => new(_line, construct);

    // A piece of C# with what is needed to join it to another: the type of
    // its value, and how strongly it holds together.
    // A value on its way into the method being written: its expression in
    // the program tree, the FORTRAN type it has, and for a character value
    // its length beside its address.
    private readonly record struct Code(TreeExpression Node, DataType Type, TreeExpression? LengthNode = null)
    {
        public string Text => CSharpExpressionWriter.Write(Node);

        public Level Level => CSharpExpressionWriter.LevelOf(Node);

        public string? Length => LengthNode is null ? null : CSharpExpressionWriter.Write(LengthNode);

        public string At(Level needed) => CSharpExpressionWriter.WriteAt(Node, needed);
    }
}
