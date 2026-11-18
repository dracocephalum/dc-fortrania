using Dracocephalum.Fortrania.Analysis;
using Dracocephalum.Fortrania.Syntax;

namespace Dracocephalum.Fortrania.Emit;

/// <content>Writes calls of subroutines and references to functions, the language's own included.</content>
internal sealed partial class UnitEmitter
{
    // A call of a procedure of the program. Each argument is an address;
    // where the procedure declares another type than the caller passes,
    // the pointer is cast, which is what passing by address amounts to.
    private Code Call(string name, IReadOnlyList<Expression> arguments)
    {
        // The type is the one the calling unit gives the function; the two
        // agree, or the call would not have passed the check across units.
        var type = _table.Find(name)!.Type!.Type;
        var invocation = Invocation(name, arguments);

        return type == DataType.Logical && _options.Convention.ReturnsLogicalAsInteger
            ? new Code($"{invocation} != 0", type, Level.Equality)
            : new Code(invocation, type, Level.Primary);
    }

    private string Invocation(string name, IReadOnlyList<Expression> arguments)
    {
        var procedure = _findProcedure(name);
        var passed = new List<string>(arguments.Count);

        for (var index = 0; index < arguments.Count; index++)
        {
            var dummy = procedure is not null && index < procedure.Arguments.Count ? procedure.Arguments[index] : null;
            if (dummy is { Kind: not (DummyArgumentKind.Variable or DummyArgumentKind.Array) })
            {
                throw Unsupported("a procedure as an argument");
            }

            passed.Add(Argument(arguments[index], dummy?.Type));
        }

        return $"{_options.Convention.ProcedureName(name)}({string.Join(", ", passed)})";
    }

    private string Argument(Expression argument, DataType? expected)
    {
        switch (argument)
        {
            case Expression.Name name when _table.Find(name.Identifier) is { Kind: SymbolKind.Variable } variable:
                var local = CSharpText.Local(variable.Name);
                var address = variable.IsArray || variable.IsDummyArgument ? local : $"&{local}";
                return Cast(address, variable.Type!.Type, expected);

            case Expression.Name name when _table.Find(name.Identifier)!.Kind != SymbolKind.Constant:
                throw Unsupported("a procedure as an argument");

            case Expression.Reference reference when _table.Find(reference.Identifier) is { Kind: SymbolKind.Variable } array:
                return Cast($"&{Element(array, reference.Arguments).Text}", array.Type!.Type, expected);

            default:
                return Passed(Value(argument), expected);
        }
    }

    private static string Cast(string address, DataType actual, DataType? expected) =>
        expected is { } type && CSharpText.Storage(type) != CSharpText.Storage(actual)
            ? $"({CSharpText.Storage(type)}*){address}"
            : address;

    // A value with no address of its own is stored in a local set aside
    // for it and that local's address passed. The local is as large as the
    // larger of what is passed and what the procedure declares, so that
    // the procedure reads and writes inside it whichever it takes it for.
    private string Passed(Code value, DataType? expected)
    {
        var actual = value.Type == DataType.Logical ? DataType.Integer : value.Type;
        var declared = expected is null or DataType.Character or DataType.Complex ? actual : expected.Value;
        var slot = TakeSlot(CSharpText.Size(declared) > CSharpText.Size(actual) ? declared : actual);

        var address = Cast($"&{slot}", CSharpText.Size(declared) > CSharpText.Size(actual) ? declared : actual, actual);
        var stored = $"Fortran.Arg({address}, {Stored(value, actual)})";
        return Cast(stored, actual, declared);
    }

    // A function the language supplies. The generic name and its specific
    // names come to the same call; C# picks the overload by the type of the
    // argument, as the generic name does.
    private Code Intrinsic(Expression.Reference reference)
    {
        var arguments = reference.Arguments.Select(Value).ToList();
        var first = arguments[0];
        var library = first.Type == DataType.Real ? "MathF" : "Math";
        var list = string.Join(", ", arguments.Select(argument => argument.Text));

        switch (reference.Identifier)
        {
            case "ABS" or "IABS" or "DABS":
                return new Code($"{(first.Type == DataType.Integer ? "Math" : library)}.Abs({list})", first.Type, Level.Primary);

            case "SQRT" or "DSQRT":
                return Library("Sqrt");
            case "EXP" or "DEXP":
                return Library("Exp");
            case "LOG" or "ALOG" or "DLOG":
                return Library("Log");
            case "LOG10" or "ALOG10" or "DLOG10":
                return Library("Log10");
            case "SIN" or "DSIN":
                return Library("Sin");
            case "COS" or "DCOS":
                return Library("Cos");
            case "TAN" or "DTAN":
                return Library("Tan");
            case "ASIN" or "DASIN":
                return Library("Asin");
            case "ACOS" or "DACOS":
                return Library("Acos");
            case "ATAN" or "DATAN":
                return Library("Atan");
            case "ATAN2" or "DATAN2":
                return Library("Atan2");
            case "SINH" or "DSINH":
                return Library("Sinh");
            case "COSH" or "DCOSH":
                return Library("Cosh");
            case "TANH" or "DTANH":
                return Library("Tanh");
            case "AINT" or "DINT":
                return Library("Truncate");
            case "ANINT" or "DNINT":
                return new Code($"{library}.Round({first.Text}, MidpointRounding.AwayFromZero)", first.Type, Level.Primary);

            case "NINT" or "IDNINT":
                return new Code($"Fortran.Nint({first.Text})", DataType.Integer, Level.Primary);

            case "MAX" or "MAX0" or "AMAX1" or "DMAX1":
                return Extreme("Max", first.Type);
            case "MIN" or "MIN0" or "AMIN1" or "DMIN1":
                return Extreme("Min", first.Type);
            case "AMAX0":
                return Converted(Extreme("Max", DataType.Integer), DataType.Real);
            case "AMIN0":
                return Converted(Extreme("Min", DataType.Integer), DataType.Real);
            case "MAX1":
                return Converted(Extreme("Max", DataType.Real), DataType.Integer);
            case "MIN1":
                return Converted(Extreme("Min", DataType.Real), DataType.Integer);

            case "MOD" or "AMOD" or "DMOD":
                return new Code(
                    $"{first.At(Level.Multiplicative)} % {arguments[1].At(Level.Unary)}",
                    first.Type,
                    Level.Multiplicative);

            case "SIGN" or "ISIGN" or "DSIGN":
                return first.Type == DataType.Integer
                    ? new Code($"Fortran.Sign({list})", first.Type, Level.Primary)
                    : new Code($"{library}.CopySign({list})", first.Type, Level.Primary);

            case "DIM" or "IDIM" or "DDIM":
                return new Code($"Fortran.Dim({list})", first.Type, Level.Primary);

            case "DPROD":
                return new Code(
                    $"(double){first.At(Level.Unary)} * {arguments[1].At(Level.Unary)}",
                    DataType.DoublePrecision,
                    Level.Multiplicative);

            case "INT" or "IFIX" or "IDINT":
                return Converted(first, DataType.Integer);
            case "REAL" or "FLOAT" or "SNGL":
                return Converted(first, DataType.Real);
            case "DBLE":
                return Converted(first, DataType.DoublePrecision);

            case "IAND":
                return new Code($"({first.At(Level.Unary)} & {arguments[1].At(Level.Unary)})", DataType.Integer, Level.Primary);
            case "IOR":
                return new Code($"({first.At(Level.Unary)} | {arguments[1].At(Level.Unary)})", DataType.Integer, Level.Primary);
            case "IEOR":
                return new Code($"({first.At(Level.Unary)} ^ {arguments[1].At(Level.Unary)})", DataType.Integer, Level.Primary);
            case "NOT":
                return new Code($"~{first.At(Level.Unary)}", DataType.Integer, Level.Unary);

            default:
                throw Unsupported($"the intrinsic function {reference.Identifier}");
        }

        Code Library(string method) => new($"{library}.{method}({list})", first.Type, Level.Primary);

        // The greatest or least of two or more, taken a pair at a time.
        Code Extreme(string method, DataType type)
        {
            var text = arguments[0].Text;
            foreach (var argument in arguments.Skip(1))
            {
                text = $"Math.{method}({text}, {argument.Text})";
            }

            return new Code(text, type, Level.Primary);
        }
    }

    // A value converted to another numeric type, as INT, REAL and DBLE do.
    // To an integer the fraction is dropped, which is what the cast does.
    private static Code Converted(Code value, DataType type) => value.Type == type
        ? value
        : new Code($"({CSharpText.Storage(type)}){value.At(Level.Unary)}", type, Level.Unary);
}
