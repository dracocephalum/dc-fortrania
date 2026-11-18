using System.Text;

namespace Dracocephalum.Fortrania.Emit;

/// <summary>Collects lines of source text at a running depth of indentation.</summary>
internal sealed class CodeWriter
{
    private readonly StringBuilder _text = new();
    private int _depth;

    public void Line(string line) => _text.Append(' ', _depth * 4).Append(line).Append('\n');

    public void Blank() => _text.Append('\n');

    public void Open(string header)
    {
        Line(header);
        Line("{");
        _depth++;
    }

    public void Close()
    {
        _depth--;
        Line("}");
    }

    /// <summary>Adds lines written elsewhere, each moved in to the current depth.</summary>
    public void Lines(CodeWriter other)
    {
        foreach (var line in other.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            Line(line);
        }
    }

    public override string ToString() => _text.ToString();
}
