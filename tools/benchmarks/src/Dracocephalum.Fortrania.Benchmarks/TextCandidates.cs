using System.Runtime.CompilerServices;

namespace Dracocephalum.Fortrania.Benchmarks;

/// <summary>
/// Other ways of doing what the runtime does with character values, kept
/// here to be measured against it. One that proves faster moves into the
/// runtime and leaves this class; one that does not is deleted with its
/// benchmark, and its figures stay in the change that took it out.
/// </summary>
internal static unsafe class TextCandidates
{
    private const byte Blank = (byte)' ';

    /// <summary>
    /// Assigns as the runtime's <c>Assign</c> does, for two values known not
    /// to overlap: the copy then need not look which way to go.
    /// </summary>
    public static void AssignDistinct(byte* target, int targetLength, byte* source, int sourceLength)
    {
        if (targetLength <= 0)
        {
            return;
        }

        if (sourceLength >= targetLength)
        {
            Unsafe.CopyBlockUnaligned(target, source, (uint)targetLength);
            return;
        }

        if (sourceLength < 0)
        {
            sourceLength = 0;
        }

        Unsafe.CopyBlockUnaligned(target, source, (uint)sourceLength);
        new Span<byte>(target + sourceLength, targetLength - sourceLength).Fill(Blank);
    }

    /// <summary>
    /// Says whether two character values are equal, the shorter taken as
    /// followed by blanks. It does not find their order, so it can stop at
    /// the first difference without saying which way it goes.
    /// </summary>
    public static bool AreEqual(byte* left, int leftLength, byte* right, int rightLength)
    {
        leftLength = Math.Max(leftLength, 0);
        rightLength = Math.Max(rightLength, 0);
        var common = Math.Min(leftLength, rightLength);

        if (!new ReadOnlySpan<byte>(left, common).SequenceEqual(new ReadOnlySpan<byte>(right, common)))
        {
            return false;
        }

        var rest = leftLength > common
            ? new ReadOnlySpan<byte>(left + common, leftLength - common)
            : new ReadOnlySpan<byte>(right + common, rightLength - common);
        return !rest.ContainsAnyExcept(Blank);
    }
}
