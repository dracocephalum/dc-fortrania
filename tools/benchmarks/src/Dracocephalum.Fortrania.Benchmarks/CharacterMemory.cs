using System.Runtime.InteropServices;

namespace Dracocephalum.Fortrania.Benchmarks;

/// <summary>
/// Memory for the character values a benchmark works on, outside the
/// collected heap as a translated program's is, so that an address stays
/// good without pinning anything.
/// </summary>
/// <remarks>
/// The values of one benchmark lie in one page, at places a fixed distance
/// apart. Where they lie decides what is measured: a processor takes a
/// read for waiting on an earlier write when the two addresses agree in
/// their low twelve bits, and a copy in a loop then runs at half its speed.
/// Two allocations made apart land that way on one run and not on the
/// next, and the same benchmark gave either figure.
/// </remarks>
internal static unsafe class CharacterMemory
{
    /// <summary>The room each value has, and the distance from one to the next.</summary>
    public const int PlaceSize = 512;

    private const int PageSize = 4096;

    /// <summary>Takes a page of blanks.</summary>
    /// <returns>The address, to be given to <see cref="Free"/>.</returns>
    public static byte* AllocatePage()
    {
        var page = (byte*)NativeMemory.AlignedAlloc(PageSize, PageSize);
        new Span<byte>(page, PageSize).Fill((byte)' ');
        return page;
    }

    /// <summary>Stores a value at one of a page's places, the rest of the place left blank.</summary>
    /// <param name="page">A page from <see cref="AllocatePage"/>.</param>
    /// <param name="place">Which place, counted from zero; a page has eight.</param>
    /// <param name="value">The value's characters, each one byte, no more than a place holds.</param>
    /// <returns>The address of the value.</returns>
    public static byte* Store(byte* page, int place, string value)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(place, PageSize / PlaceSize);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(value.Length, PlaceSize);

        var address = page + (place * PlaceSize);
        for (var index = 0; index < value.Length; index++)
        {
            address[index] = (byte)value[index];
        }

        return address;
    }

    public static void Free(byte* page) => NativeMemory.AlignedFree(page);
}
