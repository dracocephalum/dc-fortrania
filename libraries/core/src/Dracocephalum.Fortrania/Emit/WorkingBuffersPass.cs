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
/// A method that has buffers and a place is written as three. Its entry,
/// public and of its own name, is what the program and other projects call:
/// it tests whether a copy of the arena is open, which is not so while
/// nothing translated runs, and calls the core, or calls it through the
/// opener, which opens a copy and closes it however the call ends. The core,
/// <c>_NAME_core</c>, takes the buffers and does the work, with the one test
/// of the stack, and calls the cores of the procedures of the project it
/// calls that have one, since a copy is open while it runs. A call from
/// inside a project so tests once, as before copies were opened, and only
/// one from outside a second time; no method that does the work has
/// exception handling, so the compiler treats it as before.
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

    /// <summary>Gives the methods of a file that the pass splits into an entry and a core: each that has buffers and a place.</summary>
    /// <param name="file">The file as the emitter made it.</param>
    /// <param name="placeOf">Where a procedure's piece starts in its project's region, by the procedure's name; nothing for one that has no place.</param>
    /// <returns>Each such method's procedure, and the name it is called by.</returns>
    public static IEnumerable<(string Procedure, string Name)> FindSplit(TreeFile file, Func<string, long?> placeOf) => file.Members
        .OfType<TreeMethod>()
        .Where(method => method.Procedure is { } procedure && BuffersOf(method).Count > 0 && placeOf(procedure) is not null)
        .Select(method => (method.Procedure ?? method.Name, method.Name));

    /// <summary>
    /// Rewrites the methods of a file that have buffers and a place into an
    /// entry, a core and an opener, and says why those that have buffers and
    /// no place keep the stack.
    /// </summary>
    /// <param name="file">The file as the emitter made it.</param>
    /// <param name="placeOf">Where a procedure's piece starts in its project's region, by the procedure's name; nothing for one that has no place.</param>
    /// <param name="whyOnStack">Why a procedure that has no place keeps the stack, as lines of a comment, by the procedure's name; nothing where nothing is to be said.</param>
    /// <param name="hasCore">
    /// Whether a call, by what it calls as written, <c>NAME</c> or
    /// <c>_Class.NAME</c>, reaches a procedure of the project that has a
    /// core; a core calls the core of such a one, and not its entry.
    /// </param>
    public static TreeFile Apply(TreeFile file, Func<string, long?> placeOf, Func<string, IReadOnlyList<string>?> whyOnStack, Func<string, bool> hasCore)
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
                members.Add(EnterFor(method));
                members.Add(Apply(method, place, hasCore));
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

    // The names of the methods written beside a procedure's entry: a name
    // no FORTRAN procedure can have under any convention, starting with an
    // underscore. A name that starts with one already, a variant's, is not
    // given a second: C# keeps names with two in a row for itself.
    private static string CoreOf(string name) => $"{Underscored(name)}_core";

    private static string OpenerOf(string name) => $"{Underscored(name)}_with_arena";

    private static string Underscored(string name) => name.StartsWith('_') ? name : "_" + name;

    // The procedure as the program calls it: where no copy of the arena is
    // open, nothing translated running, it calls the core through the
    // opener; otherwise the core itself. It is small enough to be compiled
    // into its callers, and only calls from outside a core reach it.
    private static TreeMethod EnterFor(TreeMethod method) => new(
        method.Comments,
        method.ReturnType,
        method.Name,
        method.Parameters,
        [],
        [],
        [
            new TreeStatement.If(
            [
                new TreeStatement.Branch(
                    new TreeExpression.Local($"{RegionField}.CurrentSource == BufferSource.None"),
                    method.ForwardTo(OpenerOf(method.Name))),
            ]),
            .. method.ForwardTo(CoreOf(method.Name), isLast: true),
        ])
    {
        IsInlined = true,
        IsPublic = method.IsPublic,
        IsInternal = method.IsInternal,
    };

    private static TreeMethod Apply(TreeMethod method, long place, Func<string, bool> hasCore)
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
                // The place in the open copy of the arena first, and the
                // stack in its stead where the region says so: a pointer may
                // be given what is taken from the stack only where it is
                // declared. A core is reached only with a copy open or the
                // stack chosen, its entry having seen to it.
                declarations.Add(new TreeStatement.Declare("byte*", Piece, At(region, place)));
                declarations.Add(new TreeStatement.If(
                [
                    new TreeStatement.Branch(
                        new TreeExpression.Local($"{RegionField}.CurrentSource == BufferSource.Stack"),
                        [
                            new TreeStatement.Declare("byte*", OnStack, new TreeExpression.StackAllocation("byte", PieceBytes(size))),
                            new TreeStatement.Assign(piece, onStack),
                        ]),
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

        // A call of a procedure of the project that has a core reaches the
        // core: this one runs, so a copy is open or the stack chosen.
        TreeExpression CallCore(TreeExpression expression)
        {
            if (expression is not TreeExpression.Call call || !hasCore(call.Method))
            {
                return expression;
            }

            var dot = call.Method.LastIndexOf('.');
            return call with { Method = call.Method[..(dot + 1)] + CoreOf(call.Method[(dot + 1)..]) };
        }

        return method with
        {
            Comments = [new TreeStatement.Comment($"What {method.Name} does, reached from its entry and from the cores of the project that call it.")],
            Name = CoreOf(method.Name),
            IsPublic = false,
            IsInternal = true,
            Declarations = TreeRewriter.Rewrite(declarations, CallCore),
            Body = TreeRewriter.Rewrite(method.Body, CallCore),
        };
    }

    // The method beside an entry, for a call from outside while nothing
    // translated runs: it opens a copy of the arena, calls the core, and
    // closes the copy however the call ends.
    private static TreeMethod OpenArenaFor(TreeMethod method) => new(
        [],
        method.ReturnType,
        OpenerOf(method.Name),
        method.Parameters,
        [],
        [],
        [
            new TreeStatement.Comment($"{method.Name} called from outside, with a copy of the arena open for it, closed however the call ends."),
            new TreeStatement.Evaluate(new TreeExpression.Call("_context.Arena.Push", [])),
            new TreeStatement.TryFinally(
                method.ForwardTo(CoreOf(method.Name), isLast: true),
                [new TreeStatement.Evaluate(new TreeExpression.Call("_context.Arena.Pop", []))]),
        ])
    {
        IsPublic = false,
    };

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
