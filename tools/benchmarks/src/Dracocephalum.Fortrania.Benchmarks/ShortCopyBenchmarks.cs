using BenchmarkDotNet.Attributes;
using Dracocephalum.Fortrania.Runtime;

namespace Dracocephalum.Fortrania.Benchmarks;

/// <summary>
/// A character value assigned to another of its length, the length not
/// known until the call: the runtime's copy, which allows the two to
/// overlap, against one that does not. The control is the runtime's copy
/// again, placed and called as the candidate is; a candidate is ahead only
/// by what it is ahead of the control. Character values in FORTRAN are
/// mostly short, so the lengths are.
/// </summary>
public unsafe class ShortCopyBenchmarks
{
    private const int Longest = 100;

    private byte* _page;
    private byte* _source;
    private byte* _target;

    [Params(4, 8, 20, 32, Longest)]
    public int Length { get; set; }

    [GlobalSetup]
    public void Allocate()
    {
        _page = CharacterMemory.AllocatePage();
        _source = CharacterMemory.Store(_page, 0, "BARYCENTRIC DYNAMICAL TIME");
        _target = CharacterMemory.Store(_page, 1, string.Empty);
    }

    [GlobalCleanup]
    public void Free()
    {
        CharacterMemory.Free(_page);
    }

    [Benchmark(Baseline = true, OperationsPerInvoke = BenchmarkConfig.Repeats)]
    public void Assign()
    {
        for (var repeat = 0; repeat < BenchmarkConfig.Repeats; repeat++)
        {
            Fortran.Assign(_target, Length, _source, Length);
        }
    }

    [Benchmark(OperationsPerInvoke = BenchmarkConfig.Repeats)]
    public void AssignAsControl()
    {
        for (var repeat = 0; repeat < BenchmarkConfig.Repeats; repeat++)
        {
            TextCandidates.AssignAllowingOverlap(_target, Length, _source, Length);
        }
    }

    [Benchmark(OperationsPerInvoke = BenchmarkConfig.Repeats)]
    public void AssignDistinct()
    {
        for (var repeat = 0; repeat < BenchmarkConfig.Repeats; repeat++)
        {
            TextCandidates.AssignDistinct(_target, Length, _source, Length);
        }
    }
}
