namespace Dracocephalum.Fortrania.Emit;

/// <summary>
/// What takes lines of comment: source text being collected, or the
/// statements of a method being built, where a comment is still a line of
/// text until the tree has a node for it.
/// </summary>
internal interface ILineWriter
{
    /// <summary>Gets a value indicating whether nothing has been written.</summary>
    bool IsEmpty { get; }

    /// <summary>Gets the last line written, without its indentation; empty when it was blank or there is none.</summary>
    string LastLine { get; }

    void Line(string line);

    void Blank();
}
