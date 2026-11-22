namespace Dracocephalum.Fortrania.Emit;

/// <summary>
/// Collects the statements of a method as they are made, block within
/// block. A part of the emitter that has been moved to the tree adds
/// nodes, and takes a block's statements back to put them in the node the
/// block belongs to; a part that has not writes lines and opens and
/// closes blocks as it did when this was source text.
/// </summary>
internal sealed class StatementBuilder : ILineWriter
{
    // The block being filled is the last; the first is the method's own.
    // A block opened by its heading as text keeps the heading beside it.
    private readonly List<(List<TreeStatement> Statements, string? Heading)> _blocks = [([], null)];

    /// <summary>Gets the statements of the method, once every block opened has been closed.</summary>
    public IReadOnlyList<TreeStatement> Statements => _blocks.Count == 1
        ? _blocks[0].Statements
        : throw new InvalidOperationException("A block is still open.");

    /// <inheritdoc/>
    public bool IsEmpty => _blocks.Count == 1 && Current.Count == 0;

    /// <inheritdoc/>
    public string LastLine => Current.Count == 0
        ? (_blocks.Count == 1 ? string.Empty : "{")
        : Current[^1] switch
        {
            TreeStatement.Blank => string.Empty,
            TreeStatement.If or TreeStatement.While or TreeStatement.For or TreeStatement.TextBlock => "}",
            var statement => CSharpStatementWriter.WriteLine(statement)!.TrimStart(),
        };

    /// <summary>Gets a value indicating whether the first thing in the method is a comment.</summary>
    public bool StartsWithComment =>
        _blocks[0].Statements is [TreeStatement.Text first, ..] && first.Line.TrimStart().StartsWith("//", StringComparison.Ordinal);

    private List<TreeStatement> Current => _blocks[^1].Statements;

    public void Add(TreeStatement statement) => Current.Add(statement);

    /// <summary>Starts a block, which the statements added from here on fill.</summary>
    public void Begin() => _blocks.Add(([], null));

    /// <summary>Ends the block being filled and gives its statements, for the node it is the body of.</summary>
    public IReadOnlyList<TreeStatement> End()
    {
        var (statements, heading) = _blocks[^1];
        if (heading is not null || _blocks.Count == 1)
        {
            throw new InvalidOperationException("No block begun is open.");
        }

        _blocks.RemoveAt(_blocks.Count - 1);
        return statements;
    }

    /// <inheritdoc/>
    public void Line(string line) => Add(new TreeStatement.Text(line));

    /// <inheritdoc/>
    public void Blank() => Add(new TreeStatement.Blank());

    /// <summary>Starts a block under a heading of source text.</summary>
    public void Open(string heading) => _blocks.Add(([], heading));

    /// <summary>Ends a block started by <see cref="Open"/>.</summary>
    public void Close()
    {
        var (statements, heading) = _blocks[^1];
        _blocks.RemoveAt(_blocks.Count - 1);
        Add(new TreeStatement.TextBlock(heading ?? throw new InvalidOperationException("No block opened is open."), statements));
    }
}
