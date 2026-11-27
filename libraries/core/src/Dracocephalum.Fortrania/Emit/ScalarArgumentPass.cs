namespace Dracocephalum.Fortrania.Emit;

/// <summary>
/// The pass that keeps a method's scalar arguments in locals while it runs.
/// An argument is passed by its address, and a method that reads and writes
/// it there does so in memory every time, since the compiler cannot know
/// that no other address of the method reaches the same place. FORTRAN 77
/// says so of a procedure's arguments: no two of them may be the same
/// storage where either is given a value (ANSI X3.9-1978, 15.9.3.6). So the
/// argument is read into a local on entry, used there, and stored back
/// where the method ends, and the compiler keeps the local in a register.
/// </summary>
/// <remarks>
/// <para>
/// Where a statement passes the argument's address on, to a procedure or to
/// input and output, the local is stored before it and read again after it,
/// so that what is called sees the value and what it stores is seen. An
/// argument is left as it was where that cannot be done simply, or where
/// nothing is gained: one whose address is used in the condition of a
/// branch or a loop, or in a statement that also reads its value, since
/// what is called may store to it before the value is read; one a
/// statement function of the unit names; one that is never read; and one
/// read too seldom to pay for the reading and storing around the statements
/// that pass it on.
/// </para>
/// <para>
/// A method that ends by an exception a procedure it calls throws does not
/// store its locals back. A program stopped so has ended, as FORTRAN's
/// <c>STOP</c> ends it.
/// </para>
/// <para>
/// <c>DESIGN.md</c>, <em>A scalar argument is kept in a local</em>, has the
/// measurements and what a translation does about calls that break the
/// rule.
/// </para>
/// </remarks>
internal static class ScalarArgumentPass
{
    /// <summary>The setting that leaves every argument behind its address, as a translation's settings file spells it.</summary>
    public const string Setting = "allowsArgumentAliasing";

    private const string Suffix = "_value";

    /// <summary>Rewrites the methods of a file to keep their scalar arguments in locals.</summary>
    /// <param name="file">The file as the emitter made it.</param>
    /// <param name="isAliased">
    /// Whether an argument of an entry point, by the name the program calls
    /// it by and the argument's place counted from zero, may be the same
    /// storage as another: some call of the program passes it so. Such a
    /// one is left behind its address.
    /// </param>
    public static TreeFile Apply(TreeFile file, Func<string, int, bool> isAliased) => file with
    {
        Members = [.. file.Members.Select(member => member is TreeMethod { ScalarArguments.Count: > 0 } method ? Apply(method, isAliased) : member)],
    };

    private static TreeMethod Apply(TreeMethod method, Func<string, int, bool> isAliased)
    {
        var uses = method.ScalarArguments
            .Where(argument => method.EntryPoint is not { } entry || !isAliased(entry, argument.Position))
            .ToDictionary(argument => argument.Name, argument => new Use(argument), StringComparer.Ordinal);
        if (uses.Count == 0)
        {
            return method;
        }

        // What stands at the top comes before the locals are read, so an
        // argument named there stays behind its address.
        foreach (var statement in method.Declarations)
        {
            foreach (var name in Mentioned(statement, uses))
            {
                uses[name].IsLeft = true;
            }
        }

        Measure(method.Body, uses, isInLoop: false);

        var kept = uses.Values.Where(use => use.IsWorthKeeping).ToDictionary(use => use.Argument.Name, StringComparer.Ordinal);
        if (kept.Count == 0)
        {
            return method;
        }

        // The locals' names: the argument's with a suffix no FORTRAN name
        // can have, and an underscore in front where the method has that
        // name already.
        var taken = NamesOf(method);
        foreach (var use in kept.Values)
        {
            use.Local = taken.Contains(use.Argument.Name + Suffix) ? "_" + use.Argument.Name + Suffix : use.Argument.Name + Suffix;
        }

        var body = Rewrite(method.Body, kept);
        if (body is not [.., TreeStatement.Return or TreeStatement.Throw])
        {
            body.AddRange(StoreBack(kept.Values.Where(use => use.IsWritten)));
        }

        return method with
        {
            Declarations =
            [
                .. method.Declarations,
                new TreeStatement.Note("Scalar arguments, kept in locals while the method runs: FORTRAN lets no other name reach them."),
                .. kept.Values.OrderBy(use => use.Argument.Position).Select(use => new TreeStatement.Declare(
                    use.Argument.Type,
                    use.Local,
                    new TreeExpression.Dereference(new TreeExpression.Local(use.Argument.Name)))),
            ],
            Body = body,
        };
    }

    // Counts how each argument is used in statements: its value read or
    // stored, and its address passed on.
    private static void Measure(IReadOnlyList<TreeStatement> statements, Dictionary<string, Use> uses, bool isInLoop)
    {
        foreach (var statement in statements)
        {
            switch (statement)
            {
                case TreeStatement.If branches:
                    foreach (var branch in branches.Branches)
                    {
                        MeasureHeader(branch.Condition, uses, isInLoop);
                        Measure(branch.Body, uses, isInLoop);
                    }

                    break;

                case TreeStatement.While loop:
                    MeasureHeader(loop.Condition, uses, isInLoop: true);
                    Measure(loop.Body, uses, isInLoop: true);
                    break;

                case TreeStatement.For loop:
                    MeasureSimple(loop.Start, uses, isInLoop, isHeader: true);
                    MeasureHeader(loop.Condition, uses, isInLoop: true);
                    foreach (var step in loop.Step)
                    {
                        MeasureSimple(step, uses, isInLoop: true, isHeader: true);
                    }

                    Measure(loop.Body, uses, isInLoop: true);
                    break;

                case TreeStatement.TryFinally guarded:
                    Measure(guarded.Body, uses, isInLoop);
                    Measure(guarded.After, uses, isInLoop);
                    break;

                case TreeStatement.LocalFunction:
                    foreach (var name in Mentioned(statement, uses))
                    {
                        uses[name].IsLeft = true;
                    }

                    break;

                case TreeStatement.Comment or TreeStatement.Note or TreeStatement.Blank:
                    break;

                default:
                    MeasureSimple(statement, uses, isInLoop, isHeader: false);
                    break;
            }
        }
    }

    private static void MeasureHeader(TreeExpression? condition, Dictionary<string, Use> uses, bool isInLoop)
    {
        if (condition is not null)
        {
            MeasureSimple(new TreeStatement.Evaluate(condition), uses, isInLoop, isHeader: true);
        }
    }

    // One statement that holds no others. An address passed on in the head
    // of a branch or a loop leaves the argument as it was: the local would
    // have to be read again inside what the head governs.
    private static void MeasureSimple(TreeStatement statement, Dictionary<string, Use> uses, bool isInLoop, bool isHeader)
    {
        var target = TargetOf(statement);
        foreach (var (name, (values, addresses)) in Count(statement, uses))
        {
            var use = uses[name];
            var isStoredOnly = statement is TreeStatement.Assign && target == name;
            var reads = values - (isStoredOnly ? 1 : 0);
            use.Values += values;
            use.Reads += reads;
            use.ReadsInLoops += isInLoop ? reads : 0;
            use.IsWritten |= target == name;
            if (addresses > 0)
            {
                use.Passes++;

                // What is called may store to the argument before the
                // statement reads its value.
                use.IsLeft |= isHeader || reads > 0;
            }
        }
    }

    private static List<TreeStatement> Rewrite(IReadOnlyList<TreeStatement> statements, Dictionary<string, Use> kept)
    {
        var written = kept.Values.Where(use => use.IsWritten).ToList();
        var result = new List<TreeStatement>();
        foreach (var statement in statements)
        {
            switch (statement)
            {
                case TreeStatement.If branches:
                    result.Add(new TreeStatement.If(
                    [
                        .. branches.Branches.Select(branch => new TreeStatement.Branch(
                            branch.Condition is { } condition ? WithLocals(condition, kept) : null,
                            Rewrite(branch.Body, kept))),
                    ]));
                    break;

                case TreeStatement.While loop:
                    result.Add(new TreeStatement.While(WithLocals(loop.Condition, kept), Rewrite(loop.Body, kept)));
                    break;

                case TreeStatement.For loop:
                    result.Add(new TreeStatement.For(
                        WithLocals(loop.Start, kept),
                        WithLocals(loop.Condition, kept),
                        [.. loop.Step.Select(step => WithLocals(step, kept))],
                        Rewrite(loop.Body, kept)));
                    break;

                case TreeStatement.TryFinally guarded:
                    result.Add(new TreeStatement.TryFinally(Rewrite(guarded.Body, kept), Rewrite(guarded.After, kept)));
                    break;

                case TreeStatement.LocalFunction or TreeStatement.Comment or TreeStatement.Note or TreeStatement.Blank:
                    result.Add(statement);
                    break;

                // The method ends: what it stored in a local goes to the
                // caller's variable.
                case TreeStatement.Return or TreeStatement.Throw:
                    result.AddRange(StoreBack(written));
                    result.Add(WithLocals(statement, kept));
                    break;

                default:
                    var passed = Count(statement, kept).Where(pair => pair.Value.Addresses > 0).Select(pair => kept[pair.Key]).ToList();
                    var target = TargetOf(statement);

                    // What the statement calls is to see the value, and
                    // what it stores is to be seen after; unless the
                    // statement itself then gives the argument a value.
                    result.AddRange(StoreBack(passed.Where(use => use.IsWritten)));
                    result.Add(WithLocals(statement, kept));
                    result.AddRange(passed.Where(use => use.Argument.Name != target).Select(use => (TreeStatement)new TreeStatement.Assign(
                        new TreeExpression.Local(use.Local),
                        new TreeExpression.Dereference(new TreeExpression.Local(use.Argument.Name)))));
                    break;
            }
        }

        return result;
    }

    private static IEnumerable<TreeStatement> StoreBack(IEnumerable<Use> uses) => uses
        .OrderBy(use => use.Argument.Position)
        .Select(use => new TreeStatement.Assign(
            new TreeExpression.Dereference(new TreeExpression.Local(use.Argument.Name)),
            new TreeExpression.Local(use.Local)));

    private static TreeStatement WithLocals(TreeStatement statement, Dictionary<string, Use> kept) =>
        TreeRewriter.Rewrite([statement], node => ToLocal(node, kept))[0];

    private static TreeExpression WithLocals(TreeExpression expression, Dictionary<string, Use> kept) =>
        ((TreeStatement.Evaluate)WithLocals(new TreeStatement.Evaluate(expression), kept)).Expression;

    private static TreeExpression ToLocal(TreeExpression node, Dictionary<string, Use> kept) =>
        node is TreeExpression.Dereference { Address: TreeExpression.Local { Name: var name } } && kept.TryGetValue(name, out var use)
            ? new TreeExpression.Local(use.Local)
            : node;

    // The argument a statement stores to through its address, where it does.
    private static string? TargetOf(TreeStatement statement) => statement switch
    {
        TreeStatement.Assign { Target: TreeExpression.Dereference { Address: TreeExpression.Local { Name: var name } } } => name,
        TreeStatement.Advance { Target: TreeExpression.Dereference { Address: TreeExpression.Local { Name: var name } } } => name,
        TreeStatement.Increment { Target: TreeExpression.Dereference { Address: TreeExpression.Local { Name: var name } } } => name,
        TreeStatement.Decrement { Target: TreeExpression.Dereference { Address: TreeExpression.Local { Name: var name } } } => name,
        _ => null,
    };

    // How often a statement uses each argument's value, behind its address,
    // and how often the address itself. The rewriter offers an address
    // before what dereferences it, so each value is first counted as an
    // address and then moved.
    private static Dictionary<string, (int Values, int Addresses)> Count(TreeStatement statement, Dictionary<string, Use> uses)
    {
        var counts = new Dictionary<string, (int Values, int Addresses)>(StringComparer.Ordinal);
        TreeRewriter.Rewrite([statement], node =>
        {
            if (node is TreeExpression.Local { Name: var name } && uses.ContainsKey(name))
            {
                var (values, addresses) = counts.GetValueOrDefault(name);
                counts[name] = (values, addresses + 1);
            }
            else if (node is TreeExpression.Dereference { Address: TreeExpression.Local { Name: var behind } } && uses.ContainsKey(behind))
            {
                var (values, addresses) = counts[behind];
                counts[behind] = (values + 1, addresses - 1);
            }

            return node;
        });
        return counts;
    }

    // The arguments a statement names anywhere, a function of the method's
    // own among the places, which the rewriter leaves alone.
    private static IEnumerable<string> Mentioned(TreeStatement statement, Dictionary<string, Use> uses) => statement is TreeStatement.LocalFunction function
        ? function.Body.SelectMany(inner => Mentioned(inner, uses)).Distinct(StringComparer.Ordinal)
        : Count(statement, uses).Keys.Concat(Inner(statement).SelectMany(inner => Mentioned(inner, uses))).Distinct(StringComparer.Ordinal);

    // The functions of the method's own among the statements a statement
    // holds; the rest the rewriter counts with the statement.
    private static IEnumerable<TreeStatement> Inner(TreeStatement statement) => statement switch
    {
        TreeStatement.If branches => branches.Branches.SelectMany(branch => branch.Body).SelectMany(Functions),
        TreeStatement.While loop => loop.Body.SelectMany(Functions),
        TreeStatement.For loop => loop.Body.SelectMany(Functions),
        TreeStatement.TryFinally guarded => guarded.Body.Concat(guarded.After).SelectMany(Functions),
        _ => [],
    };

    private static IEnumerable<TreeStatement> Functions(TreeStatement statement) =>
        statement is TreeStatement.LocalFunction ? [statement] : Inner(statement);

    // Every name the method declares or is given, for a local's name to
    // differ from.
    private static HashSet<string> NamesOf(TreeMethod method)
    {
        var names = new HashSet<string>(method.Parameters.Select(parameter => parameter[(parameter.LastIndexOf(' ') + 1)..]), StringComparer.Ordinal);

        void Add(IEnumerable<TreeStatement> statements)
        {
            foreach (var statement in statements)
            {
                switch (statement)
                {
                    case TreeStatement.Declare declared:
                        names.Add(declared.Name);
                        break;
                    case TreeStatement.If branches:
                        Add(branches.Branches.SelectMany(branch => branch.Body));
                        break;
                    case TreeStatement.While loop:
                        Add(loop.Body);
                        break;
                    case TreeStatement.For loop:
                        Add([loop.Start, .. loop.Body]);
                        break;
                    case TreeStatement.TryFinally guarded:
                        Add([.. guarded.Body, .. guarded.After]);
                        break;
                }
            }
        }

        Add(method.Declarations);
        Add(method.Body);
        return names;
    }

    // How a method uses one argument.
    private sealed class Use(ScalarArgument argument)
    {
        public ScalarArgument Argument { get; } = argument;

        // Its value used behind its address: read, or stored to.
        public int Values { get; set; }

        // Of those, the uses that read it.
        public int Reads { get; set; }

        public int ReadsInLoops { get; set; }

        // The statements that pass its address on.
        public int Passes { get; set; }

        public bool IsWritten { get; set; }

        // Whether it has to stay behind its address.
        public bool IsLeft { get; set; }

        public string Local { get; set; } = string.Empty;

        // A local costs a read on entry and a read after every statement
        // that passes the address on. It pays in a loop, where the value
        // is otherwise read from memory on every turn, and where its uses
        // outnumber those reads.
        public bool IsWorthKeeping => !IsLeft && Reads > 0 && (ReadsInLoops > 0 || Values > Passes + 1);
    }
}
