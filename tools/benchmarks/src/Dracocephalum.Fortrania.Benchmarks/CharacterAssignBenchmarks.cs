using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Dracocephalum.Fortrania.Runtime;

namespace Dracocephalum.Fortrania.Benchmarks;

/// <summary>
/// What assigning a character value costs, in the reference sources'
/// commonest shapes: one byte to one byte, a short name to a longer
/// variable, a variable to one of its own length, and a blank to a whole
/// variable. The runtime's <c>Assign</c> is set against the same done by
/// the framework's copy and fill whatever the lengths, which is what it
/// was, and for one byte against the byte stored where it stands, which is
/// what a translation writes.
/// </summary>
/// <remarks>
/// <para>
/// Source and target lie a fixed distance apart in one allocation, and the
/// place is one of several taken in turn.
/// </para>
/// <para>
/// The lengths are read from fields and not written where they are used.
/// A length the compiler can see is one it writes the copy out for, with
/// either form, and what is measured here is the other kind: a length
/// worked out as the program runs, or a call the compiler did not write
/// in line.
/// </para>
/// </remarks>
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
[SkipLocalsInit]
public unsafe class CharacterAssignBenchmarks
{
    private const string OneToOne = "1 byte to 1";
    private const string SixToThirtyTwo = "6 bytes to 32";
    private const string ThirtyTwoToThirtyTwo = "32 bytes to 32";
    private const string TwentyEightToEighty = "28 bytes to 80";
    private const string BlankToEighty = "a blank to 80";
    private const int Places = 64;
    private const int Stride = 128;
    private const byte Blank = (byte)' ';

    private byte* _memory;
    private byte* _sources;
    private byte* _targets;
    private int _one;
    private int _six;
    private int _twentyEight;
    private int _thirtyTwo;
    private int _eighty;

    [GlobalSetup]
    public void Allocate()
    {
        _memory = (byte*)NativeMemory.Alloc(2 * Places * Stride);
        _sources = _memory;
        _targets = _memory + (Places * Stride);
        for (var index = 0; index < 2 * Places * Stride; index++)
        {
            _memory[index] = (byte)('A' + (index % 23));
        }

        (_one, _six, _twentyEight, _thirtyTwo, _eighty) = (1, 6, 28, 32, 80);
    }

    [GlobalCleanup]
    public void Free() => NativeMemory.Free(_memory);

    [Benchmark(Baseline = true, OperationsPerInvoke = BenchmarkConfig.Repeats)]
    [BenchmarkCategory(OneToOne)]
    public int OneByAssign() => ByAssign(_one, _one);

    [Benchmark(OperationsPerInvoke = BenchmarkConfig.Repeats)]
    [BenchmarkCategory(OneToOne)]
    public int OneByCopyAndFill() => ByCopyAndFill(_one, _one);

    [Benchmark(OperationsPerInvoke = BenchmarkConfig.Repeats)]
    [BenchmarkCategory(OneToOne)]
    public int OneByItsByte()
    {
        for (var repeat = 0; repeat < BenchmarkConfig.Repeats; repeat++)
        {
            var place = (repeat & (Places - 1)) * Stride;
            *(_targets + place) = *(_sources + place);
        }

        return _targets[0];
    }

    [Benchmark(Baseline = true, OperationsPerInvoke = BenchmarkConfig.Repeats)]
    [BenchmarkCategory(SixToThirtyTwo)]
    public int SixByAssign() => ByAssign(_thirtyTwo, _six);

    [Benchmark(OperationsPerInvoke = BenchmarkConfig.Repeats)]
    [BenchmarkCategory(SixToThirtyTwo)]
    public int SixByCopyAndFill() => ByCopyAndFill(_thirtyTwo, _six);

    [Benchmark(Baseline = true, OperationsPerInvoke = BenchmarkConfig.Repeats)]
    [BenchmarkCategory(ThirtyTwoToThirtyTwo)]
    public int ThirtyTwoByAssign() => ByAssign(_thirtyTwo, _thirtyTwo);

    [Benchmark(OperationsPerInvoke = BenchmarkConfig.Repeats)]
    [BenchmarkCategory(ThirtyTwoToThirtyTwo)]
    public int ThirtyTwoByCopyAndFill() => ByCopyAndFill(_thirtyTwo, _thirtyTwo);

    [Benchmark(Baseline = true, OperationsPerInvoke = BenchmarkConfig.Repeats)]
    [BenchmarkCategory(TwentyEightToEighty)]
    public int TwentyEightByAssign() => ByAssign(_eighty, _twentyEight);

    [Benchmark(OperationsPerInvoke = BenchmarkConfig.Repeats)]
    [BenchmarkCategory(TwentyEightToEighty)]
    public int TwentyEightByCopyAndFill() => ByCopyAndFill(_eighty, _twentyEight);

    [Benchmark(Baseline = true, OperationsPerInvoke = BenchmarkConfig.Repeats)]
    [BenchmarkCategory(BlankToEighty)]
    public int BlankByAssign() => ByAssign(_eighty, _one);

    [Benchmark(OperationsPerInvoke = BenchmarkConfig.Repeats)]
    [BenchmarkCategory(BlankToEighty)]
    public int BlankByCopyAndFill() => ByCopyAndFill(_eighty, _one);

    /// <summary>Gives the bytes one assignment of each of many shapes leaves behind, for a test to compare.</summary>
    /// <param name="useRuntime">Whether the runtime assigns; otherwise the framework's copy and fill do.</param>
    /// <returns>The targets and what lies around them, one shape after another.</returns>
    public byte[] Assign(bool useRuntime)
    {
        var result = new List<byte>();
        for (var target = -1; target <= 70; target++)
        {
            for (var source = -1; source <= 70; source++)
            {
                new Span<byte>(_targets, Stride).Fill((byte)'#');
                if (useRuntime)
                {
                    Fortran.Assign(_targets + 8, target, _sources, source);
                }
                else
                {
                    AssignByCopyAndFill(_targets + 8, target, _sources, source);
                }

                result.AddRange(new ReadOnlySpan<byte>(_targets, Stride));
            }
        }

        return [.. result];
    }

    // What the runtime's Assign was. It is kept from being written in
    // line as the runtime's is, by lying in another assembly as it does
    // not, so that the two are called alike.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void AssignByCopyAndFill(byte* target, int targetLength, byte* source, int sourceLength)
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

    private int ByAssign(int targetLength, int sourceLength)
    {
        for (var repeat = 0; repeat < BenchmarkConfig.Repeats; repeat++)
        {
            var place = (repeat & (Places - 1)) * Stride;
            Fortran.Assign(_targets + place, targetLength, _sources + place, sourceLength);
        }

        return _targets[0];
    }

    private int ByCopyAndFill(int targetLength, int sourceLength)
    {
        for (var repeat = 0; repeat < BenchmarkConfig.Repeats; repeat++)
        {
            var place = (repeat & (Places - 1)) * Stride;
            AssignByCopyAndFill(_targets + place, targetLength, _sources + place, sourceLength);
        }

        return _targets[0];
    }
}
