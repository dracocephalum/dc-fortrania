using System.Globalization;
using Dracocephalum.Fortrania.Analysis;
using Dracocephalum.Fortrania.Syntax;

namespace Dracocephalum.Fortrania.Emit;

/// <content>
/// Writes what a program unit keeps between calls. Kept are the variables
/// the unit declares <c>SAVE</c> or initializes with <c>DATA</c>, and local
/// arrays too large for the stack, which may be kept because the language
/// has no recursion. They lie in one block of the instance's memory per
/// unit, handed out on the first call and given the <c>DATA</c> values
/// then. Inside the method each is a pointer into the block, and from there
/// on is written exactly as a dummy argument is.
/// </content>
internal sealed partial class UnitEmitter
{
    // Where each kept variable lies in the unit's block, by its name.
    private readonly Dictionary<string, int> _offsets = new(StringComparer.Ordinal);
    private int _stateSize;

    private bool IsKept(Symbol symbol) => _offsets.ContainsKey(symbol.Name);

    private string StateField => "_state_" + UnitName;

    private string StateLoader => "_load_" + UnitName;

    private string UnitName => _table.Unit.Header!.Statement.ProcedureHeading().Name;

    // Places the kept variables one after another, each at a multiple of
    // the size of its elements.
    private void LayOutState()
    {
        var initialized = new HashSet<string>(StringComparer.Ordinal);
        Roots(_table.Data.SelectMany(group => group.Targets), initialized);

        foreach (var symbol in _table.Symbols.Where(symbol => symbol is { Kind: SymbolKind.Variable, IsDummyArgument: false, IsEntryPoint: false }))
        {
            _line = symbol.Line;
            var bytes = ElementCount(symbol) * ElementSize(symbol);
            var isKept = symbol.IsSaved || _table.SavesEverything || initialized.Contains(symbol.Name) || bytes > StackLimit;
            if (!isKept)
            {
                continue;
            }

            if (_stateSize + bytes > int.MaxValue - 16)
            {
                throw Unsupported("kept variables of two gigabytes or more in one program unit");
            }

            var alignment = Math.Min(ElementSize(symbol), 8);
            _stateSize = (int)((_stateSize + alignment - 1) / alignment * alignment);
            _offsets[symbol.Name] = _stateSize;
            _stateSize += (int)bytes;
        }

        _stateSize = (_stateSize + 7) / 8 * 8;
    }

    private static void Roots(IEnumerable<DataTarget> targets, HashSet<string> names)
    {
        foreach (var target in targets)
        {
            switch (target)
            {
                case DataTarget.Variable variable:
                    names.Add(Root(variable.Designator));
                    break;
                case DataTarget.ImpliedDo loop:
                    Roots(loop.Targets, names);
                    break;
            }
        }
    }

    private long ElementCount(Symbol symbol)
    {
        long count = 1;
        foreach (var bound in symbol.Dimensions)
        {
            count *= ConstantExtent(bound) ?? throw Unsupported("a local array whose size is not constant");
        }

        return count;
    }

    // The bytes one element takes: for character data its length.
    private long ElementSize(Symbol symbol) => symbol.Type!.Type == DataType.Character
        ? long.Parse(LengthOf(symbol), CultureInfo.InvariantCulture)
        : symbol.Type.Type.GetStorageSize();

    private string KeptDeclaration(Symbol symbol)
    {
        var type = symbol.Type!.Type.ToCSharpStorageType();
        var name = symbol.Name.ToCSharpLocal();
        var offset = _offsets[symbol.Name].ToString(CultureInfo.InvariantCulture);
        return type == "byte" ? $"byte* {name} = _state + {offset};" : $"{type}* {name} = ({type}*)(_state + {offset});";
    }

    // The field that holds the unit's block, and the method that hands the
    // block out on the first call and stores the DATA values in it. The
    // method is kept out of line, so that every later call pays for a load
    // and a test and nothing more.
    private void WriteState(CodeWriter file)
    {
        if (_stateSize == 0)
        {
            return;
        }

        file.Line($"private byte* {StateField};");
        file.Blank();
        file.Line("[MethodImpl(MethodImplOptions.NoInlining)]");
        file.Open($"private byte* {StateLoader}()");
        file.Line(string.Create(CultureInfo.InvariantCulture, $"byte* _state = _memory.Allocate({_stateSize});"));

        var data = new CodeWriter();
        var named = new List<Symbol>();
        WriteData(data, named);
        foreach (var symbol in named)
        {
            file.Line(KeptDeclaration(symbol));
        }

        file.Lines(data);
        file.Line($"{StateField} = _state;");
        file.Line("return _state;");
        file.Close();
        file.Blank();
    }

    // The lines that open a method of a unit with kept variables.
    private void WriteStateAccess(CodeWriter file)
    {
        if (_stateSize == 0)
        {
            return;
        }

        file.Line($"byte* _state = {StateField};");
        file.Open("if (_state == null)");
        file.Line($"_state = {StateLoader}();");
        file.Close();

        foreach (var symbol in _table.Symbols.Where(IsKept))
        {
            file.Line(KeptDeclaration(symbol));
        }
    }

    // One element, or part of one, that a DATA statement gives a value.
    private readonly record struct Initialized(Symbol Variable, long Element, int? First, int? Length, Constant Value);

    private void WriteData(CodeWriter data, List<Symbol> named)
    {
        _line = (_table.Unit.Header ?? _table.Unit.End).Source.Line;

        foreach (var group in _table.Data)
        {
            var places = new List<(Symbol Variable, long Element, int? First, int? Length)>();
            Expand(group.Targets, [], places);

            var values = new List<Constant>(places.Count);
            foreach (var value in group.Values)
            {
                var times = value.Repeat is null ? 1 : Whole(value.Repeat, []);
                var constant = _evaluator.TryEvaluate(value.Value, _line)
                    ?? throw Unsupported("a DATA value that is not a constant");
                values.AddRange(Enumerable.Repeat(constant, times));
            }

            if (values.Count != places.Count)
            {
                throw Unsupported("a DATA statement with a different number of values than of variables");
            }

            var items = places.Select((place, index) => new Initialized(place.Variable, place.Element, place.First, place.Length, values[index])).ToList();

            // Elements that follow one another and take the same value are
            // written together.
            for (var start = 0; start < items.Count;)
            {
                var end = start + 1;
                while (end < items.Count && Follows(items[end - 1], items[end]))
                {
                    end++;
                }

                WriteRun(data, items[start], end - start, named);
                start = end;
            }
        }
    }

    private static bool Follows(Initialized last, Initialized next) =>
        next.Variable == last.Variable
        && next.Element == last.Element + 1
        && last.First is null
        && next.First is null
        && next.Value == last.Value;

    private void WriteRun(CodeWriter data, Initialized first, int count, List<Symbol> named)
    {
        var type = first.Variable.Type!.Type;
        var name = first.Variable.Name.ToCSharpLocal();

        // The block is zero when handed out, so a zero need not be stored.
        if (first.Value is Constant.Integer { Value: 0 } or Constant.Real { Value: 0f } or Constant.DoublePrecision { Value: 0.0 } or Constant.Logical { Value: false }
            && type != DataType.Character
            && !IsNegativeZero(first.Value))
        {
            return;
        }

        if (!named.Contains(first.Variable))
        {
            named.Add(first.Variable);
        }

        var element = first.Element.ToString(CultureInfo.InvariantCulture);
        var times = count.ToString(CultureInfo.InvariantCulture);

        if (type == DataType.Character)
        {
            if (first.Value is not Constant.Character)
            {
                throw Unsupported("a DATA value of another type than its variable");
            }

            var value = Text(((Constant.Character)first.Value).Value);
            var length = ElementSize(first.Variable).ToString(CultureInfo.InvariantCulture);

            if (first.First is { } position)
            {
                var address = string.Create(CultureInfo.InvariantCulture, $"{name} + {(first.Element * ElementSize(first.Variable)) + position - 1}");
                data.Line(string.Create(CultureInfo.InvariantCulture, $"{Runtime("Assign")}({address}, {first.Length}, {value.Text}, {value.Length});"));
            }
            else if (count == 1)
            {
                var address = first.Element == 0
                    ? name
                    : string.Create(CultureInfo.InvariantCulture, $"{name} + {first.Element * ElementSize(first.Variable)}");
                data.Line($"{Runtime("Assign")}({address}, {length}, {value.Text}, {value.Length});");
            }
            else
            {
                data.Open(string.Create(CultureInfo.InvariantCulture, $"for (var _k = {first.Element}; _k < {first.Element + count}; _k++)"));
                data.Line($"{Runtime("Assign")}({name} + _k * {length}, {length}, {value.Text}, {value.Length});");
                data.Close();
            }

            return;
        }

        var stored = Initial(first.Value, type);
        if (count < 4)
        {
            for (var index = 0; index < count; index++)
            {
                data.Line(string.Create(CultureInfo.InvariantCulture, $"{name}[{first.Element + index}] = {stored};"));
            }
        }
        else
        {
            var storage = type.ToCSharpStorageType();
            var address = first.Element == 0 ? name : $"{name} + {element}";
            data.Line($"new Span<{storage}>({address}, {times}).Fill({stored});");
        }
    }

    private static bool IsNegativeZero(Constant value) => value switch
    {
        Constant.Real real => float.IsNegative(real.Value),
        Constant.DoublePrecision number => double.IsNegative(number.Value),
        _ => false,
    };

    // A DATA value as it is stored in a variable of the given type, with
    // the conversion an assignment would make.
    private string Initial(Constant value, DataType type)
    {
        Constant converted = (type, value) switch
        {
            (DataType.Logical, Constant.Logical logical) => new Constant.Integer(logical.Value ? 1 : 0),
            (DataType.Integer, Constant.Integer) => value,
            (DataType.Integer, Constant.Real real) => new Constant.Integer((int)real.Value),
            (DataType.Integer, Constant.DoublePrecision number) => new Constant.Integer((int)number.Value),
            (DataType.Real, Constant.Integer integer) => new Constant.Real(integer.Value),
            (DataType.Real, Constant.Real) => value,
            (DataType.Real, Constant.DoublePrecision number) => new Constant.Real((float)number.Value),
            (DataType.DoublePrecision, Constant.Integer integer) => new Constant.DoublePrecision(integer.Value),
            (DataType.DoublePrecision, Constant.Real real) => new Constant.DoublePrecision(real.Value),
            (DataType.DoublePrecision, Constant.DoublePrecision) => value,
            _ => throw Unsupported("a DATA value of another type than its variable"),
        };

        return converted.ToCSharpLiteral();
    }

    // Lists the elements a DATA statement names, in the order its values
    // are given: a whole array is all its elements, and an implied loop is
    // run here, with its variable taking each value in turn.
    private void Expand(
        IReadOnlyList<DataTarget> targets,
        Dictionary<string, int> loopVariables,
        List<(Symbol Variable, long Element, int? First, int? Length)> places)
    {
        foreach (var target in targets)
        {
            switch (target)
            {
                case DataTarget.ImpliedDo loop:
                    var first = Whole(loop.First, loopVariables);
                    var last = Whole(loop.Last, loopVariables);
                    var step = loop.Step is null ? 1 : Whole(loop.Step, loopVariables);
                    if (step == 0)
                    {
                        throw Unsupported("a DATA implied loop with a step of zero");
                    }

                    for (var value = first; step > 0 ? value <= last : value >= last; value += step)
                    {
                        loopVariables[loop.ControlVariable] = value;
                        Expand(loop.Targets, loopVariables, places);
                    }

                    loopVariables.Remove(loop.ControlVariable);
                    break;

                case DataTarget.Variable { Designator: Expression.Substring substring }:
                    var (variable, element) = Place(substring.Target, loopVariables);
                    var from = substring.First is null ? 1 : Whole(substring.First, loopVariables);
                    var to = substring.Last is null ? (int)ElementSize(variable) : Whole(substring.Last, loopVariables);
                    places.Add((variable, element, from, to - from + 1));
                    break;

                case DataTarget.Variable { Designator: Expression.Name name } when _table.Find(name.Identifier) is { IsArray: true } array:
                    for (long index = 0, count = ElementCount(array); index < count; index++)
                    {
                        places.Add((array, index, null, null));
                    }

                    break;

                case DataTarget.Variable variableTarget:
                    var place = Place(variableTarget.Designator, loopVariables);
                    places.Add((place.Variable, place.Element, null, null));
                    break;
            }
        }
    }

    // A variable, or the element of an array its subscripts select.
    private (Symbol Variable, long Element) Place(Expression designator, Dictionary<string, int> loopVariables)
    {
        if (designator is Expression.Name name)
        {
            return (_table.Find(name.Identifier)!, 0);
        }

        var reference = (Expression.Reference)designator;
        var array = _table.Find(reference.Identifier)!;
        long element = 0;
        long stride = 1;
        for (var dimension = 0; dimension < reference.Arguments.Count; dimension++)
        {
            var bound = array.Dimensions[dimension];
            var lower = ConstantBound(bound.Lower, 1) ?? throw Unsupported("a local array whose size is not constant");
            element += (Whole(reference.Arguments[dimension], loopVariables) - lower) * stride;
            stride *= ConstantExtent(bound) ?? 1;
        }

        return (array, element);
    }

    // The value of an integer expression made of constants and the
    // variables of the implied loops around it.
    private int Whole(Expression expression, Dictionary<string, int> loopVariables)
    {
        switch (expression)
        {
            case Expression.Name name when loopVariables.TryGetValue(name.Identifier, out var value):
                return value;
            case Expression.Parenthesized parenthesized:
                return Whole(parenthesized.Inner, loopVariables);
            case Expression.Unary { Operator: UnaryOperator.Minus } negated:
                return -Whole(negated.Operand, loopVariables);
            case Expression.Unary { Operator: UnaryOperator.Plus } same:
                return Whole(same.Operand, loopVariables);
            case Expression.Binary { Operator: BinaryOperator.Add or BinaryOperator.Subtract or BinaryOperator.Multiply or BinaryOperator.Divide } binary:
                var left = Whole(binary.Left, loopVariables);
                var right = Whole(binary.Right, loopVariables);
                return binary.Operator switch
                {
                    BinaryOperator.Add => left + right,
                    BinaryOperator.Subtract => left - right,
                    BinaryOperator.Multiply => left * right,
                    _ => left / right,
                };
            default:
                return Known(expression) ?? throw Unsupported("a DATA subscript or count that is not plain integer arithmetic");
        }
    }
}
