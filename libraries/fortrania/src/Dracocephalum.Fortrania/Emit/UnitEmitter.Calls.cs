using Dracocephalum.Fortrania.Analysis;
using Dracocephalum.Fortrania.Syntax;

namespace Dracocephalum.Fortrania.Emit;

/// <content>Writes calls of subroutines and references to functions, the language's own included.</content>
internal sealed partial class UnitEmitter
{
    // A reference to a function of the program, as a value.
    private Code Call(Symbol function, IReadOnlyList<Expression> arguments)
    {
        // The type is the one the calling unit gives the function; the two
        // agree, or the call would not have passed the check across units.
        var type = function.Type!.Type;
        if (type == DataType.Character)
        {
            return TextCall(function, arguments);
        }

        var invocation = Invocation(function.Name, arguments);

        return type == DataType.Logical && _options.Convention.ReturnsLogicalAsInteger
            ? new Code($"{invocation} != 0", type, Level.Equality)
            : new Code(invocation, type, Level.Primary);
    }

    // A call of a procedure of the program. Each argument is an address;
    // where the procedure declares another type than the caller passes,
    // the pointer is cast, which is what passing by address amounts to. A
    // character argument is an address and a length, and the convention
    // says where the length goes.
    private string Invocation(string name, IReadOnlyList<Expression> arguments, IEnumerable<string>? leading = null)
    {
        _callsProcedure = true;

        // A dummy procedure is whatever was passed, and not the procedure
        // of the program that happens to have its name.
        var called = _table.Find(name);
        var through = called is { IsDummyArgument: true };

        // A procedure of the compiler's library is the context's, and takes
        // each length straight after its address whatever the convention.
        var system = through ? null : SystemMethod(name);
        var procedure = through ? null : system is null ? FindAnywhere(name) : SystemProcedures[name].Interface;
        var addresses = new List<string>(leading ?? []);
        var lengths = new List<string>();
        var lengthsComeLast = system is null && _options.Convention.TextLengthsComeLast;
        var passes = new List<string>();
        var passesOnArgument = false;

        for (var index = 0; index < arguments.Count; index++)
        {
            var dummy = procedure is not null && index < procedure.Arguments.Count ? procedure.Arguments[index] : null;
            if (arguments[index] is Expression.Name named && _table.Find(named.Identifier) is { } passed && IsProcedure(passed))
            {
                if (passed.IsDummyArgument)
                {
                    passesOnArgument = true;
                }
                else
                {
                    passes.Add(passed.Name);
                }

                addresses.Add(PassedProcedure(passed, dummy));
                continue;
            }

            if (dummy is { Kind: not (DummyArgumentKind.Variable or DummyArgumentKind.Array) })
            {
                throw Unsupported("a value passed where a procedure is taken");
            }

            var (address, length) = Argument(arguments[index], dummy?.Type);
            addresses.Add(address);
            if (length is not null)
            {
                (lengthsComeLast ? lengths : addresses).Add(length);
            }
        }

        if (through)
        {
            _callsThroughArgument = true;
            return CallThrough(called!, arguments, addresses.Concat(lengths));
        }

        if (system is not null)
        {
            return $"_context.{system}({string.Join(", ", addresses)})";
        }

        RecordCall(name, passes, passesOnArgument);

        // A procedure of another library is called on that library's
        // instance, which this one finds in the context they share.
        var owner = OwnerOf(name) is { } other ? other.Accessor + "." : string.Empty;
        return $"{owner}{_options.Convention.ProcedureName(name)}({string.Join(", ", addresses.Concat(lengths))})";
    }

    // Adds a call to what the method being written calls, joined with the
    // method's other calls of the same procedure.
    private void RecordCall(string name, List<string> passes, bool passesOnArgument)
    {
        if (!_calls.TryGetValue(name, out var known))
        {
            known = (new SortedSet<string>(StringComparer.Ordinal), false);
        }

        known.Passes.UnionWith(passes);
        _calls[name] = (known.Passes, known.PassesOnArgument || passesOnArgument);
    }

    private (string Address, string? Length) Argument(Expression argument, DataType? expected)
    {
        switch (argument)
        {
            // A character variable or a whole character array is its own
            // address already, with the length of one element.
            case Expression.Name name when _table.Find(name.Identifier) is { Kind: SymbolKind.Variable, Type.Type: DataType.Character } text:
                return (IsOwnArgument(text) ? text.Name.ToCSharpLocal() : Local(text), LengthOf(text));

            // An argument of a statement function is a value of the local
            // function's own, and has an address there.
            case Expression.Name name when _table.Find(name.Identifier) is { Kind: SymbolKind.Variable } own && IsOwnArgument(own):
                if (own.Type!.Type == DataType.Logical)
                {
                    throw Unsupported("a logical argument of a statement function passed on to a procedure");
                }

                return (Cast($"&{own.Name.ToCSharpLocal()}", own.Type.Type, expected), null);

            case Expression.Name name when _table.Find(name.Identifier) is { Kind: SymbolKind.Variable } variable:
                var local = Local(variable);
                var address = variable.IsArray || variable.IsDummyArgument || IsPlaced(variable) ? local : $"&{local}";
                return (Cast(address, variable.Type!.Type, expected), null);

            case Expression.Name name when _table.Find(name.Identifier)!.Kind != SymbolKind.Constant:
                throw Unsupported("a procedure as an argument");

            case Expression.Reference reference when _table.Find(reference.Identifier) is { Kind: SymbolKind.Variable } array:
                var element = Element(array, reference.Arguments);
                return element.Type == DataType.Character
                    ? (element.Text, element.Length)
                    : (Cast($"&{element.Text}", element.Type, expected), null);

            default:
                var value = Value(argument);
                return value.Type == DataType.Character ? (value.Text, value.Length) : (Passed(value, expected), null);
        }
    }

    private static string Cast(string address, DataType actual, DataType? expected) =>
        expected is { } type && type != DataType.Character && type.ToCSharpStorageType() != actual.ToCSharpStorageType()
            ? $"({type.ToCSharpStorageType()}*){address}"
            : address;

    // A value with no address of its own is stored in a local set aside
    // for it and that local's address passed. The local is as large as the
    // larger of what is passed and what the procedure declares, so that
    // the procedure reads and writes inside it whichever it takes it for.
    private string Passed(Code value, DataType? expected)
    {
        var actual = value.Type == DataType.Logical ? DataType.Integer : value.Type;
        var declared = expected is null or DataType.Character or DataType.Complex ? actual : expected.Value;
        var slot = TakeSlot(declared.GetStorageSize() > actual.GetStorageSize() ? declared : actual);

        var address = Cast($"&{slot}", declared.GetStorageSize() > actual.GetStorageSize() ? declared : actual, actual);
        var stored = $"{Runtime("Arg")}({address}, {Stored(value, value.Type)})";
        return Cast(stored, actual, declared);
    }

    // A function the language supplies. The generic name and its specific
    // names come to the same call; C# picks the overload by the type of the
    // argument, as the generic name does.
    private Code Intrinsic(Expression.Reference reference)
    {
        // LEN alone takes a whole array, and asks nothing of its value.
        if (reference.Identifier == "LEN")
        {
            // Nor is a concatenation joined to be measured.
            var length = reference.Arguments[0] is Expression.Name name && _table.Find(name.Identifier) is { Kind: SymbolKind.Variable } measured
                ? LengthOf(measured)
                : IsConcatenation(reference.Arguments[0])
                    ? JoinedLength(reference.Arguments[0])
                    : Value(reference.Arguments[0]).Length!;
            return new Code(Grouped(length), DataType.Integer, Level.Primary);
        }

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
                return new Code($"{Runtime("Nint")}({first.Text})", DataType.Integer, Level.Primary);

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
                    ? new Code($"{Runtime("Sign")}({list})", first.Type, Level.Primary)
                    : new Code($"{library}.CopySign({list})", first.Type, Level.Primary);

            case "DIM" or "IDIM" or "DDIM":
                return new Code($"{Runtime("Dim")}({list})", first.Type, Level.Primary);

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
            case "ISHFT":
                return new Code($"{Runtime("Ishft")}({first.Text}, {arguments[1].Text})", DataType.Integer, Level.Primary);
            case "NOT":
                return new Code($"~{first.At(Level.Unary)}", DataType.Integer, Level.Unary);

            // The code of a character is its byte, and the character of a
            // code is one byte put where it has an address. The byte is
            // widened where it is read, so that what takes the code sees
            // an integer whatever it is: C# widens it in arithmetic, and
            // not where it chooses between overloads.
            case "ICHAR":
                return new Code($"(int){first.At(Level.Primary)}[0]", DataType.Integer, Level.Unary);
            case "CHAR":
                return new Code($"{Runtime("Arg")}(&{TakeSlot(DataType.Character)}, {first.Text})", DataType.Character, Level.Primary, "1");

            case "INDEX":
                return new Code(
                    $"{Runtime("Index")}({first.Text}, {first.Length}, {arguments[1].Text}, {arguments[1].Length})",
                    DataType.Integer,
                    Level.Primary);

            // The collating sequence these four ask for is ASCII, which is
            // the order of the bytes.
            case "LGE":
                return new Code($"{Compared(first, arguments[1])} >= 0", DataType.Logical, Level.Relational);
            case "LGT":
                return new Code($"{Compared(first, arguments[1])} > 0", DataType.Logical, Level.Relational);
            case "LLE":
                return new Code($"{Compared(first, arguments[1])} <= 0", DataType.Logical, Level.Relational);
            case "LLT":
                return new Code($"{Compared(first, arguments[1])} < 0", DataType.Logical, Level.Relational);

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
        : new Code($"({type.ToCSharpStorageType()}){value.At(Level.Unary)}", type, Level.Unary);
}
