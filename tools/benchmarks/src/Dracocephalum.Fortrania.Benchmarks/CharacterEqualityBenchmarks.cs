using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Dracocephalum.Fortrania.Runtime;

namespace Dracocephalum.Fortrania.Benchmarks;

/// <summary>
/// What telling that two character values are equal costs once their
/// first bytes agree, which is where the runtime's <c>IsEqual</c> becomes
/// a call: by the runtime, which looks at up to 64 bytes itself, against
/// the same done by the framework's comparison and search whatever the
/// lengths, which is what it was. The shapes are the reference sources':
/// a routine's name against the one its caller checked in under, a name
/// in a variable of its own length, a short name in a short variable, and
/// a name in a line.
/// </summary>
/// <remarks>
/// A value is taken from one of several places in turn, and the lengths
/// are read from fields, as <see cref="CharacterAssignBenchmarks"/> has
/// them and for its reasons.
/// </remarks>
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
[SkipLocalsInit]
public unsafe class CharacterEqualityBenchmarks
{
    private const string ThirtyTwoAgainstSix = "32 bytes against 6";
    private const string ThirtyTwoAgainstThirtyTwo = "32 bytes against 32";
    private const string EightAgainstFive = "8 bytes against 5";
    private const string EightyAgainstTwelve = "80 bytes against 12";
    private const int Places = 64;
    private const int Stride = 128;
    private const int Shapes = 3;
    private const byte Blank = (byte)' ';
    private const int ShortRun = 8;

    private byte* _memory;
    private byte* _routine;
    private byte* _frame;
    private byte* _epoch;
    private int _five;
    private int _six;
    private int _eight;
    private int _twelve;
    private int _thirtyTwo;
    private int _eighty;

    /// <summary>Gets or sets a value indicating whether the two values are equal; otherwise they differ in their last character that is no blank.</summary>
    [Params(true, false)]
    public bool IsEqual { get; set; }

    [GlobalSetup]
    public void Allocate()
    {
        _memory = (byte*)NativeMemory.Alloc(Shapes * 2 * Places * Stride);
        new Span<byte>(_memory, Shapes * 2 * Places * Stride).Fill(Blank);
        _routine = Store(0, "SPKPVN");
        _frame = Store(1, "ECLIPJ2000_X");
        _epoch = Store(2, "J2000");
        (_five, _six, _eight, _twelve, _thirtyTwo, _eighty) = (5, 6, 8, 12, 32, 80);
    }

    [GlobalCleanup]
    public void Free() => NativeMemory.Free(_memory);

    [Benchmark(Baseline = true, OperationsPerInvoke = BenchmarkConfig.Repeats)]
    [BenchmarkCategory(ThirtyTwoAgainstSix)]
    public int SixByIsEqual() => ByIsEqual(_routine, _thirtyTwo, _six);

    [Benchmark(OperationsPerInvoke = BenchmarkConfig.Repeats)]
    [BenchmarkCategory(ThirtyTwoAgainstSix)]
    public int SixByTheFramework() => ByTheFramework(_routine, _thirtyTwo, _six);

    [Benchmark(Baseline = true, OperationsPerInvoke = BenchmarkConfig.Repeats)]
    [BenchmarkCategory(ThirtyTwoAgainstThirtyTwo)]
    public int ThirtyTwoByIsEqual() => ByIsEqual(_frame, _thirtyTwo, _thirtyTwo);

    [Benchmark(OperationsPerInvoke = BenchmarkConfig.Repeats)]
    [BenchmarkCategory(ThirtyTwoAgainstThirtyTwo)]
    public int ThirtyTwoByTheFramework() => ByTheFramework(_frame, _thirtyTwo, _thirtyTwo);

    [Benchmark(Baseline = true, OperationsPerInvoke = BenchmarkConfig.Repeats)]
    [BenchmarkCategory(EightAgainstFive)]
    public int FiveByIsEqual() => ByIsEqual(_epoch, _eight, _five);

    [Benchmark(OperationsPerInvoke = BenchmarkConfig.Repeats)]
    [BenchmarkCategory(EightAgainstFive)]
    public int FiveByTheFramework() => ByTheFramework(_epoch, _eight, _five);

    [Benchmark(Baseline = true, OperationsPerInvoke = BenchmarkConfig.Repeats)]
    [BenchmarkCategory(EightyAgainstTwelve)]
    public int TwelveByIsEqual() => ByIsEqual(_frame, _eighty, _twelve);

    [Benchmark(OperationsPerInvoke = BenchmarkConfig.Repeats)]
    [BenchmarkCategory(EightyAgainstTwelve)]
    public int TwelveByTheFramework() => ByTheFramework(_frame, _eighty, _twelve);

    /// <summary>Tells, for every pair of lengths and every place two values can differ in, whether they are found equal, for a test to compare.</summary>
    /// <param name="useRuntime">Whether the runtime tests; otherwise the framework's comparison and search do.</param>
    /// <returns>An answer for each pair of lengths and each place, in one order for both.</returns>
    public bool[] Test(bool useRuntime)
    {
        var left = stackalloc byte[Stride];
        var right = stackalloc byte[Stride];
        var result = new List<bool>();
        for (var leftLength = -1; leftLength <= 70; leftLength++)
        {
            for (var rightLength = -1; rightLength <= 70; rightLength++)
            {
                // Equal first, then with one byte of the left changed, at
                // each place in turn.
                for (var changed = -1; changed < leftLength; changed++)
                {
                    new Span<byte>(left, Stride).Fill(Blank);
                    new Span<byte>(right, Stride).Fill(Blank);
                    Encoding.ASCII.GetBytes("FORTRAN", new Span<byte>(left, 7));
                    Encoding.ASCII.GetBytes("FORTRAN", new Span<byte>(right, 7));
                    if (changed >= 0)
                    {
                        left[changed] = (byte)'#';
                    }

                    result.Add(useRuntime
                        ? Fortran.IsEqual(left, leftLength, right, rightLength)
                        : IsEqualByTheFramework(left, leftLength, right, rightLength));
                }
            }
        }

        return [.. result];
    }

    // What the runtime's IsEqual was: the first bytes where it is used,
    // and the rest by a call that is not written in line.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsEqualByTheFramework(byte* left, int leftLength, byte* right, int rightLength) =>
        (leftLength <= 0 || rightLength <= 0 || *left == *right) && IsEqualInFullByTheFramework(left, leftLength, right, rightLength);

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool IsEqualInFullByTheFramework(byte* longer, int length, byte* shorter, int shorterLength)
    {
        if (length < shorterLength)
        {
            var address = longer;
            longer = shorter;
            shorter = address;
            (length, shorterLength) = (shorterLength, length);
        }

        shorterLength = Math.Max(shorterLength, 0);
        if (!new ReadOnlySpan<byte>(longer, shorterLength).SequenceEqual(new ReadOnlySpan<byte>(shorter, shorterLength)))
        {
            return false;
        }

        var rest = length - shorterLength;
        if (rest >= ShortRun)
        {
            return !new ReadOnlySpan<byte>(longer + shorterLength, rest).ContainsAnyExcept(Blank);
        }

        for (var index = shorterLength; index < length; index++)
        {
            if (longer[index] != Blank)
            {
                return false;
            }
        }

        return true;
    }

    // Stores a name in every place of a shape's two runs of places, the
    // second run the first's length on from it, and where the two are to
    // differ, another last character in the first.
    private byte* Store(int shape, string name)
    {
        var first = _memory + (shape * 2 * Places * Stride);
        for (var place = 0; place < Places; place++)
        {
            Encoding.ASCII.GetBytes(name, new Span<byte>(first + (place * Stride), name.Length));
            Encoding.ASCII.GetBytes(name, new Span<byte>(first + ((Places + place) * Stride), name.Length));
            if (!IsEqual)
            {
                first[(place * Stride) + name.Length - 1] = (byte)'#';
            }
        }

        return first;
    }

    private static int ByIsEqual(byte* first, int leftLength, int rightLength)
    {
        var equal = 0;
        for (var repeat = 0; repeat < BenchmarkConfig.Repeats; repeat++)
        {
            var left = first + ((repeat & (Places - 1)) * Stride);
            equal += Fortran.IsEqual(left, leftLength, left + (Places * Stride), rightLength) ? 1 : 0;
        }

        return equal;
    }

    private static int ByTheFramework(byte* first, int leftLength, int rightLength)
    {
        var equal = 0;
        for (var repeat = 0; repeat < BenchmarkConfig.Repeats; repeat++)
        {
            var left = first + ((repeat & (Places - 1)) * Stride);
            equal += IsEqualByTheFramework(left, leftLength, left + (Places * Stride), rightLength) ? 1 : 0;
        }

        return equal;
    }
}
