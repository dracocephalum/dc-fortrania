using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Dracocephalum.Fortrania.Runtime;

namespace Dracocephalum.Fortrania.Benchmarks;

/// <summary>
/// A character value assigned to another when the translator knows both
/// lengths, which is the usual case. Today that is still a call of the
/// runtime with the lengths as arguments; the other two are what could be
/// written in place of the call, the copy that allows an overlap and the
/// one that does not.
/// </summary>
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public unsafe class ConstantCopyBenchmarks
{
    private const int Short = 8;
    private const int Name = 20;
    private const string ShortCategory = "8 bytes";
    private const string NameCategory = "20 bytes";

    private byte* _page;
    private byte* _source;
    private byte* _target;

    [GlobalSetup]
    public void Allocate()
    {
        _page = CharacterMemory.AllocatePage();
        _source = CharacterMemory.Store(_page, 0, "EARTH BARYCENTER");
        _target = CharacterMemory.Store(_page, 1, string.Empty);
    }

    [GlobalCleanup]
    public void Free()
    {
        CharacterMemory.Free(_page);
    }

    [Benchmark(Baseline = true, OperationsPerInvoke = BenchmarkConfig.Repeats)]
    [BenchmarkCategory(ShortCategory)]
    public void CallAssignOfEight()
    {
        for (var repeat = 0; repeat < BenchmarkConfig.Repeats; repeat++)
        {
            Fortran.Assign(_target, Short, _source, Short);
        }
    }

    [Benchmark(OperationsPerInvoke = BenchmarkConfig.Repeats)]
    [BenchmarkCategory(ShortCategory)]
    public void CopyEightAllowingOverlap()
    {
        for (var repeat = 0; repeat < BenchmarkConfig.Repeats; repeat++)
        {
            Buffer.MemoryCopy(_source, _target, Short, Short);
        }
    }

    [Benchmark(OperationsPerInvoke = BenchmarkConfig.Repeats)]
    [BenchmarkCategory(ShortCategory)]
    public void CopyEightDistinct()
    {
        for (var repeat = 0; repeat < BenchmarkConfig.Repeats; repeat++)
        {
            Unsafe.CopyBlockUnaligned(_target, _source, Short);
        }
    }

    [Benchmark(Baseline = true, OperationsPerInvoke = BenchmarkConfig.Repeats)]
    [BenchmarkCategory(NameCategory)]
    public void CallAssignOfTwenty()
    {
        for (var repeat = 0; repeat < BenchmarkConfig.Repeats; repeat++)
        {
            Fortran.Assign(_target, Name, _source, Name);
        }
    }

    [Benchmark(OperationsPerInvoke = BenchmarkConfig.Repeats)]
    [BenchmarkCategory(NameCategory)]
    public void CopyTwentyAllowingOverlap()
    {
        for (var repeat = 0; repeat < BenchmarkConfig.Repeats; repeat++)
        {
            Buffer.MemoryCopy(_source, _target, Name, Name);
        }
    }

    [Benchmark(OperationsPerInvoke = BenchmarkConfig.Repeats)]
    [BenchmarkCategory(NameCategory)]
    public void CopyTwentyDistinct()
    {
        for (var repeat = 0; repeat < BenchmarkConfig.Repeats; repeat++)
        {
            Unsafe.CopyBlockUnaligned(_target, _source, Name);
        }
    }
}
