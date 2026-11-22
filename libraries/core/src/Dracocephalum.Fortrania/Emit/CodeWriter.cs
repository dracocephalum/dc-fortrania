using System.Text;

namespace Dracocephalum.Fortrania.Emit;

/// <summary>Collects lines of source text at a running depth of indentation.</summary>
internal sealed class CodeWriter
{
    private readonly StringBuilder _text = new();
    private int _depth;

    /// <summary>Gets the last line written, without its indentation; empty when it was blank or there is none.</summary>
    public string LastLine { get; private set; } = string.Empty;

    /// <summary>Gets the first line written, without its indentation; empty when it was blank or there is none.</summary>
    public string FirstLine { get; private set; } = string.Empty;

    public void Line(string line)
    {
        if (IsEmpty)
        {
            FirstLine = line.TrimStart();
        }

        _text.Append(' ', _depth * 4).Append(line).Append('\n');
        LastLine = line.TrimStart();
    }

    public void Blank()
    {
        _text.Append('\n');
        LastLine = string.Empty;
    }

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

    /// <summary>Gets a value indicating whether nothing has been written.</summary>
    public bool IsEmpty => _text.Length == 0;

    /// <summary>Adds lines written elsewhere, each moved in to the current depth, blank ones kept.</summary>
    public void Append(CodeWriter other)
    {
        var lines = other.ToString().Split('\n');
        foreach (var line in lines[..^1])
        {
            if (line.Length == 0)
            {
                Blank();
            }
            else
            {
                Line(line);
            }
        }
    }

    public override string ToString() => _text.ToString();
}
