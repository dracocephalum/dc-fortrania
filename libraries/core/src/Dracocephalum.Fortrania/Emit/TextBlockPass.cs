namespace Dracocephalum.Fortrania.Emit;

/// <summary>
/// The pass that has a method reach its character constants in the block
/// its library keeps them in: the address of each becomes the block's
/// address and so many bytes on, with the constant beside it as a note,
/// and the method takes the block's address once.
/// </summary>
/// <remarks>
/// <para>
/// Each constant written by itself is several calls for the just-in-time
/// compiler to write in line, the making of a span and the taking of its
/// address, and that compiler writes only so many in line in one method.
/// A method with some hundreds of constants uses the allowance up, and
/// from there on every one is a call with a slot of its own in the
/// method's frame, and so is every other small procedure of the runtime
/// the method uses. An address and a number added to it are no call.
/// </para>
/// <para>
/// A method written as one expression is left as it is, having nowhere to
/// take the block's address, and so is a function of a method's own. A
/// constant given as a span, as an input or output statement gives one,
/// is left too: it is not an address.
/// </para>
/// </remarks>
internal static class TextBlockPass
{
    /// <summary>The field of a library that holds the address of its block.</summary>
    public const string BlockField = "_textBlock";

    private const string Block = "_text";
    private const string AddressOfConstant = "AddrOf";

    /// <summary>Rewrites the methods of a file that take the address of a constant.</summary>
    /// <param name="file">The file as the emitter made it.</param>
    /// <param name="block">The block of the file's library, which is given the constants the file has.</param>
    public static TreeFile Apply(TreeFile file, TextBlock block) =>
        file with { Members = [.. file.Members.Select(member => member is TreeMethod { IsOneExpression: false } method ? Apply(method, block) : member)] };

    private static TreeMethod Apply(TreeMethod method, TextBlock block)
    {
        var found = false;
        TreeExpression Place(TreeExpression node)
        {
            if (node is not TreeExpression.Call { Arguments: [TreeExpression.Utf8 constant] } call || !IsAddressOfConstant(call.Method))
            {
                return node;
            }

            found = true;
            var offset = block.Add(constant.Text);
            var start = new TreeExpression.Local(Block);
            return new TreeExpression.Noted(
                offset == 0 ? start : new TreeExpression.Sum([new(start), new(new TreeExpression.Integer(offset))]),
                constant.Text.ToUtf8Literal()[..^2]);
        }

        var declarations = TreeRewriter.Rewrite(method.Declarations, Place);
        var body = TreeRewriter.Rewrite(method.Body, Place);
        return found
            ? method with
            {
                Declarations = [new TreeStatement.Declare("byte*", Block, new TreeExpression.Local(BlockField)), .. declarations],
                Body = body,
            }
            : method;
    }

    // The runtime's procedure by its own name, or by the name the file
    // gives the runtime's class where it has to.
    private static bool IsAddressOfConstant(string method) =>
        method == AddressOfConstant || method.EndsWith("." + AddressOfConstant, StringComparison.Ordinal);
}
