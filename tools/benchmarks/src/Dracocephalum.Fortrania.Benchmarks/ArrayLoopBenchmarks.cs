using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Dracocephalum.Fortrania.Runtime;

namespace Dracocephalum.Fortrania.Benchmarks;

/// <summary>
/// What a <c>DO</c> loop that only copies one array to another, or sets
/// every element of one to the same value, costs as a translation
/// writes it, the runtime's copy or fill for a long run and the loop for
/// a short one, against the loop alone, which is what a translation
/// wrote before, and against the framework's forms whatever the length. The lengths are the reference sources' own: a
/// vector, a state, a matrix of six by six, and a buffer.
/// </summary>
/// <remarks>
/// Each method is written as the translated routine is, taking its
/// length through a pointer and leaving the loop's variable where the
/// loop would.
/// </remarks>
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
[SkipLocalsInit]
public unsafe class ArrayLoopBenchmarks
{
    private const string CopyCategory = "copy";
    private const string ClearCategory = "clear";
    private const string FillCategory = "fill";
    private const int Longest = 1000;

    private double* _from;
    private double* _to;
    private int _count;

    [Params(3, 6, 16, 36, Longest)]
    public int Length { get; set; }

    [GlobalSetup]
    public void Allocate()
    {
        _from = (double*)NativeMemory.AllocZeroed(Longest, sizeof(double));
        _to = (double*)NativeMemory.AllocZeroed(Longest, sizeof(double));
        for (var index = 0; index < Longest; index++)
        {
            _from[index] = index + 0.5;
        }

        _count = Length;
    }

    [GlobalCleanup]
    public void Free()
    {
        NativeMemory.Free(_from);
        NativeMemory.Free(_to);
    }

    [Benchmark(OperationsPerInvoke = BenchmarkConfig.Repeats)]
    [BenchmarkCategory(CopyCategory)]
    public int CopyByLoop()
    {
        var last = 0;
        fixed (int* count = &_count)
        {
            for (var repeat = 0; repeat < BenchmarkConfig.Repeats; repeat++)
            {
                last += CopyByLoop(_from, count, _to);
            }
        }

        return last;
    }

    [Benchmark(OperationsPerInvoke = BenchmarkConfig.Repeats)]
    [BenchmarkCategory(CopyCategory)]
    public int CopyByMoving()
    {
        var last = 0;
        fixed (int* count = &_count)
        {
            for (var repeat = 0; repeat < BenchmarkConfig.Repeats; repeat++)
            {
                last += CopyByMoving(_from, count, _to);
            }
        }

        return last;
    }

    [Benchmark(Baseline = true, OperationsPerInvoke = BenchmarkConfig.Repeats)]
    [BenchmarkCategory(CopyCategory)]
    public int CopyAsTranslated()
    {
        var last = 0;
        fixed (int* count = &_count)
        {
            for (var repeat = 0; repeat < BenchmarkConfig.Repeats; repeat++)
            {
                last += CopyAsTranslated(_from, count, _to);
            }
        }

        return last;
    }

    [Benchmark(OperationsPerInvoke = BenchmarkConfig.Repeats)]
    [BenchmarkCategory(ClearCategory)]
    public int ClearByLoop()
    {
        var last = 0;
        fixed (int* count = &_count)
        {
            for (var repeat = 0; repeat < BenchmarkConfig.Repeats; repeat++)
            {
                last += ClearByLoop(count, _to);
            }
        }

        return last;
    }

    [Benchmark(OperationsPerInvoke = BenchmarkConfig.Repeats)]
    [BenchmarkCategory(ClearCategory)]
    public int ClearBySpan()
    {
        var last = 0;
        fixed (int* count = &_count)
        {
            for (var repeat = 0; repeat < BenchmarkConfig.Repeats; repeat++)
            {
                last += ClearBySpan(count, _to);
            }
        }

        return last;
    }

    [Benchmark(Baseline = true, OperationsPerInvoke = BenchmarkConfig.Repeats)]
    [BenchmarkCategory(ClearCategory)]
    public int ClearAsTranslated()
    {
        var last = 0;
        fixed (int* count = &_count)
        {
            for (var repeat = 0; repeat < BenchmarkConfig.Repeats; repeat++)
            {
                last += ClearAsTranslated(count, _to);
            }
        }

        return last;
    }

    [Benchmark(OperationsPerInvoke = BenchmarkConfig.Repeats)]
    [BenchmarkCategory(FillCategory)]
    public int FillByLoop()
    {
        var last = 0;
        fixed (int* count = &_count)
        {
            for (var repeat = 0; repeat < BenchmarkConfig.Repeats; repeat++)
            {
                last += FillByLoop(_from, count, _to);
            }
        }

        return last;
    }

    [Benchmark(OperationsPerInvoke = BenchmarkConfig.Repeats)]
    [BenchmarkCategory(FillCategory)]
    public int FillBySpan()
    {
        var last = 0;
        fixed (int* count = &_count)
        {
            for (var repeat = 0; repeat < BenchmarkConfig.Repeats; repeat++)
            {
                last += FillBySpan(_from, count, _to);
            }
        }

        return last;
    }

    [Benchmark(Baseline = true, OperationsPerInvoke = BenchmarkConfig.Repeats)]
    [BenchmarkCategory(FillCategory)]
    public int FillAsTranslated()
    {
        var last = 0;
        fixed (int* count = &_count)
        {
            for (var repeat = 0; repeat < BenchmarkConfig.Repeats; repeat++)
            {
                last += FillAsTranslated(_from, count, _to);
            }
        }

        return last;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int CopyByLoop(double* vin, int* ndim, double* vout)
    {
        int i;
        var last = *ndim;
        for (i = 1; i <= last; i += 1)
        {
            vout[i - 1] = vin[i - 1];
        }

        return i;
    }

    // The framework's move gives what the loop gives unless the target
    // starts inside the source and after its start, where the loop would
    // read what it has just written; there the loop is kept.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int CopyByMoving(double* vin, int* ndim, double* vout)
    {
        int i;
        var last = *ndim;
        if (last >= 1 && (vout <= vin || vout >= vin + last))
        {
            new ReadOnlySpan<double>(vin, last).CopyTo(new Span<double>(vout, last));
            i = last + 1;
        }
        else
        {
            for (i = 1; i <= last; i += 1)
            {
                vout[i - 1] = vin[i - 1];
            }
        }

        return i;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int ClearByLoop(int* ndim, double* array)
    {
        int i;
        var last = *ndim;
        for (i = 1; i <= last; i += 1)
        {
            array[i - 1] = 0.0;
        }

        return i;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int ClearBySpan(int* ndim, double* array)
    {
        var last = Math.Max(*ndim, 0);
        new Span<double>(array, last).Clear();
        return last + 1;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int FillByLoop(double* value, int* ndim, double* array)
    {
        int i;
        var last = *ndim;
        for (i = 1; i <= last; i += 1)
        {
            array[i - 1] = *value;
        }

        return i;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int FillBySpan(double* value, int* ndim, double* array)
    {
        var last = Math.Max(*ndim, 0);
        new Span<double>(array, last).Fill(*value);
        return last + 1;
    }

    // As a translation writes it: the runtime's form for a long run that
    // it can stand for, and the loop otherwise.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int CopyAsTranslated(double* vin, int* ndim, double* vout)
    {
        int i;
        var last = *ndim;
        if (Fortran.CanMoveElements(vin, vout, last))
        {
            i = Fortran.MoveElements(vin, vout, last);
        }
        else
        {
            for (i = 1; i <= last; i += 1)
            {
                vout[i - 1] = vin[i - 1];
            }
        }

        return i;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int ClearAsTranslated(int* ndim, double* array)
    {
        int i;
        var last = *ndim;
        if (last >= Fortran.LongRun)
        {
            i = Fortran.FillElements(array, last, 0.0);
        }
        else
        {
            for (i = 1; i <= last; i += 1)
            {
                array[i - 1] = 0.0;
            }
        }

        return i;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int FillAsTranslated(double* value, int* ndim, double* array)
    {
        int i;
        var last = *ndim;
        if (last >= Fortran.LongRun)
        {
            i = Fortran.FillElements(array, last, *value);
        }
        else
        {
            for (i = 1; i <= last; i += 1)
            {
                array[i - 1] = *value;
            }
        }

        return i;
    }
}
