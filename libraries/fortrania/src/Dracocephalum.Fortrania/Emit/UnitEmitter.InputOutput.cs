using System.Globalization;
using Dracocephalum.Fortrania.Analysis;
using Dracocephalum.Fortrania.Syntax;

namespace Dracocephalum.Fortrania.Emit;

/// <content>
/// Writes input and output statements. Each becomes calls on the
/// instance's <c>_io</c>, which alone knows what is behind a unit. A
/// statement that connects, inquires or positions is one call that gives
/// its status. A statement that transfers is a call that begins it, one for
/// each item of its list, and one that ends it and gives the status. The
/// status goes where <c>IOSTAT</c> says; where the statement has none, a
/// failure ends the program, which here is an exception.
/// </content>
internal sealed partial class UnitEmitter
{
    private const string IO = "_io";

    private void WriteInputOutput(Statement statement)
    {
        switch (statement)
        {
            case Statement.Open open:
                WriteOpen(Controls("OPEN", open.Controls, "UNIT", "FILE", "STATUS", "ACCESS", "FORM", "RECL", "IOSTAT"));
                break;

            case Statement.Close close:
                var closing = Controls("CLOSE", close.Controls, "UNIT", "STATUS", "IOSTAT");
                Finish($"{IO}.Close({Unit(closing)}{Specifier(closing, "STATUS", "status")})", closing);
                break;

            case Statement.Inquire inquire:
                WriteInquire(Controls("INQUIRE", inquire.Controls, "UNIT", "FILE", "EXIST", "OPENED", "NUMBER", "NAME", "NAMED", "IOSTAT"));
                break;

            case Statement.Rewind rewind:
                var rewinding = Controls("REWIND", rewind.Controls, "UNIT", "IOSTAT");
                Finish($"{IO}.Rewind({Unit(rewinding)})", rewinding);
                break;

            case Statement.Read read:
                WriteTransfer("READ", read.Controls, read.Items, "ReadDirect", "Get", "EndRead");
                break;

            case Statement.Write write:
                WriteTransfer("WRITE", write.Controls, write.Items, "WriteDirect", "Put", "EndWrite");
                break;

            case Statement.Print:
                throw Unsupported("input or output of text");

            case Statement.Backspace:
                throw Unsupported("BACKSPACE");

            default:
                throw Unsupported("ENDFILE");
        }
    }

    // The control list by keyword. The first item may leave its keyword
    // out and is then the unit, and the second the format.
    private Dictionary<string, Expression?> Controls(string statement, IReadOnlyList<IOControl> controls, params string[] known)
    {
        var byKeyword = new Dictionary<string, Expression?>(StringComparer.Ordinal);
        for (var index = 0; index < controls.Count; index++)
        {
            var keyword = controls[index].Keyword ?? (index == 0 ? "UNIT" : "FMT");
            if (!known.Contains(keyword, StringComparer.Ordinal))
            {
                throw Unsupported($"{statement} with {keyword}");
            }

            byKeyword[keyword] = controls[index].Value;
        }

        return byKeyword;
    }

    private string Unit(Dictionary<string, Expression?> controls) =>
        controls.GetValueOrDefault("UNIT") is { } unit && Value(unit) is { Type: DataType.Integer } number
            ? number.Text
            : throw Unsupported("input or output of text");

    private void WriteOpen(Dictionary<string, Expression?> controls)
    {
        var call = $"{IO}.Open({Unit(controls)}"
            + Specifier(controls, "FILE", "file")
            + Specifier(controls, "STATUS", "status")
            + Specifier(controls, "ACCESS", "access")
            + Specifier(controls, "FORM", "form");

        if (controls.GetValueOrDefault("RECL") is { } length)
        {
            call += $", recl: {Stored(Value(length), DataType.Integer)}";
        }

        Finish(call + ")", controls);
    }

    private void WriteInquire(Dictionary<string, Expression?> controls)
    {
        var byFile = controls.GetValueOrDefault("FILE") is not null;
        if (byFile == controls.ContainsKey("UNIT"))
        {
            throw Unsupported("INQUIRE that names both a file and a unit, or neither");
        }

        var call = byFile ? $"{IO}.InquireFile({Bytes(controls["FILE"]!)}" : $"{IO}.InquireUnit({Unit(controls)}";

        foreach (var (keyword, parameter) in new[] { ("EXIST", "exist"), ("OPENED", "opened"), ("NUMBER", "number"), ("NAMED", "named") })
        {
            if (controls.GetValueOrDefault(keyword) is { } answer)
            {
                call += $", {parameter}: {Argument(answer, null).Address}";
            }
        }

        if (controls.GetValueOrDefault("NAME") is { } name)
        {
            var (address, length) = Argument(name, null);
            call += $", name: {address}, nameLength: {length}";
        }

        Finish(call + ")", controls);
    }

    // A specifier whose value is text, as a named argument; nothing where
    // the statement leaves the specifier out.
    private string Specifier(Dictionary<string, Expression?> controls, string keyword, string parameter) =>
        controls.GetValueOrDefault(keyword) is { } value ? $", {parameter}: {Bytes(value)}" : string.Empty;

    // A character value as a span of bytes: a constant as its literal, and
    // anything else as its address and length.
    private string Bytes(Expression value)
    {
        if (_evaluator.TryEvaluate(value, _line) is Constant.Character constant && constant.Value.All(character => character <= '\u007f'))
        {
            return constant.Value.ToUtf8Literal();
        }

        var (address, length) = Argument(value, null);
        return length is null ? throw Unsupported("a specifier that is not of type CHARACTER") : $"new({address}, {length})";
    }

    private void Finish(string call, Dictionary<string, Expression?> controls)
    {
        _body.Line(controls.GetValueOrDefault("IOSTAT") is { } status
            ? $"{Target(status).Text} = {call};"
            : $"{IO}.Require({call});");
    }

    private void WriteTransfer(string statement, IReadOnlyList<IOControl> controlList, IReadOnlyList<IOItem> items, string begin, string item, string end)
    {
        var controls = Controls(statement, controlList, "UNIT", "FMT", "REC", "IOSTAT");
        if (controls.ContainsKey("FMT"))
        {
            throw Unsupported("input or output of text");
        }

        var unit = Unit(controls);
        if (controls.GetValueOrDefault("REC") is not { } record)
        {
            throw Unsupported("unformatted input or output without a record number");
        }

        _body.Line($"{IO}.{begin}({unit}, {Stored(Value(record), DataType.Integer)});");
        WriteItems(items, item);
        Finish($"{IO}.{end}()", controls);
    }

    private void WriteItems(IReadOnlyList<IOItem> items, string method)
    {
        foreach (var item in items)
        {
            switch (item)
            {
                // A whole array is all its elements, in the order they lie.
                case IOItem.Value { Expression: Expression.Name name } when _table.Find(name.Identifier) is { Kind: SymbolKind.Variable, IsArray: true } array:
                    _body.Line($"{IO}.{method}({Local(array)}, {WholeSize(array)});");
                    break;

                case IOItem.Value value:
                    _typer ??= new ExpressionTyper(_table);
                    var type = _typer.TypeOf(value.Expression, _line, null);
                    var (address, length) = Argument(value.Expression, null);
                    var bytes = length ?? type.GetStorageSize().ToString(CultureInfo.InvariantCulture);
                    _body.Line($"{IO}.{method}({address}, {bytes});");
                    break;

                case IOItem.ImpliedDo loop:
                    _body.Open(CountedLoop(new Statement.Do(null, loop.ControlVariable, loop.First, loop.Last, loop.Step)));
                    WriteItems(loop.Items, method);
                    _body.Close();
                    break;
            }
        }
    }

    // The bytes a whole array takes, which for a dummy argument may be
    // known only when the program runs.
    private string WholeSize(Symbol array)
    {
        long known = array.Type!.Type == DataType.Character ? 1 : array.Type.Type.GetStorageSize();
        var factors = new List<string>();

        if (array.Type.Type == DataType.Character)
        {
            var length = LengthOf(array);
            if (long.TryParse(length, NumberStyles.None, CultureInfo.InvariantCulture, out var constant))
            {
                known *= constant;
            }
            else
            {
                factors.Add(Grouped(length));
            }
        }

        foreach (var bound in array.Dimensions)
        {
            if (ConstantExtent(bound) is { } extent)
            {
                known *= extent;
            }
            else if (bound.Upper is null)
            {
                throw Unsupported("an array of assumed size in an input or output list");
            }
            else
            {
                var upper = Value(bound.Upper);
                factors.Add(bound.Lower is null
                    ? upper.At(Level.Unary)
                    : $"({upper.At(Level.Additive)} - {Value(bound.Lower).At(Level.Multiplicative)} + 1)");
            }
        }

        factors.Insert(0, known.ToString(CultureInfo.InvariantCulture));
        return string.Join(" * ", factors);
    }
}
