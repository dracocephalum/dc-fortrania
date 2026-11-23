using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Dracocephalum.Fortrania.Runtime;

namespace Dracocephalum.Fortrania.Benchmarks;

/// <summary>
/// What testing a character value for equality with a constant costs:
/// by the runtime's <c>IsEqual</c>, which is what a translation writes,
/// against its <c>Compare</c>, which orders two values of any lengths
/// and was what a translation wrote before, and against the whole test
/// written in line, which is faster where the two are equal and costs
/// the compiler that writes it in line several times as much. The shapes are the reference sources'
/// commonest: one byte against one, a short name in a longer variable,
/// and a variable of some length against one byte or a dozen.
/// </summary>
/// <remarks>
/// A value is taken from one of several places in turn, all holding
/// the same, so that a test written in line is done each time round and
/// not once before the loop.
/// </remarks>
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
[SkipLocalsInit]
public unsafe class ConstantEqualityBenchmarks
{
    private const string OneAgainstOne = "1 byte against 1";
    private const string EightAgainstFive = "8 bytes against 5";
    private const string LongAgainstOne = "32 bytes against 1";
    private const string LongAgainstTwelve = "32 bytes against 12";
    private const int Places = 64;
    private const int Stride = 32;
    private const byte Blank = (byte)' ';

    private byte* _memory;
    private byte* _one;
    private byte* _five;
    private byte* _twelve;
    private byte* _leftOne;
    private byte* _leftFive;
    private byte* _leftTwelve;

    /// <summary>Gets or sets a value indicating whether the values tested are equal to the constants; otherwise they differ in their first byte, as most do in a chain of tests.</summary>
    [Params(true, false)]
    public bool IsEqual { get; set; }

    [GlobalSetup]
    public void Allocate()
    {
        _memory = (byte*)NativeMemory.Alloc(((3 * Places) + 3) * Stride);
        new Span<byte>(_memory, ((3 * Places) + 3) * Stride).Fill(Blank);
        _one = Store(_memory, "U");
        _five = Store(_memory + Stride, "J2000");
        _twelve = Store(_memory + (2 * Stride), "ECLIPJ2000_X");
        _leftOne = _memory + (3 * Stride);
        _leftFive = _leftOne + (Places * Stride);
        _leftTwelve = _leftFive + (Places * Stride);
        for (var place = 0; place < Places; place++)
        {
            Store(_leftOne + (place * Stride), IsEqual ? "U" : "C");
            Store(_leftFive + (place * Stride), IsEqual ? "J2000" : "B1950");
            Store(_leftTwelve + (place * Stride), IsEqual ? "ECLIPJ2000_X" : "GALACTIC");
        }
    }

    [GlobalCleanup]
    public void Free() => NativeMemory.Free(_memory);

    [Benchmark(OperationsPerInvoke = BenchmarkConfig.Repeats)]
    [BenchmarkCategory(OneAgainstOne)]
    public int OneByCompare()
    {
        var equal = 0;
        for (var repeat = 0; repeat < BenchmarkConfig.Repeats; repeat++)
        {
            equal += Fortran.Compare(_leftOne + ((repeat & (Places - 1)) * Stride), 1, _one, 1) == 0 ? 1 : 0;
        }

        return equal;
    }

    [Benchmark(OperationsPerInvoke = BenchmarkConfig.Repeats)]
    [BenchmarkCategory(OneAgainstOne)]
    public int OneByTestInLine()
    {
        var equal = 0;
        for (var repeat = 0; repeat < BenchmarkConfig.Repeats; repeat++)
        {
            equal += IsEqualInLine(_leftOne + ((repeat & (Places - 1)) * Stride), 1, _one, 1) ? 1 : 0;
        }

        return equal;
    }

    [Benchmark(Baseline = true, OperationsPerInvoke = BenchmarkConfig.Repeats)]
    [BenchmarkCategory(OneAgainstOne)]
    public int OneByIsEqual()
    {
        var equal = 0;
        for (var repeat = 0; repeat < BenchmarkConfig.Repeats; repeat++)
        {
            equal += Fortran.IsEqual(_leftOne + ((repeat & (Places - 1)) * Stride), 1, _one, 1) ? 1 : 0;
        }

        return equal;
    }

    [Benchmark(OperationsPerInvoke = BenchmarkConfig.Repeats)]
    [BenchmarkCategory(OneAgainstOne)]
    public int OneByItsByte()
    {
        var equal = 0;
        for (var repeat = 0; repeat < BenchmarkConfig.Repeats; repeat++)
        {
            equal += *(_leftOne + ((repeat & (Places - 1)) * Stride)) == (byte)'U' ? 1 : 0;
        }

        return equal;
    }

    [Benchmark(OperationsPerInvoke = BenchmarkConfig.Repeats)]
    [BenchmarkCategory(EightAgainstFive)]
    public int FiveByCompare()
    {
        var equal = 0;
        for (var repeat = 0; repeat < BenchmarkConfig.Repeats; repeat++)
        {
            equal += Fortran.Compare(_leftFive + ((repeat & (Places - 1)) * Stride), 8, _five, 5) == 0 ? 1 : 0;
        }

        return equal;
    }

    [Benchmark(OperationsPerInvoke = BenchmarkConfig.Repeats)]
    [BenchmarkCategory(EightAgainstFive)]
    public int FiveByTestInLine()
    {
        var equal = 0;
        for (var repeat = 0; repeat < BenchmarkConfig.Repeats; repeat++)
        {
            equal += IsEqualInLine(_leftFive + ((repeat & (Places - 1)) * Stride), 8, _five, 5) ? 1 : 0;
        }

        return equal;
    }

    [Benchmark(Baseline = true, OperationsPerInvoke = BenchmarkConfig.Repeats)]
    [BenchmarkCategory(EightAgainstFive)]
    public int FiveByIsEqual()
    {
        var equal = 0;
        for (var repeat = 0; repeat < BenchmarkConfig.Repeats; repeat++)
        {
            equal += Fortran.IsEqual(_leftFive + ((repeat & (Places - 1)) * Stride), 8, _five, 5) ? 1 : 0;
        }

        return equal;
    }

    [Benchmark(OperationsPerInvoke = BenchmarkConfig.Repeats)]
    [BenchmarkCategory(LongAgainstOne)]
    public int LongOneByCompare()
    {
        var equal = 0;
        for (var repeat = 0; repeat < BenchmarkConfig.Repeats; repeat++)
        {
            equal += Fortran.Compare(_leftOne + ((repeat & (Places - 1)) * Stride), Stride, _one, 1) == 0 ? 1 : 0;
        }

        return equal;
    }

    [Benchmark(OperationsPerInvoke = BenchmarkConfig.Repeats)]
    [BenchmarkCategory(LongAgainstOne)]
    public int LongOneByTestInLine()
    {
        var equal = 0;
        for (var repeat = 0; repeat < BenchmarkConfig.Repeats; repeat++)
        {
            equal += IsEqualInLine(_leftOne + ((repeat & (Places - 1)) * Stride), Stride, _one, 1) ? 1 : 0;
        }

        return equal;
    }

    [Benchmark(Baseline = true, OperationsPerInvoke = BenchmarkConfig.Repeats)]
    [BenchmarkCategory(LongAgainstOne)]
    public int LongOneByIsEqual()
    {
        var equal = 0;
        for (var repeat = 0; repeat < BenchmarkConfig.Repeats; repeat++)
        {
            equal += Fortran.IsEqual(_leftOne + ((repeat & (Places - 1)) * Stride), Stride, _one, 1) ? 1 : 0;
        }

        return equal;
    }

    [Benchmark(OperationsPerInvoke = BenchmarkConfig.Repeats)]
    [BenchmarkCategory(LongAgainstTwelve)]
    public int LongTwelveByCompare()
    {
        var equal = 0;
        for (var repeat = 0; repeat < BenchmarkConfig.Repeats; repeat++)
        {
            equal += Fortran.Compare(_leftTwelve + ((repeat & (Places - 1)) * Stride), Stride, _twelve, 12) == 0 ? 1 : 0;
        }

        return equal;
    }

    [Benchmark(OperationsPerInvoke = BenchmarkConfig.Repeats)]
    [BenchmarkCategory(LongAgainstTwelve)]
    public int LongTwelveByTestInLine()
    {
        var equal = 0;
        for (var repeat = 0; repeat < BenchmarkConfig.Repeats; repeat++)
        {
            equal += IsEqualInLine(_leftTwelve + ((repeat & (Places - 1)) * Stride), Stride, _twelve, 12) ? 1 : 0;
        }

        return equal;
    }

    [Benchmark(Baseline = true, OperationsPerInvoke = BenchmarkConfig.Repeats)]
    [BenchmarkCategory(LongAgainstTwelve)]
    public int LongTwelveByIsEqual()
    {
        var equal = 0;
        for (var repeat = 0; repeat < BenchmarkConfig.Repeats; repeat++)
        {
            equal += Fortran.IsEqual(_leftTwelve + ((repeat & (Places - 1)) * Stride), Stride, _twelve, 12) ? 1 : 0;
        }

        return equal;
    }

    private static byte* Store(byte* place, string text)
    {
        Encoding.ASCII.GetBytes(text, new Span<byte>(place, text.Length));
        return place;
    }

    // The whole test where it is used: the constant's bytes, then blanks
    // to the value's end. A value shorter than the constant is left to
    // Compare.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsEqualInLine(byte* value, int length, byte* constant, int constantLength) =>
        length >= constantLength
            ? new ReadOnlySpan<byte>(value, constantLength).SequenceEqual(new ReadOnlySpan<byte>(constant, constantLength))
                && (length == constantLength || !new ReadOnlySpan<byte>(value + constantLength, length - constantLength).ContainsAnyExcept(Blank))
            : Fortran.Compare(value, length, constant, constantLength) == 0;
}
