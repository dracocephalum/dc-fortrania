using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using BenchmarkDotNet.Attributes;

namespace Dracocephalum.Fortrania.Benchmarks;

/// <summary>
/// What a procedure's scalar arguments cost where it reads and writes them
/// through their pointers in a loop, as a translation writes it today,
/// against the same procedure with each copied to a local on entry and back
/// before it returns. FORTRAN 77 lets no two arguments, nor an argument and
/// a common block, be the same storage where either is written, so the copy
/// keeps what the procedure does; the compiler, which cannot know that of a
/// pointer, then keeps the values in registers. The procedure is the series
/// branch of the reference sources' <c>STMP03</c>, the Stumpff functions,
/// as the translator emits it: its table in a block of native memory, as
/// saved state is.
/// </summary>
[SkipLocalsInit]
public unsafe class ScalarArgumentBenchmarks
{
    private const int Pairs = 20;
    private const int Arguments = 64;

    private double* _pairs;
    private double* _arguments;
    private double* _results;

    [GlobalSetup]
    public void Allocate()
    {
        _pairs = (double*)NativeMemory.AlignedAlloc(Pairs * sizeof(double), 64);
        for (var i = 1; i <= Pairs; i++)
        {
            _pairs[i - 1] = 1.0 / ((double)i * (double)(i + 1));
        }

        // Arguments in the series' range, -1 to 1, and four results for each
        // a fixed distance from them.
        _arguments = (double*)NativeMemory.AlignedAlloc(Arguments * sizeof(double), 64);
        _results = (double*)NativeMemory.AlignedAlloc(4 * sizeof(double), 64);
        for (var index = 0; index < Arguments; index++)
        {
            _arguments[index] = -0.99 + (index * (1.98 / Arguments));
        }
    }

    [GlobalCleanup]
    public void Free()
    {
        NativeMemory.AlignedFree(_pairs);
        NativeMemory.AlignedFree(_arguments);
        NativeMemory.AlignedFree(_results);
    }

    [Benchmark(Baseline = true, OperationsPerInvoke = BenchmarkConfig.Repeats)]
    public double ThroughPointers()
    {
        var sum = 0.0;
        var r = _results;
        for (var repeat = 0; repeat < BenchmarkConfig.Repeats; repeat++)
        {
            SeriesThroughPointers(_arguments + (repeat & (Arguments - 1)), r, r + 1, r + 2, r + 3);
            sum += r[0] + r[3];
        }

        return sum;
    }

    [Benchmark(OperationsPerInvoke = BenchmarkConfig.Repeats)]
    public double InLocals()
    {
        var sum = 0.0;
        var r = _results;
        for (var repeat = 0; repeat < BenchmarkConfig.Repeats; repeat++)
        {
            SeriesInLocals(_arguments + (repeat & (Arguments - 1)), r, r + 1, r + 2, r + 3);
            sum += r[0] + r[3];
        }

        return sum;
    }

    // As the translator writes STMP03's series today.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void SeriesThroughPointers(double* x, double* c0, double* c1, double* c2, double* c3)
    {
        var pairs = _pairs;
        int i;
        *c3 = 1.0;
        for (i = 20; i >= 4; i -= 2)
        {
            *c3 = 1.0 - (*x * pairs[i - 1] * *c3);
        }

        *c3 = pairs[1] * *c3;
        *c2 = 1.0;
        for (i = 19; i >= 3; i -= 2)
        {
            *c2 = 1.0 - (*x * pairs[i - 1] * *c2);
        }

        *c2 = pairs[0] * *c2;
        *c1 = 1.0 - (*x * *c3);
        *c0 = 1.0 - (*x * *c2);
    }

    // The same with each scalar argument in a local, written back before
    // the return.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void SeriesInLocals(double* x, double* c0, double* c1, double* c2, double* c3)
    {
        var pairs = _pairs;
        var xValue = *x;
        double c0Value, c1Value, c2Value, c3Value;
        int i;
        c3Value = 1.0;
        for (i = 20; i >= 4; i -= 2)
        {
            c3Value = 1.0 - (xValue * pairs[i - 1] * c3Value);
        }

        c3Value = pairs[1] * c3Value;
        c2Value = 1.0;
        for (i = 19; i >= 3; i -= 2)
        {
            c2Value = 1.0 - (xValue * pairs[i - 1] * c2Value);
        }

        c2Value = pairs[0] * c2Value;
        c1Value = 1.0 - (xValue * c3Value);
        c0Value = 1.0 - (xValue * c2Value);
        *c0 = c0Value;
        *c1 = c1Value;
        *c2 = c2Value;
        *c3 = c3Value;
    }
}
