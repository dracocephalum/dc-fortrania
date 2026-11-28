using System.Globalization;
using Dracocephalum.Fortrania.Analysis;
using Dracocephalum.Fortrania.Syntax;

namespace Dracocephalum.Fortrania.Emit;

/// <summary>
/// Writes one program unit. Every argument is a pointer, as
/// <c>DESIGN.md</c> has it under <em>What emitted code looks like, and
/// why</em>: a variable is passed by its address, an array by the
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

    // The platform the unit is written for where it is one of several
    // variants of a unit; nothing for a unit written once. A variant's
    // methods are the class's own, under names of their own, and a method
    // of the unit's name chooses between them.
    private readonly PlatformVariant? _variant;

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

    public UnitEmitter(SymbolTable table, EmitOptions options, Func<string, ProcedureInterface?> findProcedure, string? sourceFile = null, PlatformVariant? variant = null)
    {
        _sourceFile = sourceFile is null ? null : Path.GetFileName(sourceFile);
        _variant = variant;
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

        var methods = new List<TreeMethod>();
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

        string[] suppressed = [.. Suppressed()];

        // What a file says after its last unit, an appendix say, follows
        // the unit's last method.
        var trailing = new StatementBuilder();
        WriteComments(trailing, WithoutTrailingBlanks(_table.Unit.TrailingComments));

        // The unit's block of kept variables and its loader come first,
        // then the forwarders its methods needed, then the methods.
        var file = new TreeFile(
            UnitName,
            _sourceFile,
            _options.Namespace,
            _options.ClassName,
            RuntimeClass,
            _usesAlias ? _options.RuntimeAlias : null,
            suppressed,
            [.. StateMembers(), .. _forwarderMethods, .. methods],
            trailing.Statements)
        {
            Variant = _variant?.Description,
        };

        // Where control runs on from one entry point into the next, both
        // methods hold the statements after the second, and so their calls.
        return new EmittedUnit(CSharpFileWriter.Write(file), _methods, _unaligned, [.. _widened.Distinct()]) { File = file };
    }

    // The statement that names the method, where there is one, gives the
    // comments before it; the last method of the unit is given those before
    // its END.
    private void WriteMethod(
        List<TreeMethod> methods,
        ParsedStatement? heading,
        string name,
        IReadOnlyList<string> parameters,
        IReadOnlyList<Construct> statements,
        bool isFunction,
        bool isLast)
    {
        _body = new StatementBuilder();
        _preamble = new StatementBuilder();
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

        // Methods are a blank line apart already, and a comment before one
        // is about it, so blank lines around such comments are left out.
        var comments = new StatementBuilder();
        if (heading is not null)
        {
            WriteComments(comments, WithoutTrailingBlanks(CommentsOf(heading)));
        }

        // The parameters are named before what stands at the top is worked
        // out, since naming one records that the method uses it.
        var returns = _result is null ? "void" : ResultType(_result.Type!.Type);
        string[] signature = [.. Parameters(parameters, _result)];
        var declarations = Declarations();

        // A variant's method is the class's own and is known by its own
        // name in the program's graph of calls, below the method of the
        // procedure's name that calls it; the convention names that one.
        var procedure = _variant?.GetMethodName(name) ?? name;
        methods.Add(new TreeMethod(
            comments.Statements,
            returns,
            _variant is null ? _options.Convention.ProcedureName(name) : procedure,
            signature,
            _preamble.Statements,
            declarations,
            _body.Statements)
        {
            Procedure = procedure,
            EntryPoint = name,
            ScalarArguments = [.. ScalarArguments(parameters)],
            SavedScalars = [.. SavedScalars()],
            IsPublic = _variant is null,
        });
        _methods.Add(new EmittedMethod(
            procedure,
            _stackBytes,
            [.. _calls.Select(call => new EmittedCall(call.Key, [.. call.Value.Passes], call.Value.PassesOnArgument))],
            _callsThroughArgument));
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

    // The arguments of a method that are one number or logical value each:
    // not an array, not a character value, not a procedure. A pass may keep
    // such a one in a local.
    private IEnumerable<ScalarArgument> ScalarArguments(IReadOnlyList<string> names)
    {
        for (var position = 0; position < names.Count; position++)
        {
            var symbol = _table.Find(names[position])!;
            if (!IsProcedure(symbol) && !symbol.IsArray && symbol.Type!.Type != DataType.Character)
            {
                yield return new ScalarArgument(position, names[position].ToCSharpLocal(), symbol.Type.Type.ToCSharpStorageType());
            }
        }
    }

    // The variables the unit keeps between calls that are one number or
    // logical value each and that share their storage with no other. A
    // pass may keep such a one in a local while a method runs.
    private IEnumerable<SavedScalar> SavedScalars() => _table.Symbols
        .Where(symbol => symbol is { Kind: SymbolKind.Variable, IsDummyArgument: false, IsEntryPoint: false, IsArray: false }
            && symbol.Type!.Type != DataType.Character
            && IsKept(symbol)
            && !_overlayOf.ContainsKey(symbol.Name))
        .Select(symbol => new SavedScalar(symbol.Name.ToCSharpLocal(), symbol.Type!.Type.ToCSharpStorageType()));

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

    // What stands at the top of a method, before its statements: the
    // constants it names, its kept variables, its locals and what it takes
    // from the stack, and the local functions it refers to.
    private List<TreeStatement> Declarations()
    {
        var file = new List<TreeStatement>();
        foreach (var constant in _constantsUsed)
        {
            var type = ValueType(constant.Type!.Type);
            file.Add(new TreeStatement.Declare(type, constant.Name.ToCSharpLocal(), new TreeExpression.Number(_evaluator.ValueOf(constant)), IsConstant: true));
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
                file.Add(new TreeStatement.Declare(type, name, Zero));
                continue;
            }

            // A character variable is so many bytes, and an array of them so
            // many times that, end to end.
            long count = isText ? ConstantLengthOf(symbol) : 1;
            foreach (var bound in symbol.Dimensions)
            {
                count *= ConstantExtent(bound) ?? throw Unsupported("a local array whose size is not constant");
            }

            file.Add(new TreeStatement.Declare(type + "*", name, TakeFromStack(type, count, ElementCount(symbol) * ElementSize(symbol))));
        }

        WriteScratch(file);
        file.AddRange(_localFunctions);
        return file;
    }

    // The places for made character values and the locals for passing
    // values that the statements of a method, or the expression of a
    // statement function, have taken.
    private void WriteScratch(List<TreeStatement> file)
    {
        for (var number = 1; number <= _buffers.Count; number++)
        {
            var name = string.Create(CultureInfo.InvariantCulture, $"_s{number}");
            file.Add(new TreeStatement.Declare("byte*", name, TakeFromStack("byte", _buffers[number - 1], _buffers[number - 1])));
        }

        foreach (var (type, (_, most)) in _slots.OrderBy(slot => slot.Key))
        {
            for (var number = 1; number <= most; number++)
            {
                file.Add(new TreeStatement.Declare(type.ToCSharpStorageType(), Slot(type, number), Zero));
            }
        }
    }

    // A stray argument is a local that holds zero or blanks: an array has
    // one element, text of assumed length one character, and a procedure
    // is no address at all. The statement
    // that names it then does something harmless, where the original would
    // follow an address nobody set.
    private void WriteStray(List<TreeStatement> file, Symbol symbol)
    {
        var name = symbol.Name.ToCSharpLocal();
        if (IsProcedure(symbol))
        {
            file.Add(new TreeStatement.Declare(OwnProcedureType(symbol), name, new TreeExpression.Null()));
            return;
        }

        if (symbol.Type!.Type != DataType.Character)
        {
            var type = symbol.Type.Type.ToCSharpStorageType();
            file.Add(new TreeStatement.Declare(type + "*", name, TakeFromStack(type, 1, symbol.Type.Type.GetStorageSize(), isZeroed: true)));
            return;
        }

        var length = One;
        if (symbol.Type.Length is { IsAssumed: true })
        {
            file.Add(new TreeStatement.Declare("int", LengthName(symbol), One));
        }
        else
        {
            length = LengthNodeOf(symbol);
        }

        var bytes = ((TreeExpression.Integer)length).Value;
        file.Add(new TreeStatement.Declare("byte*", name, TakeFromStack("byte", bytes, bytes)));

        var whole = new TreeExpression.New("Span<byte>", [new TreeExpression.Local(name), length]);
        var blank = new TreeExpression.Converted("byte", new TreeExpression.Character(' '));
        file.Add(new TreeStatement.Evaluate(new TreeExpression.MemberCall(whole, "Fill", [blank])));
    }

    // Takes so many elements of a type from the stack for the method being
    // written, and counts their bytes against it.
    private TreeExpression.StackAllocation TakeFromStack(string type, long count, long bytes, bool isZeroed = false)
    {
        _stackBytes += bytes;
        return new TreeExpression.StackAllocation(type, count, isZeroed);
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
    }
}
