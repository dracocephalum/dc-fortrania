using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using static Dracocephalum.Fortrania.Runtime.Fortran;

namespace Dracocephalum.Fortrania.Benchmarks;

/// <summary>
/// What a real raised to a small whole power known when translating
/// costs: through the runtime's <c>Pow</c>, which is what a translation
/// writes, against the loop that was all of <c>Pow</c> once, and against
/// the multiplications written out, which give the same bits. The shape
/// is the reference sources' commonest use, the squares of a vector's
/// three components each divided and added up.
/// </summary>
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
[SkipLocalsInit]
public unsafe class PowerBenchmarks
{
    private const string SquareCategory = "three squares";
    private const string CubeCategory = "three cubes";
    private const string FourthCategory = "three fourth powers";
    private const int Values = 64;

    private readonly double[] _values = new double[Values + 2];

    [GlobalSetup]
    public void Fill()
    {
        for (var index = 0; index < _values.Length; index++)
        {
            _values[index] = 0.75 + (index * 0.0078125);
        }
    }

    [Benchmark(OperationsPerInvoke = BenchmarkConfig.Repeats)]
    [BenchmarkCategory(SquareCategory)]
    public double SquareByLoop()
    {
        var sum = 0.0;
        fixed (double* v = _values)
        {
            for (var repeat = 0; repeat < BenchmarkConfig.Repeats; repeat++)
            {
                var p = v + (repeat & (Values - 1));
                sum += (LoopPow(p[0], 2) / 1.5) + (LoopPow(p[1], 2) / 2.5) + (LoopPow(p[2], 2) / 3.5);
            }
        }

        return sum;
    }

    [Benchmark(Baseline = true, OperationsPerInvoke = BenchmarkConfig.Repeats)]
    [BenchmarkCategory(SquareCategory)]
    public double SquareByPow()
    {
        var sum = 0.0;
        fixed (double* v = _values)
        {
            for (var repeat = 0; repeat < BenchmarkConfig.Repeats; repeat++)
            {
                var p = v + (repeat & (Values - 1));
                sum += (Pow(p[0], 2) / 1.5) + (Pow(p[1], 2) / 2.5) + (Pow(p[2], 2) / 3.5);
            }
        }

        return sum;
    }

    [Benchmark(OperationsPerInvoke = BenchmarkConfig.Repeats)]
    [BenchmarkCategory(SquareCategory)]
    public double SquareByMultiplying()
    {
        var sum = 0.0;
        fixed (double* v = _values)
        {
            for (var repeat = 0; repeat < BenchmarkConfig.Repeats; repeat++)
            {
                var p = v + (repeat & (Values - 1));
                sum += (p[0] * p[0] / 1.5) + (p[1] * p[1] / 2.5) + (p[2] * p[2] / 3.5);
            }
        }

        return sum;
    }

    [Benchmark(OperationsPerInvoke = BenchmarkConfig.Repeats)]
    [BenchmarkCategory(CubeCategory)]
    public double CubeByLoop()
    {
        var sum = 0.0;
        fixed (double* v = _values)
        {
            for (var repeat = 0; repeat < BenchmarkConfig.Repeats; repeat++)
            {
                var p = v + (repeat & (Values - 1));
                sum += (LoopPow(p[0], 3) / 1.5) + (LoopPow(p[1], 3) / 2.5) + (LoopPow(p[2], 3) / 3.5);
            }
        }

        return sum;
    }

    [Benchmark(Baseline = true, OperationsPerInvoke = BenchmarkConfig.Repeats)]
    [BenchmarkCategory(CubeCategory)]
    public double CubeByPow()
    {
        var sum = 0.0;
        fixed (double* v = _values)
        {
            for (var repeat = 0; repeat < BenchmarkConfig.Repeats; repeat++)
            {
                var p = v + (repeat & (Values - 1));
                sum += (Pow(p[0], 3) / 1.5) + (Pow(p[1], 3) / 2.5) + (Pow(p[2], 3) / 3.5);
            }
        }

        return sum;
    }

    [Benchmark(OperationsPerInvoke = BenchmarkConfig.Repeats)]
    [BenchmarkCategory(CubeCategory)]
    public double CubeByMultiplying()
    {
        var sum = 0.0;
        fixed (double* v = _values)
        {
            for (var repeat = 0; repeat < BenchmarkConfig.Repeats; repeat++)
            {
                var p = v + (repeat & (Values - 1));
                sum += (p[0] * (p[0] * p[0]) / 1.5) + (p[1] * (p[1] * p[1]) / 2.5) + (p[2] * (p[2] * p[2]) / 3.5);
            }
        }

        return sum;
    }

    [Benchmark(OperationsPerInvoke = BenchmarkConfig.Repeats)]
    [BenchmarkCategory(FourthCategory)]
    public double FourthByLoop()
    {
        var sum = 0.0;
        fixed (double* v = _values)
        {
            for (var repeat = 0; repeat < BenchmarkConfig.Repeats; repeat++)
            {
                var p = v + (repeat & (Values - 1));
                sum += (LoopPow(p[0], 4) / 1.5) + (LoopPow(p[1], 4) / 2.5) + (LoopPow(p[2], 4) / 3.5);
            }
        }

        return sum;
    }

    [Benchmark(Baseline = true, OperationsPerInvoke = BenchmarkConfig.Repeats)]
    [BenchmarkCategory(FourthCategory)]
    public double FourthByPow()
    {
        var sum = 0.0;
        fixed (double* v = _values)
        {
            for (var repeat = 0; repeat < BenchmarkConfig.Repeats; repeat++)
            {
                var p = v + (repeat & (Values - 1));
                sum += (Pow(p[0], 4) / 1.5) + (Pow(p[1], 4) / 2.5) + (Pow(p[2], 4) / 3.5);
            }
        }

        return sum;
    }

    // The runtime's general form, as it stood alone before the runtime
    // came to choose the commonest exponents ahead of it.
    private static double LoopPow(double value, int exponent)
    {
        var remaining = (uint)Math.Abs((long)exponent);
        var result = (remaining & 1) != 0 ? value : 1.0;
        while ((remaining >>= 1) != 0)
        {
            value *= value;
            if ((remaining & 1) != 0)
            {
                result *= value;
            }
        }

        return exponent < 0 ? 1.0 / result : result;
    }
}
