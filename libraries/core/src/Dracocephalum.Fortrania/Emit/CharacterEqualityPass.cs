namespace Dracocephalum.Fortrania.Emit;

/// <summary>
/// The pass that has two character values tested for equality by a test
/// that only says whether they are equal. The emitter writes every
/// comparison of character values as the runtime's ordering of the two
/// and a question about how it came out; where the question is whether
/// they are equal, the ordering is more than is asked for.
/// </summary>
/// <remarks>
/// <para>
/// One byte against one byte becomes a comparison of the two bytes,
/// written where it stands. Anything else becomes the runtime's test for
/// equality, which compares the first bytes where it is used and is a
/// call only for values that agree in them; <c>DESIGN.md</c>,
/// <em>Equality of character values is tested as equality</em>, has the
/// measurements.
/// </para>
/// <para>
/// It is to run before the pass that moves constants to a library's
/// block, while a constant is still to be seen for what it is.
/// </para>
/// </remarks>
internal static class CharacterEqualityPass
{
    private const string Ordering = "Compare";
    private const string Equality = "IsEqual";
    private const string AddressOfConstant = "AddrOf";

    /// <summary>Rewrites the tests for equality of character values in the methods of a file.</summary>
    /// <param name="file">The file as the emitter made it.</param>
    public static TreeFile Apply(TreeFile file) =>
        file with
        {
            Members =
            [
                .. file.Members.Select(member => member is TreeMethod method
                    ? method with
                    {
                        Declarations = TreeRewriter.Rewrite(method.Declarations, ToEqualityTest),
                        Body = TreeRewriter.Rewrite(method.Body, ToEqualityTest),
                    }
                    : member),
            ],
        };

    /// <summary>Tells whether the target language writes a character between two marks with nothing before it.</summary>
    /// <param name="character">The character.</param>
    internal static bool IsWrittenAsItself(char character) => character is >= ' ' and < (char)127 and not '\'' and not '\\';

    private static TreeExpression ToEqualityTest(TreeExpression node)
    {
        if (node is not TreeExpression.Binary { Operator: TreeExpression.Operation.Equal or TreeExpression.Operation.NotEqual, Right: TreeExpression.Integer { Value: 0 } } asked
            || asked.Left is not TreeExpression.Call { Arguments: [var left, var leftLength, var right, var rightLength] } ordering
            || !IsNamed(ordering.Method, Ordering))
        {
            return node;
        }

        // Equality does not mind which side a constant is on; the test
        // for one byte is written with it on the right, as a person would.
        if (ReadConstant(left, leftLength) is not null && ReadConstant(right, rightLength) is null)
        {
            (left, leftLength, right, rightLength) = (right, rightLength, left, leftLength);
        }

        var isOneByteEach = leftLength is TreeExpression.Integer { Value: 1 } && rightLength is TreeExpression.Integer { Value: 1 };
        TreeExpression test = isOneByteEach && ReadByte(right, rightLength) is { } other
            ? new TreeExpression.Binary(TreeExpression.Operation.Equal, new TreeExpression.Dereference(left), other)
            : new TreeExpression.Call(ordering.Method[..^Ordering.Length] + Equality, [left, leftLength, right, rightLength]);

        if (asked.Operator == TreeExpression.Operation.Equal)
        {
            return test;
        }

        return test is TreeExpression.Binary bytes
            ? bytes with { Operator = TreeExpression.Operation.NotEqual }
            : new TreeExpression.Unary(TreeExpression.Operation.Not, test);
    }

    // The one byte a value of that length is, to compare another with: a
    // constant's character where the target language writes it plainly,
    // what lies at the address of anything that is no constant, and
    // nothing for a constant that has to stay a constant.
    private static TreeExpression? ReadByte(TreeExpression address, TreeExpression length) =>
        ReadConstant(address, length) switch
        {
            null => new TreeExpression.Dereference(address),
            [var character] when IsWrittenAsItself(character) => new TreeExpression.Character(character),
            _ => null,
        };

    // The text of a constant whose address and length the two are, or
    // nothing where they are anything else.
    private static string? ReadConstant(TreeExpression address, TreeExpression length)
    {
        var isConstant = address is TreeExpression.Call { Arguments: [TreeExpression.Utf8 { Text.Length: > 0 }] } call
            && IsNamed(call.Method, AddressOfConstant)
            && length is TreeExpression.Integer;
        return isConstant ? ((TreeExpression.Utf8)((TreeExpression.Call)address).Arguments[0]).Text : null;
    }

    // The runtime's procedure by its own name, or by the name the file
    // gives the runtime's class where it has to.
    private static bool IsNamed(string method, string name) =>
        method == name || method.EndsWith("." + name, StringComparison.Ordinal);
}
