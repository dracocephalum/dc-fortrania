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
    /// The runtime's own <c>Assign</c>, line for line, as the control: it
    /// lies in this assembly as the candidates do and is called as they
    /// are, so that where a method lies and whether it is compiled into
    /// its caller is no part of a difference between them.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void AssignAllowingOverlap(byte* target, int targetLength, byte* source, int sourceLength)
    {
        if (targetLength <= 0)
        {
            return;
        }

        if (sourceLength >= targetLength)
        {
            Buffer.MemoryCopy(source, target, targetLength, targetLength);
            return;
        }

        if (sourceLength < 0)
        {
            sourceLength = 0;
        }

        Buffer.MemoryCopy(source, target, targetLength, sourceLength);
        new Span<byte>(target + sourceLength, targetLength - sourceLength).Fill(Blank);
    }

    /// <summary>
    /// Assigns as the runtime's <c>Assign</c> does, for two values known not
    /// to overlap: the copy then need not look which way to go.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
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
}
