using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Dracocephalum.Fortrania.Runtime;

namespace Dracocephalum.Fortrania.Benchmarks;

/// <summary>
/// <c>.EQ.</c> between character values. The runtime answers it with the
/// comparison that finds the order of the two; the candidate only says
/// whether they are equal. Three cases, since each stops at another place:
/// equal values of one length, which are read to the end; a value against
/// itself in a longer variable, where the rest is checked for blanks; and
/// values that differ in their first character, which is what a search
/// through a table of names mostly meets.
/// </summary>
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public unsafe class EqualityBenchmarks
{
    private const int Longest = 100;
    private const int Padding = 12;
    private const string EqualCategory = "equal";
    private const string PaddedCategory = "equal, one longer";
    private const string DifferentCategory = "different at once";

    // No blank at the end, and as long as the longest length measured.
    private static readonly string Text = "SOLAR SYSTEM BARYCENTER TO EARTH MOON BARYCENTER".PadRight(Longest, '*');

    private byte* _page;
    private byte* _left;
    private byte* _same;
    private byte* _padded;
    private byte* _other;

    [Params(4, 8, 20, 32, Longest)]
    public int Length { get; set; }

    [GlobalSetup]
    public void Allocate()
    {
        _page = CharacterMemory.AllocatePage();
        _left = CharacterMemory.Store(_page, 0, Text);
        _same = CharacterMemory.Store(_page, 1, Text);
        _padded = CharacterMemory.Store(_page, 2, Text[..Length]);
        _other = CharacterMemory.Store(_page, 3, "X" + Text[1..]);
    }

    [GlobalCleanup]
    public void Free()
    {
        CharacterMemory.Free(_page);
    }

    [Benchmark(Baseline = true, OperationsPerInvoke = BenchmarkConfig.Repeats)]
    [BenchmarkCategory(EqualCategory)]
    public int CompareEqualValues()
    {
        var equal = 0;
        for (var repeat = 0; repeat < BenchmarkConfig.Repeats; repeat++)
        {
            equal += Fortran.Compare(_left, Length, _same, Length) == 0 ? 1 : 0;
        }

        return equal;
    }

    [Benchmark(OperationsPerInvoke = BenchmarkConfig.Repeats)]
    [BenchmarkCategory(EqualCategory)]
    public int TestEqualValues()
    {
        var equal = 0;
        for (var repeat = 0; repeat < BenchmarkConfig.Repeats; repeat++)
        {
            equal += TextCandidates.AreEqual(_left, Length, _same, Length) ? 1 : 0;
        }

        return equal;
    }

    [Benchmark(Baseline = true, OperationsPerInvoke = BenchmarkConfig.Repeats)]
    [BenchmarkCategory(PaddedCategory)]
    public int ComparePaddedValues()
    {
        var equal = 0;
        for (var repeat = 0; repeat < BenchmarkConfig.Repeats; repeat++)
        {
            equal += Fortran.Compare(_left, Length, _padded, Length + Padding) == 0 ? 1 : 0;
        }

        return equal;
    }

    [Benchmark(OperationsPerInvoke = BenchmarkConfig.Repeats)]
    [BenchmarkCategory(PaddedCategory)]
    public int TestPaddedValues()
    {
        var equal = 0;
        for (var repeat = 0; repeat < BenchmarkConfig.Repeats; repeat++)
        {
            equal += TextCandidates.AreEqual(_left, Length, _padded, Length + Padding) ? 1 : 0;
        }

        return equal;
    }

    [Benchmark(Baseline = true, OperationsPerInvoke = BenchmarkConfig.Repeats)]
    [BenchmarkCategory(DifferentCategory)]
    public int CompareDifferentValues()
    {
        var equal = 0;
        for (var repeat = 0; repeat < BenchmarkConfig.Repeats; repeat++)
        {
            equal += Fortran.Compare(_left, Length, _other, Length) == 0 ? 1 : 0;
        }

        return equal;
    }

    [Benchmark(OperationsPerInvoke = BenchmarkConfig.Repeats)]
    [BenchmarkCategory(DifferentCategory)]
    public int TestDifferentValues()
    {
        var equal = 0;
        for (var repeat = 0; repeat < BenchmarkConfig.Repeats; repeat++)
        {
            equal += TextCandidates.AreEqual(_left, Length, _other, Length) ? 1 : 0;
        }

        return equal;
    }
}
