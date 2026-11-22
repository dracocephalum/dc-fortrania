using System.Text;

namespace Dracocephalum.Fortrania.Emit;

/// <summary>
/// The character constants of one library, laid one after another as the
/// bytes its class keeps in the assembly. A unit written with it reaches a
/// constant by where it starts in the block, and the class is written with
/// the block once every unit has been.
/// </summary>
/// <remarks>
/// Whoever takes a constant is given its length beside its address, as a
/// FORTRAN procedure is, so nothing has to mark where one ends. Each is
/// followed by a zero byte all the same, and by as many more as bring the
/// next to a multiple of eight bytes from the start: one read as a whole
/// word at a time starts on a word, and one read past its end by mistake
/// gives zeros and not the constant after it. A constant that occurs
/// again, in any unit of the library, has the place it was given first.
/// </remarks>
public sealed class TextBlock
{
    private const int Alignment = 8;

    private readonly Dictionary<string, long> _offsets = new(StringComparer.Ordinal);
    private readonly StringBuilder _text = new();

    /// <summary>Gets a value indicating whether no constant has been given a place.</summary>
    public bool IsEmpty => _text.Length == 0;

    /// <summary>Gives a constant its place in the block.</summary>
    /// <param name="text">The constant, in ASCII.</param>
    /// <returns>How many bytes from the start of the block it begins.</returns>
    public long Add(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        if (!_offsets.TryGetValue(text, out var offset))
        {
            offset = _text.Length;
            _offsets.Add(text, offset);
            _text.Append(text);
            _text.Append('\0', Alignment - (_text.Length % Alignment));
        }

        return offset;
    }

    /// <summary>Gives the whole block, every constant in the order it was added, with the zero bytes after each.</summary>
    /// <returns>The text of the block.</returns>
    public override string ToString() => _text.ToString();
}
