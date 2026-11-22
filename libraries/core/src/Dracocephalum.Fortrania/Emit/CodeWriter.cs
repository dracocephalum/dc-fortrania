using System.Text;

namespace Dracocephalum.Fortrania.Emit;

/// <summary>
/// Collects lines of source text at a running depth of indentation. A
/// writer of the program tree writes into one, and decides nothing by
/// what it holds.
/// </summary>
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

    public override string ToString() => _text.ToString();
}
