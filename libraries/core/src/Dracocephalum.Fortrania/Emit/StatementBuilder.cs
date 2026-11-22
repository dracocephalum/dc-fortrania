namespace Dracocephalum.Fortrania.Emit;

/// <summary>
/// Collects the statements of a method as they are made, block within
/// block. A part of the emitter that has been moved to the tree adds
/// nodes, and takes a block's statements back to put them in the node the
/// block belongs to. Comments have not been moved, and are written as
/// lines, as they were when this was source text.
/// </summary>
internal sealed class StatementBuilder : ILineWriter
{
    // The block being filled is the last; the first is the method's own.
    private readonly List<List<TreeStatement>> _blocks = [[]];

    /// <summary>Gets the statements of the method, once every block begun has been ended.</summary>
    public IReadOnlyList<TreeStatement> Statements => _blocks.Count == 1
        ? _blocks[0]
        : throw new InvalidOperationException("A block is still open.");

    /// <inheritdoc/>
    public bool IsEmpty => _blocks.Count == 1 && Current.Count == 0;

    /// <inheritdoc/>
    public string LastLine => Current.Count == 0
        ? (_blocks.Count == 1 ? string.Empty : "{")
        : Current[^1] switch
        {
            TreeStatement.Blank => string.Empty,
            TreeStatement.If or TreeStatement.While or TreeStatement.For => "}",
            var statement => CSharpStatementWriter.WriteLine(statement)!.TrimStart(),
        };

    /// <summary>Gets a value indicating whether the first thing in the method is a comment.</summary>
    public bool StartsWithComment =>
        _blocks[0] is [TreeStatement.Text first, ..] && first.Line.TrimStart().StartsWith("//", StringComparison.Ordinal);

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

    /// <inheritdoc/>
    public void Line(string line) => Add(new TreeStatement.Text(line));

    /// <inheritdoc/>
    public void Blank() => Add(new TreeStatement.Blank());
}
