namespace Dracocephalum.Fortrania.Emit;

/// <summary>
/// The pass that has a character value tested for equality with a
/// constant by a test that only says whether they are equal. The emitter
/// writes every comparison of character values as the runtime's ordering
/// of the two and a question about how it came out; where the question
/// is whether they are equal, and one side is a constant, the ordering
/// is more than is asked for.
/// </summary>
/// <remarks>
/// <para>
/// One byte against a constant of one byte becomes a comparison of the
/// two bytes, written where it stands. Anything else becomes the
/// runtime's test for equality, which compares the first bytes where it
/// is used and is a call only for values that agree in them;
/// <c>DESIGN.md</c>, <em>Equality with a constant is tested as equality</em>,
/// has the measurements.
/// </para>
/// <para>
/// It is to run before the pass that moves constants to a library's
/// block, while a constant is still to be seen for what it is.
/// </para>
/// </remarks>
internal static class ConstantEqualityPass
{
    private const string Ordering = "Compare";
    private const string Equality = "IsEqual";
    private const string AddressOfConstant = "AddrOf";

    /// <summary>Rewrites the tests for equality with a constant in the methods of a file.</summary>
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

    private static TreeExpression ToEqualityTest(TreeExpression node)
    {
        if (node is not TreeExpression.Binary { Operator: TreeExpression.Operation.Equal or TreeExpression.Operation.NotEqual, Right: TreeExpression.Integer { Value: 0 } } asked
            || asked.Left is not TreeExpression.Call { Arguments: [var left, var leftLength, var right, var rightLength] } ordering
            || !IsNamed(ordering.Method, Ordering))
        {
            return node;
        }

        // Equality does not mind which side the constant is on.
        TreeExpression value;
        TreeExpression length;
        TreeExpression constant;
        string text;
        if (ReadConstant(right, rightLength) is { } onTheRight)
        {
            (value, length, constant, text) = (left, leftLength, right, onTheRight);
        }
        else if (ReadConstant(left, leftLength) is { } onTheLeft)
        {
            (value, length, constant, text) = (right, rightLength, left, onTheLeft);
        }
        else
        {
            return node;
        }

        TreeExpression test = text.Length == 1 && length is TreeExpression.Integer { Value: 1 } && IsWrittenAsItself(text[0])
            ? new TreeExpression.Binary(TreeExpression.Operation.Equal, new TreeExpression.Dereference(value), new TreeExpression.Character(text[0]))
            : new TreeExpression.Call(
                ordering.Method[..^Ordering.Length] + Equality,
                [value, length, constant, new TreeExpression.Integer(text.Length)]);

        if (asked.Operator == TreeExpression.Operation.Equal)
        {
            return test;
        }

        return test is TreeExpression.Binary bytes
            ? bytes with { Operator = TreeExpression.Operation.NotEqual }
            : new TreeExpression.Unary(TreeExpression.Operation.Not, test);
    }

    // The text of a constant whose address and length the two are, or
    // nothing where they are anything else.
    private static string? ReadConstant(TreeExpression address, TreeExpression length)
    {
        var isConstant = address is TreeExpression.Call { Arguments: [TreeExpression.Utf8 { Text.Length: > 0 }] } call
            && IsNamed(call.Method, AddressOfConstant)
            && length is TreeExpression.Integer;
        return isConstant ? ((TreeExpression.Utf8)((TreeExpression.Call)address).Arguments[0]).Text : null;
    }

    // A character the target language writes between two marks with
    // nothing before it.
    private static bool IsWrittenAsItself(char character) => character is >= ' ' and < (char)127 and not '\'' and not '\\';

    // The runtime's procedure by its own name, or by the name the file
    // gives the runtime's class where it has to.
    private static bool IsNamed(string method, string name) =>
        method == name || method.EndsWith("." + name, StringComparison.Ordinal);
}
