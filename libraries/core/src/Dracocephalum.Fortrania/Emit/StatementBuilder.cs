namespace Dracocephalum.Fortrania.Emit;

/// <summary>
/// Collects statements as they are made, block within block: the emitter
/// adds nodes, and takes a block's statements back to put them in the node
/// the block is the body of. It also says what the last thing added was,
/// which is what decides whether a blank line goes before a comment.
/// </summary>
internal sealed class StatementBuilder
{
    // The block being filled is the last; the first is the method's own.
    private readonly List<List<TreeStatement>> _blocks = [[]];

    /// <summary>Gets the statements of the method, once every block begun has been ended.</summary>
    public IReadOnlyList<TreeStatement> Statements => _blocks.Count == 1
        ? _blocks[0]
        : throw new InvalidOperationException("A block is still open.");

    /// <summary>Gets a value indicating whether nothing has been added and no block begun.</summary>
    public bool IsEmpty => _blocks.Count == 1 && Current.Count == 0;

    /// <summary>
    /// Gets a value indicating whether a blank line here would be one too
    /// many: at the very start, straight after a block is begun, or after
    /// another blank line.
    /// </summary>
    public bool IsBlankNeedless => Current.Count == 0 || Current[^1] is TreeStatement.Blank;

    /// <summary>Gets a value indicating whether the last thing added is a line of comment.</summary>
    public bool EndsOnComment => Current is [.., TreeStatement.Comment];

    private List<TreeStatement> Current => _blocks[^1];

    public void Add(TreeStatement statement) => Current.Add(statement);

    /// <summary>Starts a block, which the statements added from here on fill.</summary>
    public void Begin() => _blocks.Add([]);

    /// <summary>Ends the block being filled and gives its statements, for the node it is the body of.</summary>
    public IReadOnlyList<TreeStatement> End()
    {
        if (_blocks.Count == 1)
        {
            throw new InvalidOperationException("No block is open.");
        }

        var statements = _blocks[^1];
        _blocks.RemoveAt(_blocks.Count - 1);
        return statements;
    }

    public void Comment(string text) => Add(new TreeStatement.Comment(text));

    public void Blank() => Add(new TreeStatement.Blank());
}
