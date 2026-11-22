using Dracocephalum.Fortrania.Analysis;
using Dracocephalum.Fortrania.Syntax;

namespace Dracocephalum.Fortrania.Emit;

/// <content>
/// Turns calls of subroutines and references to functions, the language's
/// own included, into the program tree.
/// </content>
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

        var invocation = InvocationNode(function.Name, arguments);

        return type == DataType.Logical && _options.Convention.ReturnsLogicalAsInteger
            ? new Code(new TreeExpression.Binary(TreeExpression.Operation.NotEqual, invocation, Zero), type)
            : new Code(invocation, type);
    }

    private string Invocation(string name, IReadOnlyList<Expression> arguments) =>
        CSharpExpressionWriter.Write(InvocationNode(name, arguments));

    // A call of a procedure of the program. Each argument is an address;
    // where the procedure declares another type than the caller passes,
    // the pointer is cast, which is what passing by address amounts to. A
    // character argument is an address and a length, and the convention
    // says where the length goes.
    private TreeExpression InvocationNode(string name, IReadOnlyList<Expression> arguments, IEnumerable<TreeExpression>? leading = null)
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

        var addresses = new List<TreeExpression>(leading ?? []);
        var lengths = new List<TreeExpression>();
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

            // The pointer cast to double lands on a multiple of 4, which
            // only some of the processors .NET runs on read at full speed
            // or at all for an 8-byte value.
            if (dummy?.Type == DataType.DoublePrecision
                && FindPassedVariable(arguments[index]) is { Type.Type: DataType.Integer or DataType.Real or DataType.Logical } variable)
            {
                _widened.Add(new WidenedArgument(variable.Name, name, index + 1, _line));
            }

            var (address, length) = ArgumentNode(arguments[index], dummy?.Type);
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
            return new TreeExpression.Call($"_context.{system}", addresses);
        }

        RecordCall(name, passes, passesOnArgument);

        // A procedure of another library is called on that library's
        // instance, which this one finds in the context they share.
        var owner = OwnerOf(name) is { } other ? other.Accessor + "." : string.Empty;
        return new TreeExpression.Call($"{owner}{_options.Convention.ProcedureName(name)}", [.. addresses, .. lengths]);
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
        var (address, length) = ArgumentNode(argument, expected);
        return (CSharpExpressionWriter.Write(address), length is null ? null : CSharpExpressionWriter.Write(length));
    }

    private (TreeExpression Address, TreeExpression? Length) ArgumentNode(Expression argument, DataType? expected)
    {
        switch (argument)
        {
            // A character variable or a whole character array is its own
            // address already, with the length of one element.
            case Expression.Name name when _table.Find(name.Identifier) is { Kind: SymbolKind.Variable, Type.Type: DataType.Character } text:
                return (new TreeExpression.Local(IsOwnArgument(text) ? text.Name.ToCSharpLocal() : Local(text)), LengthNodeOf(text));

            // An argument of a statement function is a value of the local
            // function's own, and has an address there.
            case Expression.Name name when _table.Find(name.Identifier) is { Kind: SymbolKind.Variable } own && IsOwnArgument(own):
                if (own.Type!.Type == DataType.Logical)
                {
                    throw Unsupported("a logical argument of a statement function passed on to a procedure");
                }

                return (Cast(new TreeExpression.AddressOf(new TreeExpression.Local(own.Name.ToCSharpLocal())), own.Type.Type, expected), null);

            case Expression.Name name when _table.Find(name.Identifier) is { Kind: SymbolKind.Variable } variable:
                TreeExpression local = new TreeExpression.Local(Local(variable));
                var address = variable.IsArray || variable.IsDummyArgument || IsPlaced(variable) ? local : new TreeExpression.AddressOf(local);
                return (Cast(address, variable.Type!.Type, expected), null);

            case Expression.Name name when _table.Find(name.Identifier)!.Kind != SymbolKind.Constant:
                throw Unsupported("a procedure as an argument");

            case Expression.Reference reference when _table.Find(reference.Identifier) is { Kind: SymbolKind.Variable } array:
                var element = Element(array, reference.Arguments);
                return element.Type == DataType.Character
                    ? (element.Node, element.LengthNode)
                    : (Cast(new TreeExpression.AddressOf(element.Node), element.Type, expected), null);

            default:
                var value = Value(argument);
                return value.Type == DataType.Character ? (value.Node, value.LengthNode) : (Passed(value, expected), null);
        }
    }

    // The variable whose own storage an argument passes: a variable named,
    // or the array an element names. Any other argument is a value, passed
    // in a local of the larger of the two types.
    private Symbol? FindPassedVariable(Expression argument) => argument switch
    {
        Expression.Name name => _table.Find(name.Identifier) is { Kind: SymbolKind.Variable } variable ? variable : null,
        Expression.Reference reference => _table.Find(reference.Identifier) is { Kind: SymbolKind.Variable } array ? array : null,
        _ => null,
    };

    private static TreeExpression Cast(TreeExpression address, DataType actual, DataType? expected) =>
        expected is { } type && type != DataType.Character && type.ToCSharpStorageType() != actual.ToCSharpStorageType()
            ? new TreeExpression.Converted($"{type.ToCSharpStorageType()}*", address)
            : address;

    // A value with no address of its own is stored in a local set aside
    // for it and that local's address passed. The local is as large as the
    // larger of what is passed and what the procedure declares, so that
    // the procedure reads and writes inside it whichever it takes it for.
    private TreeExpression Passed(Code value, DataType? expected)
    {
        var actual = value.Type == DataType.Logical ? DataType.Integer : value.Type;
        var declared = expected is null or DataType.Character or DataType.Complex ? actual : expected.Value;
        var slot = TakeSlot(declared.GetStorageSize() > actual.GetStorageSize() ? declared : actual);

        var address = Cast(
            new TreeExpression.AddressOf(new TreeExpression.Local(slot)),
            declared.GetStorageSize() > actual.GetStorageSize() ? declared : actual,
            actual);
        var stored = new TreeExpression.Call(Runtime("Arg"), [address, StoredNode(value, value.Type)]);
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
                ? LengthNodeOf(measured)
                : IsConcatenation(reference.Arguments[0])
                    ? JoinedLength(reference.Arguments[0])
                    : Value(reference.Arguments[0]).LengthNode!;
            return new Code(Group(length), DataType.Integer);
        }

        var arguments = reference.Arguments.Select(Value).ToList();
        var first = arguments[0];
        var library = first.Type == DataType.Real ? "MathF" : "Math";
        TreeExpression[] list = [.. arguments.Select(argument => argument.Node)];

        switch (reference.Identifier)
        {
            case "ABS" or "IABS" or "DABS":
                return Called($"{(first.Type == DataType.Integer ? "Math" : library)}.Abs", first.Type, list);

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
                return Called($"{library}.Round", first.Type, first.Node, new TreeExpression.Local("MidpointRounding.AwayFromZero"));

            case "NINT" or "IDNINT":
                return Called(Runtime("Nint"), DataType.Integer, first.Node);

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
                return new Code(new TreeExpression.Binary(TreeExpression.Operation.Remainder, first.Node, arguments[1].Node), first.Type);

            case "SIGN" or "ISIGN" or "DSIGN":
                return first.Type == DataType.Integer
                    ? Called(Runtime("Sign"), first.Type, list)
                    : Called($"{library}.CopySign", first.Type, list);

            case "DIM" or "IDIM" or "DDIM":
                return Called(Runtime("Dim"), first.Type, list);

            case "DPROD":
                return new Code(
                    new TreeExpression.Binary(TreeExpression.Operation.Multiply, new TreeExpression.Converted("double", first.Node), arguments[1].Node),
                    DataType.DoublePrecision);

            case "INT" or "IFIX" or "IDINT":
                return Converted(first, DataType.Integer);
            case "REAL" or "FLOAT" or "SNGL":
                return Converted(first, DataType.Real);
            case "DBLE":
                return Converted(first, DataType.DoublePrecision);

            case "IAND":
                return Bits(TreeExpression.Operation.BitAnd);
            case "IOR":
                return Bits(TreeExpression.Operation.BitOr);
            case "IEOR":
                return Bits(TreeExpression.Operation.BitXor);
            case "ISHFT":
                return Called(Runtime("Ishft"), DataType.Integer, first.Node, arguments[1].Node);
            case "NOT":
                return new Code(new TreeExpression.Unary(TreeExpression.Operation.Complement, first.Node), DataType.Integer);

            // The code of a character is its byte, and the character of a
            // code is one byte put where it has an address. The byte is
            // widened where it is read, so that what takes the code sees
            // an integer whatever it is: C# widens it in arithmetic, and
            // not where it chooses between overloads.
            case "ICHAR":
                return new Code(new TreeExpression.Converted("int", new TreeExpression.Indexed(first.Node, Zero)), DataType.Integer);
            case "CHAR":
                var arg = Runtime("Arg");
                var slot = new TreeExpression.AddressOf(new TreeExpression.Local(TakeSlot(DataType.Character)));
                return new Code(new TreeExpression.Call(arg, [slot, first.Node]), DataType.Character, One);

            case "INDEX":
                return Called(Runtime("Index"), DataType.Integer, first.Node, first.LengthNode!, arguments[1].Node, arguments[1].LengthNode!);

            // The collating sequence these four ask for is ASCII, which is
            // the order of the bytes.
            case "LGE":
                return Ordered(TreeExpression.Operation.GreaterOrEqual);
            case "LGT":
                return Ordered(TreeExpression.Operation.Greater);
            case "LLE":
                return Ordered(TreeExpression.Operation.LessOrEqual);
            case "LLT":
                return Ordered(TreeExpression.Operation.Less);

            default:
                throw Unsupported($"the intrinsic function {reference.Identifier}");
        }

        Code Library(string method) => Called($"{library}.{method}", first.Type, list);

        Code Bits(TreeExpression.Operation operation) =>
            new(new TreeExpression.Grouped(new TreeExpression.Binary(operation, first.Node, arguments[1].Node)), DataType.Integer);

        Code Ordered(TreeExpression.Operation operation) =>
            new(new TreeExpression.Binary(operation, ComparedNode(first, arguments[1]), Zero), DataType.Logical);

        // The greatest or least of two or more, taken a pair at a time.
        Code Extreme(string method, DataType type)
        {
            var extreme = arguments[0].Node;
            foreach (var argument in arguments.Skip(1))
            {
                extreme = new TreeExpression.Call($"Math.{method}", [extreme, argument.Node]);
            }

            return new Code(extreme, type);
        }
    }

    private static Code Called(string method, DataType type, params TreeExpression[] arguments) =>
        new(new TreeExpression.Call(method, arguments), type);

    // A value converted to another numeric type, as INT, REAL and DBLE do.
    // To an integer the fraction is dropped, which is what the cast does.
    private static Code Converted(Code value, DataType type) => value.Type == type
        ? value
        : new Code(new TreeExpression.Converted(type.ToCSharpStorageType(), value.Node), type);
}
