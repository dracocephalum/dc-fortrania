using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Dracocephalum.Fortrania.Runtime;

namespace Dracocephalum.Fortrania.Benchmarks;

/// <summary>
/// What looking for a whole number in an array costs: by the loop a
/// translation wrote, one element at a time, against the runtime's
/// <c>FindElement</c>, which a translation calls for a long run with the
/// loop beside it for a short one. The lengths
/// are a handful, a few dozen, and the two hundred of the buffer the
/// reference sources look vertices up in; the value is the last element,
/// which is the most either way has to do short of not finding it.
/// </summary>
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
[SkipLocalsInit]
public unsafe class SearchLoopBenchmarks
{
    private const string Four = "4 elements";
    private const string Forty = "40 elements";
    private const string TwoHundred = "200 elements";
    private const int Length = 200;

    private int* _array;
    private int _four;
    private int _forty;
    private int _twoHundred;

    [GlobalSetup]
    public void Allocate()
    {
        _array = (int*)NativeMemory.AlignedAlloc(Length * sizeof(int), 64);
        for (var index = 0; index < Length; index++)
        {
            _array[index] = 1000 + index;
        }

        (_four, _forty, _twoHundred) = (4, 40, 200);
    }

    [GlobalCleanup]
    public void Free() => NativeMemory.AlignedFree(_array);

    [Benchmark(Baseline = true, OperationsPerInvoke = BenchmarkConfig.Repeats)]
    [BenchmarkCategory(Four)]
    public int FourByLoop() => ByLoop(_four);

    [Benchmark(OperationsPerInvoke = BenchmarkConfig.Repeats)]
    [BenchmarkCategory(Four)]
    public int FourByFindElement() => ByFindElement(_four);

    [Benchmark(Baseline = true, OperationsPerInvoke = BenchmarkConfig.Repeats)]
    [BenchmarkCategory(Forty)]
    public int FortyByLoop() => ByLoop(_forty);

    [Benchmark(OperationsPerInvoke = BenchmarkConfig.Repeats)]
    [BenchmarkCategory(Forty)]
    public int FortyByFindElement() => ByFindElement(_forty);

    [Benchmark(Baseline = true, OperationsPerInvoke = BenchmarkConfig.Repeats)]
    [BenchmarkCategory(TwoHundred)]
    public int TwoHundredByLoop() => ByLoop(_twoHundred);

    [Benchmark(OperationsPerInvoke = BenchmarkConfig.Repeats)]
    [BenchmarkCategory(TwoHundred)]
    public int TwoHundredByFindElement() => ByFindElement(_twoHundred);

    // The reference sources' ISRCHI as a translation wrote it.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int SearchByLoop(int* value, int* ndim, int* array)
    {
        int i;
        var sought = *value;
        var last = *ndim;
        for (i = 1; i <= last; i += 1)
        {
            if (array[i - 1] == sought)
            {
                return i;
            }
        }

        return 0;
    }

    // The same as a translation writes it now.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int SearchByFindElement(int* value, int* ndim, int* array)
    {
        var sought = *value;
        var last = *ndim;
        int i;
        if (last >= Fortran.LongRun)
        {
            i = Fortran.FindElement(array, last, sought);
            if (i <= last)
            {
                return i;
            }
        }
        else
        {
            for (i = 1; i <= last; i += 1)
            {
                if (array[i - 1] == sought)
                {
                    return i;
                }
            }
        }

        return 0;
    }

    private int ByLoop(int count)
    {
        var sum = 0;
        for (var repeat = 0; repeat < BenchmarkConfig.Repeats; repeat++)
        {
            var sought = 1000 + count - 1 - (repeat & 1);
            sum += SearchByLoop(&sought, &count, _array);
        }

        return sum;
    }

    private int ByFindElement(int count)
    {
        var sum = 0;
        for (var repeat = 0; repeat < BenchmarkConfig.Repeats; repeat++)
        {
            var sought = 1000 + count - 1 - (repeat & 1);
            sum += SearchByFindElement(&sought, &count, _array);
        }

        return sum;
    }
}
