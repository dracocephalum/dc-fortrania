namespace Dracocephalum.Fortrania.Emit;

/// <summary>
/// The pass that lets a context choose where a method keeps its working
/// buffers. A method that takes buffers from the stack, each by itself,
/// comes out taking them as one piece: from the stack, or from its place
/// in its project's region of the context's arena, by a test on entry.
/// </summary>
/// <remarks>
/// <para>
/// A method with no buffers is left as it is, and so tests nothing; its
/// scalars are ordinary locals in either case. So is a method that has no
/// place, one on a chain of calls that comes back to itself or beneath
/// one, which keeps the stack and says why. What a statement function
/// takes is left on the stack too: it is a local function, a few bytes for
/// a value on its way.
/// </para>
/// <para>
/// A method entered while nothing translated runs, called from outside,
/// finds no copy of the arena open. It calls itself again through a method
/// written beside it, which opens a copy for the call and closes it however
/// the call ends; the method itself has no exception handling, so the
/// compiler treats it as before.
/// </para>
/// <para>
/// <c>DESIGN.md</c>, <em>Where a method keeps its working buffers is the
/// context's to say</em>, has the measurements behind the shape: the test
/// costs nothing that can be measured in a method that works, and one
/// piece costs what several do.
/// </para>
/// </remarks>
internal static class WorkingBuffersPass
{
    /// <summary>The field of a library that holds its project's region.</summary>
    public const string RegionField = "_region";

    private const string Piece = "_locals";
    private const string OnStack = "_onStack";

    /// <summary>Gives the bytes a method's piece takes: what its buffers take together, up to a multiple of 8.</summary>
    /// <param name="stackBytes">What the method takes from the stack as it is written before the pass.</param>
    public static long PieceBytes(long stackBytes) => (stackBytes + 7) / 8 * 8;

    /// <summary>Rewrites the methods of a file that have a place, and says why those that have buffers and none keep the stack.</summary>
    /// <param name="file">The file as the emitter made it.</param>
    /// <param name="placeOf">Where a procedure's piece starts in its project's region, by the procedure's name; nothing for one that has no place.</param>
    /// <param name="whyOnStack">Why a procedure that has no place keeps the stack, as lines of a comment, by the procedure's name; nothing where nothing is to be said.</param>
    public static TreeFile Apply(TreeFile file, Func<string, long?> placeOf, Func<string, IReadOnlyList<string>?> whyOnStack)
    {
        var members = new List<TreeMember>();
        foreach (var member in file.Members)
        {
            if (member is not TreeMethod { Procedure: { } procedure } method || BuffersOf(method).Count == 0)
            {
                members.Add(member);
            }
            else if (placeOf(procedure) is { } place)
            {
                members.Add(Apply(method, place));
                members.Add(OpenArenaFor(method));
            }
            else if (whyOnStack(procedure) is { } lines)
            {
                members.Add(method with { Declarations = [.. lines.Select(line => new TreeStatement.Comment(line)), .. method.Declarations] });
            }
            else
            {
                members.Add(member);
            }
        }

        return file with { Members = members };
    }

    private static List<TreeStatement.Declare> BuffersOf(TreeMethod method) =>
    [
        .. method.Declarations
            .OfType<TreeStatement.Declare>()
            .Where(declared => declared.Value is TreeExpression.StackAllocation),
    ];

    // The name of the method that calls one from outside with a copy of
    // the arena open; a name a FORTRAN procedure cannot have, having no
    // lower-case letter.
    private static string OpenerOf(TreeMethod method) => $"_{method.Name}_opens_arena";

    private static TreeMethod Apply(TreeMethod method, long place)
    {
        var buffers = BuffersOf(method);

        // The widest elements first, so that each lies at a multiple of
        // its own size with nothing left empty between them.
        var offsets = new Dictionary<TreeStatement.Declare, long>();
        long size = 0;
        foreach (var buffer in buffers.OrderByDescending(ElementBytes))
        {
            offsets[buffer] = size;
            size += ElementBytes(buffer) * ((TreeExpression.StackAllocation)buffer.Value).Count;
        }

        var piece = new TreeExpression.Local(Piece);
        var onStack = new TreeExpression.Local(OnStack);
        var region = new TreeExpression.Local(RegionField + ".Start");
        var declarations = new List<TreeStatement>();
        foreach (var declaration in method.Declarations)
        {
            if (declaration is not TreeStatement.Declare { Value: TreeExpression.StackAllocation taken } buffer)
            {
                declarations.Add(declaration);
                continue;
            }

            if (buffer == buffers[0])
            {
                // The place in the open copy of the arena first, the stack
                // in its stead where the region says so, and a call that
                // opens a copy where none is: a pointer may be given what
                // is taken from the stack only where it is declared.
                declarations.Add(new TreeStatement.Declare("byte*", Piece, At(region, place)));
                declarations.Add(new TreeStatement.If(
                [
                    new TreeStatement.Branch(
                        new TreeExpression.Local($"{RegionField}.CurrentSource == BufferSource.Stack"),
                        [
                            new TreeStatement.Declare("byte*", OnStack, new TreeExpression.StackAllocation("byte", PieceBytes(size))),
                            new TreeStatement.Assign(piece, onStack),
                        ]),
                    new TreeStatement.Branch(
                        new TreeExpression.Local($"{RegionField}.CurrentSource == BufferSource.None"),
                        CallAgain(method, OpenerOf(method))),
                ]));
            }

            var address = At(piece, offsets[buffer]);
            declarations.Add(buffer with
            {
                Value = taken.Type == "byte" ? address : new TreeExpression.Converted(buffer.Type, Group(address)),
            });

            // What was to be zero when taken is set so, since neither the
            // piece nor the arena is.
            if (taken.IsZeroed)
            {
                for (long element = 0; element < taken.Count; element++)
                {
                    declarations.Add(new TreeStatement.Assign(
                        new TreeExpression.Indexed(new TreeExpression.Local(buffer.Name), new TreeExpression.Integer(element)),
                        new TreeExpression.Integer(0)));
                }
            }
        }

        return method with { Declarations = declarations };
    }

    // The method beside one with a place, for a call from outside: it
    // opens a copy of the arena, calls the method, which then finds the
    // copy open, and closes it however the call ends.
    private static TreeMethod OpenArenaFor(TreeMethod method) => new(
        [],
        method.ReturnType,
        OpenerOf(method),
        method.Parameters,
        [],
        [],
        [
            new TreeStatement.Comment($"{method.Name} called from outside, with a copy of the arena open for it, closed however the call ends."),
            new TreeStatement.Evaluate(new TreeExpression.Call("_context.Arena.Push", [])),
            new TreeStatement.TryFinally(
                CallAgain(method, method.Name),
                [new TreeStatement.Evaluate(new TreeExpression.Call("_context.Arena.Pop", []))]),
        ])
    {
        IsPublic = false,
    };

    // A call of a method of the same parameters with what this one was
    // given, and its result returned.
    private static List<TreeStatement> CallAgain(TreeMethod method, string name)
    {
        var call = new TreeExpression.Call(name, [.. method.Parameters.Select(ArgumentFor)]);
        return method.ReturnType == "void"
            ? [new TreeStatement.Evaluate(call), new TreeStatement.Return(null)]
            : [new TreeStatement.Return(call)];
    }

    // What passes a parameter on: its name, after the word that says how
    // it is passed where there is one.
    private static TreeExpression.Local ArgumentFor(string parameter)
    {
        var name = parameter[(parameter.LastIndexOf(' ') + 1)..];
        var passing = parameter.Split(' ')[0];
        return new TreeExpression.Local(passing is "ref" or "out" or "in" ? $"{passing} {name}" : name);
    }

    private static TreeExpression At(TreeExpression start, long offset) => offset == 0
        ? start
        : new TreeExpression.Sum([new(start), new(new TreeExpression.Integer(offset))]);

    private static TreeExpression Group(TreeExpression address) => address is TreeExpression.Sum ? new TreeExpression.Grouped(address) : address;

    private static long ElementBytes(TreeStatement.Declare buffer) => ((TreeExpression.StackAllocation)buffer.Value).Type switch
    {
        "byte" => 1,
        "int" or "float" => 4,
        _ => 8,
    };
}
