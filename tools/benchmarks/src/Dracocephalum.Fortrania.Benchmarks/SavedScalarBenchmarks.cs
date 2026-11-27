using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;

namespace Dracocephalum.Fortrania.Benchmarks;

/// <summary>
/// What it costs a method that its saved scalars lie in the unit's block
/// and are read and stored there each time, against the same with each
/// in a local while the method runs. Two shapes of the reference
/// sources': a bisection whose every variable is saved and which calls a
/// series on each turn, and a loop whose counter is saved and which moves
/// the elements of a short array.
/// </summary>
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
[SkipLocalsInit]
public unsafe class SavedScalarBenchmarks
{
    private const string Bisection = "a bisection that calls a series";
    private const string Shift = "a loop that shifts 12 elements";
    private const int Pairs = 20;
    private const int Targets = 64;

    private double* _pairs;
    private byte* _state;
    private double* _targets;

    [GlobalSetup]
    public void Allocate()
    {
        _pairs = (double*)NativeMemory.AlignedAlloc(Pairs * sizeof(double), 64);
        for (var i = 1; i <= Pairs; i++)
        {
            _pairs[i - 1] = 1.0 / ((double)i * (double)(i + 1));
        }

        _state = (byte*)NativeMemory.AlignedAlloc(1024, 64);
        new Span<byte>(_state, 1024).Clear();
        _targets = (double*)NativeMemory.AlignedAlloc(Targets * sizeof(double), 64);
        for (var index = 0; index < Targets; index++)
        {
            _targets[index] = 0.05 + (index * (1.0 / Targets));
        }
    }

    [GlobalCleanup]
    public void Free()
    {
        NativeMemory.AlignedFree(_pairs);
        NativeMemory.AlignedFree(_state);
        NativeMemory.AlignedFree(_targets);
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory(Bisection)]
    public double BisectionThroughState()
    {
        var sum = 0.0;
        for (var repeat = 0; repeat < 8; repeat++)
        {
            sum += SolveThroughState(_targets[repeat]);
        }

        return sum;
    }

    [Benchmark]
    [BenchmarkCategory(Bisection)]
    public double BisectionInLocals()
    {
        var sum = 0.0;
        for (var repeat = 0; repeat < 8; repeat++)
        {
            sum += SolveInLocals(_targets[repeat]);
        }

        return sum;
    }

    [Benchmark(Baseline = true, OperationsPerInvoke = BenchmarkConfig.Repeats)]
    [BenchmarkCategory(Shift)]
    public int ShiftThroughState()
    {
        var sum = 0;
        for (var repeat = 0; repeat < BenchmarkConfig.Repeats; repeat++)
        {
            sum += ShiftThroughState(12);
        }

        return sum;
    }

    [Benchmark(OperationsPerInvoke = BenchmarkConfig.Repeats)]
    [BenchmarkCategory(Shift)]
    public int ShiftInLocals()
    {
        var sum = 0;
        for (var repeat = 0; repeat < BenchmarkConfig.Repeats; repeat++)
        {
            sum += ShiftInLocals(12);
        }

        return sum;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private double SolveThroughState(double target)
    {
        byte* state = _state;
        double* bq = (double*)(state + 16);
        double* br0 = (double*)(state + 32);
        double* c0 = (double*)(state + 40);
        double* c1 = (double*)(state + 48);
        double* c2 = (double*)(state + 56);
        double* c3 = (double*)(state + 64);
        double* f = (double*)(state + 104);
        double* fx2 = (double*)(state + 112);
        double* kfun = (double*)(state + 152);
        double* lower = (double*)(state + 184);
        double* upper = (double*)(state + 296);
        double* x = (double*)(state + 344);
        double* b2rv = (double*)(state + 8);
        int* mostc = (int*)(state + 764);
        int* lcount = (int*)(state + 768);
        const int maxbit = 64;
        double dt_value = target;

        *f = 0.5;
        *br0 = 1.0;
        *b2rv = 0.1;
        *bq = 0.9;
        *lower = 0.001;
        *upper = 1.2;
        *x = Math.Min(*upper, Math.Max(*lower, (*lower + *upper) / 2.0));
        *fx2 = *f * *x * *x;
        Series(fx2, c0, c1, c2, c3);
        *lcount = 0;
        *mostc = 1000;
        while (*x > *lower && *x < *upper && *lcount < *mostc)
        {
            *kfun = *x * ((*br0 * *c1) + (*x * ((*b2rv * *c2) + (*x * *bq * *c3))));
            if (*kfun > dt_value)
            {
                *upper = *x;
            }
            else if (*kfun < dt_value)
            {
                *lower = *x;
            }
            else
            {
                *upper = *x;
                *lower = *x;
            }

            if (*mostc > maxbit)
            {
                if (*upper != 0.0 && *lower != 0.0)
                {
                    *mostc = maxbit;
                    *lcount = 0;
                }
            }

            *x = Math.Min(*upper, Math.Max(*lower, (*lower + *upper) / 2.0));
            *fx2 = *f * *x * *x;
            Series(fx2, c0, c1, c2, c3);
            *lcount = *lcount + 1;
        }

        return *x + *lcount;
    }

    // The same with every saved scalar in a local, stored back where the
    // method ends; one whose address a call is given is stored before the
    // call where the method has set it, and read again after it.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private double SolveInLocals(double target)
    {
        byte* state = _state;
        double* bq = (double*)(state + 16);
        double* br0 = (double*)(state + 32);
        double* c0 = (double*)(state + 40);
        double* c1 = (double*)(state + 48);
        double* c2 = (double*)(state + 56);
        double* c3 = (double*)(state + 64);
        double* f = (double*)(state + 104);
        double* fx2 = (double*)(state + 112);
        double* kfun = (double*)(state + 152);
        double* lower = (double*)(state + 184);
        double* upper = (double*)(state + 296);
        double* x = (double*)(state + 344);
        double* b2rv = (double*)(state + 8);
        int* mostc = (int*)(state + 764);
        int* lcount = (int*)(state + 768);
        const int maxbit = 64;
        double dt_value = target;
        double bq_value = *bq, br0_value = *br0, c0_value = *c0, c1_value = *c1, c2_value = *c2, c3_value = *c3;
        double f_value = *f, fx2_value = *fx2, kfun_value = *kfun, lower_value = *lower, upper_value = *upper, x_value = *x, b2rv_value = *b2rv;
        int mostc_value = *mostc, lcount_value = *lcount;

        f_value = 0.5;
        br0_value = 1.0;
        b2rv_value = 0.1;
        bq_value = 0.9;
        lower_value = 0.001;
        upper_value = 1.2;
        x_value = Math.Min(upper_value, Math.Max(lower_value, (lower_value + upper_value) / 2.0));
        fx2_value = f_value * x_value * x_value;
        *fx2 = fx2_value;
        Series(fx2, c0, c1, c2, c3);
        fx2_value = *fx2;
        c0_value = *c0;
        c1_value = *c1;
        c2_value = *c2;
        c3_value = *c3;
        lcount_value = 0;
        mostc_value = 1000;
        while (x_value > lower_value && x_value < upper_value && lcount_value < mostc_value)
        {
            kfun_value = x_value * ((br0_value * c1_value) + (x_value * ((b2rv_value * c2_value) + (x_value * bq_value * c3_value))));
            if (kfun_value > dt_value)
            {
                upper_value = x_value;
            }
            else if (kfun_value < dt_value)
            {
                lower_value = x_value;
            }
            else
            {
                upper_value = x_value;
                lower_value = x_value;
            }

            if (mostc_value > maxbit)
            {
                if (upper_value != 0.0 && lower_value != 0.0)
                {
                    mostc_value = maxbit;
                    lcount_value = 0;
                }
            }

            x_value = Math.Min(upper_value, Math.Max(lower_value, (lower_value + upper_value) / 2.0));
            fx2_value = f_value * x_value * x_value;
            *fx2 = fx2_value;
            Series(fx2, c0, c1, c2, c3);
            fx2_value = *fx2;
            c0_value = *c0;
            c1_value = *c1;
            c2_value = *c2;
            c3_value = *c3;
            lcount_value = lcount_value + 1;
        }

        *bq = bq_value;
        *br0 = br0_value;
        *c0 = c0_value;
        *c1 = c1_value;
        *c2 = c2_value;
        *c3 = c3_value;
        *f = f_value;
        *fx2 = fx2_value;
        *kfun = kfun_value;
        *lower = lower_value;
        *upper = upper_value;
        *x = x_value;
        *b2rv = b2rv_value;
        *mostc = mostc_value;
        *lcount = lcount_value;
        return x_value + lcount_value;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private int ShiftThroughState(int count)
    {
        byte* state = _state;
        int* newest = (int*)(state + 800);
        int* bumped = (int*)(state + 752);
        int* i = (int*)(state + 756);
        int* k = (int*)(state + 760);

        *k = count;
        *bumped = newest[*k - 1];
        for (*i = *k; *i >= 2; *i -= 1)
        {
            newest[*i - 1] = newest[*i - 1 - 1];
        }

        newest[0] = *bumped + 1;
        return newest[0];
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private int ShiftInLocals(int count)
    {
        byte* state = _state;
        int* newest = (int*)(state + 800);
        int* bumped = (int*)(state + 752);
        int* i = (int*)(state + 756);
        int* k = (int*)(state + 760);
        int bumped_value = *bumped, i_value = *i, k_value = *k;

        k_value = count;
        bumped_value = newest[k_value - 1];
        for (i_value = k_value; i_value >= 2; i_value -= 1)
        {
            newest[i_value - 1] = newest[i_value - 1 - 1];
        }

        newest[0] = bumped_value + 1;
        *bumped = bumped_value;
        *i = i_value;
        *k = k_value;
        return newest[0];
    }

    // STMP03's series as a translation writes it, its arguments in locals.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void Series(double* x, double* c0, double* c1, double* c2, double* c3)
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
