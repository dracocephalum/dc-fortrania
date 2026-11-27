namespace Dracocephalum.Fortrania.Emit;

/// <summary>
/// The pass that keeps in locals, while a method runs, the single numbers
/// and logical values it reaches by an address: its scalar arguments, and
/// the scalars its unit keeps between calls. A method that reads and
/// writes one behind its address does so in memory every time, since the
/// compiler cannot know that no other address of the method reaches the
/// same place. FORTRAN 77 does know. Of arguments it says that no two may
/// be the same storage where either is given a value (ANSI X3.9-1978,
/// 15.9.3.6); and a variable a unit keeps is the unit's alone, unless an
/// <c>EQUIVALENCE</c> lays another over it, and such a one is not offered
/// to this pass. So each is read into a local on entry, used there, and
/// stored back where the method ends, and the compiler keeps the local in
/// a register.
/// </summary>
/// <remarks>
/// <para>
/// Where a statement passes the address on, to a procedure or to input
/// and output, the local is stored before it and read again after it, so
/// that what is called sees the value and what it stores is seen. A kept
/// scalar is treated so around every statement that calls what may come
/// back into the unit, another of its entry points among them, since that
/// reads and stores the same variable by an address of its own.
/// </para>
/// <para>
/// One is left as it was where that cannot be done simply, or where
/// nothing is gained: one whose address is used in the condition of a
/// branch or a loop, or in a statement that also reads its value, since
/// what is called may store to it before the value is read; one a
/// statement function of the unit names; one that is never read; and one
/// read too seldom to pay for the reading and storing around the
/// statements that pass it on.
/// </para>
/// <para>
/// A method that ends by an exception a procedure it calls throws does not
/// store its locals back. A program stopped so has ended, as FORTRAN's
/// <c>STOP</c> ends it.
/// </para>
/// <para>
/// <c>DESIGN.md</c>, <em>A scalar argument is kept in a local</em> and
/// <em>A kept scalar is in a local while its method runs</em>, has the
/// measurements, what a translation does about calls that break the rule
/// on arguments, and how a call that may come back is told.
/// </para>
/// </remarks>
internal static class ScalarLocalPass
{
    /// <summary>The setting that leaves every argument behind its address, as a translation's settings file spells it.</summary>
    public const string Setting = "allowsArgumentAliasing";

    private const string Suffix = "_value";

    /// <summary>Rewrites the methods of a file to keep their scalars in locals.</summary>
    /// <param name="file">The file as the emitter made it.</param>
    /// <param name="isAliased">
    /// Whether an argument of an entry point, by the name the program calls
    /// it by and the argument's place counted from zero, may be the same
    /// storage as another: some call of the program passes it so. Such a
    /// one is left behind its address. Nothing where every argument is to
    /// stay behind its address.
    /// </param>
    /// <param name="mayReenter">
    /// Whether a call may come back into the unit of the method that makes
    /// it, by that method's name among the program's calls and the name of
    /// the procedure called. Nothing where every kept scalar is to stay
    /// behind its address.
    /// </param>
    public static TreeFile Apply(TreeFile file, Func<string, int, bool>? isAliased, Func<string, string, bool>? mayReenter) => file with
    {
        Members =
        [
            .. file.Members.Select(member => member is TreeMethod method && (method.ScalarArguments.Count > 0 || method.SavedScalars.Count > 0)
                ? Apply(method, isAliased, mayReenter)
                : member),
        ],
    };

    private static TreeMethod Apply(TreeMethod method, Func<string, int, bool>? isAliased, Func<string, string, bool>? mayReenter)
    {
        var uses = new Dictionary<string, Use>(StringComparer.Ordinal);
        if (isAliased is not null)
        {
            foreach (var argument in method.ScalarArguments.Where(argument => method.EntryPoint is not { } entry || !isAliased(entry, argument.Position)))
            {
                uses[argument.Name] = new Use(argument.Name, argument.Type, argument.Position, isSaved: false);
            }
        }

        // A statement function is written where the method starts and is
        // not looked into below, so where one calls a procedure, a call of
        // it hides that call, and the kept scalars stay as they are.
        if (mayReenter is not null && method.Procedure is not null && !HasCallsInFunctions(method))
        {
            foreach (var (saved, order) in method.SavedScalars.Select((saved, order) => (saved, order)))
            {
                uses.TryAdd(saved.Name, new Use(saved.Name, saved.Type, order, isSaved: true));
            }
        }

        if (uses.Count == 0)
        {
            return method;
        }

        // Whether a statement calls what may come back into the unit.
        bool Reenters(TreeStatement statement) =>
            mayReenter is not null && method.Procedure is { } caller && HasCall(statement, callee => mayReenter(caller, callee));

        // What stands at the top comes before the locals are read, so a
        // scalar whose value is used there stays behind its address.
        foreach (var statement in method.Declarations)
        {
            foreach (var name in Mentioned(statement, uses))
            {
                uses[name].IsLeft = true;
            }
        }

        Measure(method.Body, uses, Reenters, isInLoop: false);

        var kept = uses.Values.Where(use => use.IsWorthKeeping).ToDictionary(use => use.Name, StringComparer.Ordinal);
        if (kept.Count == 0)
        {
            return method;
        }

        // The locals' names: the scalar's with a suffix no FORTRAN name
        // can have, and an underscore in front where the method has that
        // name already.
        var taken = NamesOf(method);
        foreach (var use in kept.Values)
        {
            use.Local = taken.Contains(use.Name + Suffix) ? "_" + use.Name + Suffix : use.Name + Suffix;
        }

        var body = Rewrite(method.Body, kept, Reenters);
        if (body is not [.., TreeStatement.Return or TreeStatement.Throw])
        {
            body.AddRange(StoreBack(kept.Values.Where(use => use.IsWritten)));
        }

        return method with
        {
            Declarations =
            [
                .. method.Declarations,
                .. Loads(kept.Values.Where(use => !use.IsSaved), "Scalar arguments, kept in locals while the method runs: FORTRAN lets no other name reach them."),
                .. Loads(kept.Values.Where(use => use.IsSaved), "Kept scalars, in locals while the method runs: stored where it ends, and around a call that may come back to this unit."),
            ],
            Body = body,
        };
    }

    // Counts how each scalar is used in statements: its value read or
    // stored, and its address passed on.
    private static void Measure(IReadOnlyList<TreeStatement> statements, Dictionary<string, Use> uses, Func<TreeStatement, bool> reenters, bool isInLoop)
    {
        foreach (var statement in statements)
        {
            switch (statement)
            {
                case TreeStatement.If branches:
                    foreach (var branch in branches.Branches)
                    {
                        MeasureHeader(branch.Condition, uses, reenters, isInLoop);
                        Measure(branch.Body, uses, reenters, isInLoop);
                    }

                    break;

                case TreeStatement.While loop:
                    MeasureHeader(loop.Condition, uses, reenters, isInLoop: true);
                    Measure(loop.Body, uses, reenters, isInLoop: true);
                    break;

                case TreeStatement.For loop:
                    MeasureSimple(loop.Start, uses, reenters, isInLoop, isHeader: true);
                    MeasureHeader(loop.Condition, uses, reenters, isInLoop: true);
                    foreach (var step in loop.Step)
                    {
                        MeasureSimple(step, uses, reenters, isInLoop: true, isHeader: true);
                    }

                    Measure(loop.Body, uses, reenters, isInLoop: true);
                    break;

                case TreeStatement.TryFinally guarded:
                    Measure(guarded.Body, uses, reenters, isInLoop);
                    Measure(guarded.After, uses, reenters, isInLoop);
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
                    MeasureSimple(statement, uses, reenters, isInLoop, isHeader: false);
                    break;
            }
        }
    }

    private static void MeasureHeader(TreeExpression? condition, Dictionary<string, Use> uses, Func<TreeStatement, bool> reenters, bool isInLoop)
    {
        if (condition is not null)
        {
            MeasureSimple(new TreeStatement.Evaluate(condition), uses, reenters, isInLoop, isHeader: true);
        }
    }

    // One statement that holds no others. An address passed on in the head
    // of a branch or a loop leaves the scalar as it was: the local would
    // have to be read again inside what the head governs. A call that may
    // come back into the unit counts as the address of every kept scalar
    // passed on.
    private static void MeasureSimple(TreeStatement statement, Dictionary<string, Use> uses, Func<TreeStatement, bool> reenters, bool isInLoop, bool isHeader)
    {
        var target = TargetOf(statement);
        var counts = Count(statement, uses);
        if (reenters(statement))
        {
            foreach (var saved in uses.Values.Where(use => use.IsSaved))
            {
                var (values, addresses) = counts.GetValueOrDefault(saved.Name);
                counts[saved.Name] = (values, addresses + 1);
            }
        }

        foreach (var (name, (values, addresses)) in counts)
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

                // What is called may store to the scalar before the
                // statement reads its value.
                use.IsLeft |= isHeader || reads > 0;
            }
        }
    }

    private static List<TreeStatement> Rewrite(IReadOnlyList<TreeStatement> statements, Dictionary<string, Use> kept, Func<TreeStatement, bool> reenters)
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
                            Rewrite(branch.Body, kept, reenters))),
                    ]));
                    break;

                case TreeStatement.While loop:
                    result.Add(new TreeStatement.While(WithLocals(loop.Condition, kept), Rewrite(loop.Body, kept, reenters)));
                    break;

                case TreeStatement.For loop:
                    result.Add(new TreeStatement.For(
                        WithLocals(loop.Start, kept),
                        WithLocals(loop.Condition, kept),
                        [.. loop.Step.Select(step => WithLocals(step, kept))],
                        Rewrite(loop.Body, kept, reenters)));
                    break;

                case TreeStatement.TryFinally guarded:
                    result.Add(new TreeStatement.TryFinally(Rewrite(guarded.Body, kept, reenters), Rewrite(guarded.After, kept, reenters)));
                    break;

                case TreeStatement.LocalFunction or TreeStatement.Comment or TreeStatement.Note or TreeStatement.Blank:
                    result.Add(statement);
                    break;

                // The method ends: what it stored in a local goes back
                // behind the address.
                case TreeStatement.Return or TreeStatement.Throw:
                    result.AddRange(StoreBack(written));
                    result.Add(WithLocals(statement, kept));
                    break;

                default:
                    var comesBack = reenters(statement);
                    var passed = Count(statement, kept).Where(pair => pair.Value.Addresses > 0).Select(pair => kept[pair.Key])
                        .Union(kept.Values.Where(use => comesBack && use.IsSaved))
                        .OrderBy(use => use.Order)
                        .ToList();
                    var target = TargetOf(statement);

                    // What the statement calls is to see the value, and
                    // what it stores is to be seen after; unless the
                    // statement itself then gives the scalar a value.
                    result.AddRange(StoreBack(passed.Where(use => use.IsWritten)));
                    result.Add(WithLocals(statement, kept));
                    result.AddRange(passed.Where(use => use.Name != target).Select(use => (TreeStatement)new TreeStatement.Assign(
                        new TreeExpression.Local(use.Local),
                        new TreeExpression.Dereference(new TreeExpression.Local(use.Name)))));
                    break;
            }
        }

        return result;
    }

    private static IEnumerable<TreeStatement> StoreBack(IEnumerable<Use> uses) => uses
        .OrderBy(use => use.Order)
        .Select(use => new TreeStatement.Assign(
            new TreeExpression.Dereference(new TreeExpression.Local(use.Name)),
            new TreeExpression.Local(use.Local)));

    // The locals of some scalars declared and read, under a remark that
    // says what they are; nothing where there are none.
    private static IEnumerable<TreeStatement> Loads(IEnumerable<Use> uses, string remark)
    {
        var ordered = uses.OrderBy(use => use.Order).ToList();
        if (ordered.Count == 0)
        {
            return [];
        }

        return
        [
            new TreeStatement.Note(remark),
            .. ordered.Select(use => new TreeStatement.Declare(use.Type, use.Local, new TreeExpression.Dereference(new TreeExpression.Local(use.Name)))),
        ];
    }

    private static TreeStatement WithLocals(TreeStatement statement, Dictionary<string, Use> kept) =>
        TreeRewriter.Rewrite([statement], node => ToLocal(node, kept))[0];

    private static TreeExpression WithLocals(TreeExpression expression, Dictionary<string, Use> kept) =>
        ((TreeStatement.Evaluate)WithLocals(new TreeStatement.Evaluate(expression), kept)).Expression;

    private static TreeExpression ToLocal(TreeExpression node, Dictionary<string, Use> kept) =>
        node is TreeExpression.Dereference { Address: TreeExpression.Local { Name: var name } } && kept.TryGetValue(name, out var use)
            ? new TreeExpression.Local(use.Local)
            : node;

    // The scalar a statement stores to through its address, where it does.
    private static string? TargetOf(TreeStatement statement) => statement switch
    {
        TreeStatement.Assign { Target: TreeExpression.Dereference { Address: TreeExpression.Local { Name: var name } } } => name,
        TreeStatement.Advance { Target: TreeExpression.Dereference { Address: TreeExpression.Local { Name: var name } } } => name,
        TreeStatement.Increment { Target: TreeExpression.Dereference { Address: TreeExpression.Local { Name: var name } } } => name,
        TreeStatement.Decrement { Target: TreeExpression.Dereference { Address: TreeExpression.Local { Name: var name } } } => name,
        _ => null,
    };

    // How often a statement uses each scalar's value, behind its address,
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

    // Whether a statement calls a procedure of the program that the test
    // holds for, or calls through the address of one, which may be any.
    private static bool HasCall(TreeStatement statement, Func<string, bool> test)
    {
        var found = false;
        TreeRewriter.Rewrite([statement], node =>
        {
            found |= node is TreeExpression.Invoke || (node is TreeExpression.Call { Procedure: { } callee } && test(callee));
            return node;
        });
        return found;
    }

    // Whether a function of the method's own, which a statement function
    // of the unit has become, calls a procedure of the program.
    private static bool HasCallsInFunctions(TreeMethod method) =>
        method.Declarations.Concat(method.Body).SelectMany(Functions).OfType<TreeStatement.LocalFunction>()
            .Any(function => function.Body.Any(inner => HasCall(inner, _ => true)));

    // The scalars a statement names anywhere, a function of the method's
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

    // How a method uses one scalar it reaches by an address.
    private sealed class Use(string name, string type, int order, bool isSaved)
    {
        // What the method has no more arguments than, so that what the
        // unit keeps comes after every argument.
        private const int AfterArguments = 100_000;

        // The name of the pointer the scalar lies behind.
        public string Name { get; } = name;

        public string Type { get; } = type;

        // Where it stands among those the method reads on entry: the
        // arguments by their places, and after them what the unit keeps.
        public int Order { get; } = isSaved ? AfterArguments + order : order;

        // Whether the unit keeps it between calls; otherwise an argument.
        public bool IsSaved { get; } = isSaved;

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
