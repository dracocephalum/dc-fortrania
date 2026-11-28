using Dracocephalum.Fortrania.Analysis;

namespace Dracocephalum.Fortrania.Emit;

/// <summary>
/// The pass that has a <c>DO</c> loop which only copies one array to
/// another, or gives every element of one the same value, done at once by
/// the runtime where the run is long, and by the loop as it was where it
/// is short or where doing it at once would give something else; and a
/// loop which only looks for a whole number in an array, and leaves the
/// procedure where it finds it, looked through by the runtime.
/// </summary>
/// <remarks>
/// <para>
/// Only the plainest loop is taken: from the first element, one at a
/// time, with one statement that stores to the element the loop's
/// variable names, from the same element of another array or from a
/// value the loop does not change.
/// </para>
/// <para>
/// Where the last element is known when translating, so is which way is
/// right for the length, and only that way is written: the loop as it
/// was for a short run, and for a long one the runtime's fill, or its
/// copy with the loop beside it, since whether two arrays overlap is
/// known only when the program runs. Where the last is not known, both
/// ways are written and the program chooses.
/// </para>
/// <para>
/// A loop that looks for a value is taken where the array and the value
/// are whole numbers, where the test is the loop's one statement, and
/// where what follows a find ends in a return: then the first find is the
/// only one, and nothing the loop does changes what it compares. Real
/// values are left to the loop, whose test tells a value that is no
/// number from itself and the framework's does not.
/// </para>
/// <para>
/// <c>DESIGN.md</c>, <em>A loop that copies or fills an array is done at
/// once where it is long</em>, has the measurements.
/// </para>
/// </remarks>
internal static class ArrayLoopPass
{
    /// <summary>The setting that leaves such loops as they are, as a translation's settings file spells it.</summary>
    public const string Setting = "plainArrayLoops";

    private const string ShouldMove = "ShouldMoveElements";
    private const string Move = "MoveElements";
    private const string Fill = "FillElements";
    private const string Find = "FindElement";
    private const string LongRunName = "LongRun";
    private const string WholeNumber = "int";

    // The runtime's LongRun, which the two have to agree on: the pass
    // decides by it for a length it knows, and the runtime for the rest.
    private const long LongRun = 16;

    /// <summary>Rewrites the loops of a file's methods that only copy or fill.</summary>
    /// <param name="file">The file as the emitter made it.</param>
    public static TreeFile Apply(TreeFile file)
    {
        // The runtime's procedures by their own names, or by the name the
        // file gives the runtime's class where it has to.
        var runtime = file.RuntimeAlias is { } alias ? alias + "." : string.Empty;
        return file with { Members = [.. file.Members.Select(member => member is TreeMethod method ? Apply(method, runtime) : member)] };
    }

    private static TreeMethod Apply(TreeMethod method, string runtime)
    {
        // The whole numbers the method names, a PARAMETER's value among
        // them: a loop to one of these has a length known here.
        var constants = method.Declarations
            .OfType<TreeStatement.Declare>()
            .Where(declared => declared.IsConstant)
            .ToDictionary(declared => declared.Name, declared => declared.Value switch
            {
                TreeExpression.Integer whole => whole.Value,
                TreeExpression.Number { Value: Constant.Integer whole } => whole.Value,
                _ => (long?)null,
            });

        // What the method names that is a whole number, and what is the
        // address of whole numbers: a loop that looks for a value is taken
        // only where both sides of its test are seen to be such.
        var typed = method.Parameters
            .Select(parameter => parameter.Split(' '))
            .Where(words => words.Length == 2)
            .Select(words => (Type: words[0], Name: words[1]))
            .Concat(method.Declarations.Concat(method.Body).OfType<TreeStatement.Declare>().Select(declared => (declared.Type, declared.Name)))
            .ToLookup(pair => pair.Type, pair => pair.Name, StringComparer.Ordinal);
        var numbers = typed[WholeNumber].ToHashSet(StringComparer.Ordinal);
        var arrays = typed[WholeNumber + "*"].ToHashSet(StringComparer.Ordinal);

        return method with { Body = TreeRewriter.RewriteStatements(method.Body, statement => Replace(statement, runtime, constants, numbers, arrays)) };
    }

    private static IReadOnlyList<TreeStatement> Replace(TreeStatement statement, string runtime, Dictionary<string, long?> constants, HashSet<string> numbers, HashSet<string> arrays)
    {
        if (ReplaceSearch(statement, runtime, constants, numbers, arrays) is { } search)
        {
            return search;
        }

        if (statement is not TreeStatement.For loop
            || loop.Start is not TreeStatement.Assign { Target: TreeExpression.Local variable, Value: TreeExpression.Integer { Value: 1 } }
            || loop.Condition is not TreeExpression.Binary { Operator: TreeExpression.Operation.LessOrEqual, Left: TreeExpression.Local counted, Right: var last }
            || loop.Step is not [TreeStatement.Advance { Target: TreeExpression.Local advanced, Amount: TreeExpression.Integer { Value: 1 }, IsSubtracted: false }]
            || loop.Body is not [TreeStatement.Assign { Target: TreeExpression.Indexed { Address: TreeExpression.Local target } stored, Value: var value }]
            || counted != variable
            || advanced != variable
            || last == variable
            || !IsElementOfTheTurn(stored, variable)
            || !TryCount(last, constants, out var known)
            || known < LongRun)
        {
            return [statement];
        }

        var source = value is TreeExpression.Indexed { Address: TreeExpression.Local from } read && IsElementOfTheTurn(read, variable) && from != target
            ? from
            : null;
        if (source is null && !IsUnchangedByTheLoop(value, variable, target))
        {
            return [statement];
        }

        // The runtime gives back where the loop leaves its variable, one
        // past the last.
        TreeExpression atOnce = source is null
            ? new TreeExpression.Call(runtime + Fill, [target, last, value])
            : new TreeExpression.Call(runtime + Move, [source, target, last]);
        var done = new TreeStatement.Assign(variable, atOnce);

        // A fill of a length known to be long needs no loop beside it.
        if (source is null && known is not null)
        {
            return [new TreeStatement.Note($"A loop that fills, done at once; \"{Setting}\": true in the translation's settings writes the loop."), done];
        }

        TreeExpression condition = source is null
            ? new TreeExpression.Binary(TreeExpression.Operation.GreaterOrEqual, last, new TreeExpression.Local(runtime + LongRunName))
            : new TreeExpression.Call(runtime + ShouldMove, [source, target, last]);
        return
        [
            new TreeStatement.Note($"A long run is done at once and a short one by the loop; \"{Setting}\": true in the translation's settings writes the loop alone."),
            new TreeStatement.If([new TreeStatement.Branch(condition, [done]), new TreeStatement.Branch(null, [loop])]),
        ];
    }

    // A loop from the first element, one at a time, whose one statement
    // tests the element of its turn against a whole number and, where
    // they are equal, does what ends in a return. The runtime gives back
    // where the loop would stand: at the first element found, or one
    // past the last. Nothing where the statement is no such loop, or its
    // length is known to be short.
    private static IReadOnlyList<TreeStatement>? ReplaceSearch(TreeStatement statement, string runtime, Dictionary<string, long?> constants, HashSet<string> numbers, HashSet<string> arrays)
    {
        if (statement is not TreeStatement.For loop
            || loop.Start is not TreeStatement.Assign { Target: TreeExpression.Local variable, Value: TreeExpression.Integer { Value: 1 } }
            || loop.Condition is not TreeExpression.Binary { Operator: TreeExpression.Operation.LessOrEqual, Left: TreeExpression.Local counted, Right: var last }
            || loop.Step is not [TreeStatement.Advance { Target: TreeExpression.Local advanced, Amount: TreeExpression.Integer { Value: 1 }, IsSubtracted: false }]
            || loop.Body is not [TreeStatement.If { Branches: [{ Condition: TreeExpression.Binary { Operator: TreeExpression.Operation.Equal, Left: var left, Right: var right }, Body: [.., TreeStatement.Return] found }] }]
            || counted != variable
            || advanced != variable
            || last == variable
            || !numbers.Contains(variable.Name)
            || !TryCount(last, constants, out var known)
            || known < LongRun)
        {
            return null;
        }

        bool IsSearched(TreeExpression side) =>
            side is TreeExpression.Indexed { Address: TreeExpression.Local array } element && arrays.Contains(array.Name) && IsElementOfTheTurn(element, variable);

        bool IsSought(TreeExpression side) => side switch
        {
            TreeExpression.Integer => true,
            TreeExpression.Local local => local != variable && numbers.Contains(local.Name),
            TreeExpression.Dereference { Address: TreeExpression.Local pointer } => arrays.Contains(pointer.Name),
            _ => false,
        };

        var (searched, sought) = IsSearched(left) ? (left, right) : (right, left);
        if (!IsSearched(searched) || !IsSought(sought))
        {
            return null;
        }

        // A short run costs the loop less than the call does, so both ways
        // are written where the length is not known, as for a copy.
        TreeStatement[] atOnce =
        [
            new TreeStatement.Assign(variable, new TreeExpression.Call(runtime + Find, [((TreeExpression.Indexed)searched).Address, last, sought])),
            new TreeStatement.If([new TreeStatement.Branch(new TreeExpression.Binary(TreeExpression.Operation.LessOrEqual, variable, last), found)]),
        ];
        if (known is not null)
        {
            return [new TreeStatement.Note($"A loop that looks for a value, done by the runtime; \"{Setting}\": true in the translation's settings writes the loop."), .. atOnce];
        }

        var isLong = new TreeExpression.Binary(TreeExpression.Operation.GreaterOrEqual, last, new TreeExpression.Local(runtime + LongRunName));
        return
        [
            new TreeStatement.Note($"A long run is looked through by the runtime and a short one by the loop; \"{Setting}\": true in the translation's settings writes the loop alone."),
            new TreeStatement.If([new TreeStatement.Branch(isLong, atOnce), new TreeStatement.Branch(null, [loop])]),
        ];
    }

    // How many elements a loop to a last element has: a number where the
    // last is one or is a constant the method names as one, and nothing
    // where it is a variable. A constant named by anything but a whole
    // number is not counted at all, its value not being known here.
    private static bool TryCount(TreeExpression last, Dictionary<string, long?> constants, out long? count)
    {
        count = null;
        switch (last)
        {
            case TreeExpression.Integer number:
                count = number.Value;
                return true;
            case TreeExpression.Local named when constants.TryGetValue(named.Name, out var value):
                count = value;
                return value is not null;
            default:
                return last is TreeExpression.Local;
        }
    }

    // Whether an element is the one the loop's variable names in its
    // turn, the first for one, of an array that has one subscript from
    // one: the offset is the variable less one, however the emitter came
    // to build it.
    private static bool IsElementOfTheTurn(TreeExpression.Indexed element, TreeExpression.Local variable) =>
        CSharpExpressionWriter.Write(element.Offset) == variable.Name + " - 1";

    // A constant, or a variable that is neither the loop's nor the array
    // stored to. One reached through a pointer may lie in the array, and
    // then the loop stores its own value back to it, which changes
    // nothing.
    private static bool IsUnchangedByTheLoop(TreeExpression value, TreeExpression.Local variable, TreeExpression.Local target) => value switch
    {
        TreeExpression.Integer or TreeExpression.Number => true,
        TreeExpression.Unary { Operator: TreeExpression.Operation.Negate, Operand: TreeExpression.Integer or TreeExpression.Number } => true,
        TreeExpression.Local local => local != variable && local != target,
        TreeExpression.Dereference { Address: TreeExpression.Local pointer } => pointer != variable && pointer != target,
        _ => false,
    };
}
