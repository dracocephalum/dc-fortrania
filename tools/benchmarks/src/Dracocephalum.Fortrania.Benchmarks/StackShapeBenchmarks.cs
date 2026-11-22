using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;

namespace Dracocephalum.Fortrania.Benchmarks;

/// <summary>
/// The two shapes a translated method has most and has largest, each as it
/// is written today and as it could be. The commonest: a scalar the method
/// passes on to a procedure it calls, which FORTRAN passes by its address;
/// the translator writes an ordinary local and takes its address, and the
/// question is what the scalar would cost anywhere else. The
/// largest: a routine with ten working arrays of 17,600 bytes together,
/// each taken from the stack by itself, which is the most any routine of
/// the reference sources takes. Unlike <see cref="StackEntryBenchmarks"/>,
/// the methods here do some work, so that the figures say what share of a
/// routine its entry is.
/// </summary>
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
[SkipLocalsInit]
public unsafe class StackShapeBenchmarks
{
    private const string ScalarCategory = "a scalar passed on";
    private const string ArraysCategory = "ten arrays";
    private const int PlaceInFrames = 1024;

    // The ten arrays, as the routine they are modelled on declares them.
    private const int TextSize = 2000;
    private const int RealCount = 100;
    private const int IntegerCount = 500;
    private const int ArraysSize = (2 * TextSize) + (2 * RealCount * sizeof(double)) + (6 * IntegerCount * sizeof(int));

    private FrameChoice _frames = null!;
    private FrameChoice _stack = null!;

    [GlobalSetup]
    public void Allocate()
    {
        _frames = new FrameChoice(localsOnStack: false);
        _stack = new FrameChoice(localsOnStack: true);
    }

    [GlobalCleanup]
    public void Free()
    {
        _frames.Release();
        _stack.Release();
    }

    // A benchmark is an instance method, which is what the library that
    // runs it looks for, whether or not it reads anything of its instance.
#pragma warning disable CA1822

    [Benchmark(OperationsPerInvoke = BenchmarkConfig.Repeats)]
    [BenchmarkCategory(ScalarCategory)]
    public int PassScalarFromStackalloc()
    {
        var sum = 0;
        for (var repeat = 0; repeat < BenchmarkConfig.Repeats; repeat++)
        {
            sum += ScalarAsStackalloc(repeat);
        }

        return sum;
    }

    [Benchmark(Baseline = true, OperationsPerInvoke = BenchmarkConfig.Repeats)]
    [BenchmarkCategory(ScalarCategory)]
    public int PassScalarAsLocal()
    {
        var sum = 0;
        for (var repeat = 0; repeat < BenchmarkConfig.Repeats; repeat++)
        {
            sum += ScalarAsLocal(repeat);
        }

        return sum;
    }

    [Benchmark(OperationsPerInvoke = BenchmarkConfig.Repeats)]
    [BenchmarkCategory(ScalarCategory)]
    public int PassScalarFromFrames()
    {
        var sum = 0;
        for (var repeat = 0; repeat < BenchmarkConfig.Repeats; repeat++)
        {
            sum += ScalarInFrames(_frames, repeat);
        }

        return sum;
    }

    [Benchmark(Baseline = true, OperationsPerInvoke = BenchmarkConfig.Repeats)]
    [BenchmarkCategory(ArraysCategory)]
    public double TakeTenArraysEachFromStack()
    {
        var sum = 0.0;
        for (var repeat = 0; repeat < BenchmarkConfig.Repeats; repeat++)
        {
            sum += ArraysEachOnStack(repeat);
        }

        return sum;
    }

    [Benchmark(OperationsPerInvoke = BenchmarkConfig.Repeats)]
    [BenchmarkCategory(ArraysCategory)]
    public double TakeTenArraysAsOneFromStack()
    {
        var sum = 0.0;
        for (var repeat = 0; repeat < BenchmarkConfig.Repeats; repeat++)
        {
            sum += ArraysAsOneOnStack(repeat);
        }

        return sum;
    }

    [Benchmark(OperationsPerInvoke = BenchmarkConfig.Repeats)]
    [BenchmarkCategory(ArraysCategory)]
    public double TakeTenArraysFromFrames()
    {
        var sum = 0.0;
        for (var repeat = 0; repeat < BenchmarkConfig.Repeats; repeat++)
        {
            sum += ArraysInFrames(_frames, repeat);
        }

        return sum;
    }

    [Benchmark(OperationsPerInvoke = BenchmarkConfig.Repeats)]
    [BenchmarkCategory(ArraysCategory)]
    public double ChooseAndTakeTenArraysFromStack()
    {
        var sum = 0.0;
        for (var repeat = 0; repeat < BenchmarkConfig.Repeats; repeat++)
        {
            sum += ArraysChosen(_stack, repeat);
        }

        return sum;
    }

    [Benchmark(OperationsPerInvoke = BenchmarkConfig.Repeats)]
    [BenchmarkCategory(ArraysCategory)]
    public double ChooseAndTakeTenArraysFromFrames()
    {
        var sum = 0.0;
        for (var repeat = 0; repeat < BenchmarkConfig.Repeats; repeat++)
        {
            sum += ArraysChosen(_frames, repeat);
        }

        return sum;
    }

#pragma warning restore CA1822

    // A place of one element on the stack, set to zero.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int ScalarAsStackalloc(int value)
    {
        int* count = stackalloc int[1] { 0 };
        Bump(count, value);
        return *count;
    }

    // As the translator writes it today: an ordinary local, and its address
    // where it is passed on.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int ScalarAsLocal(int value)
    {
        var count = 0;
        Bump(&count, value);
        return count;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int ScalarInFrames(FrameChoice context, int value)
    {
        var count = (int*)(context.Frames + PlaceInFrames);
        *count = 0;
        Bump(count, value);
        return *count;
    }

    // The procedure called, which writes through the address as FORTRAN's would.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Bump(int* count, int by) => *count += by;

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static double ArraysEachOnStack(int value)
    {
        byte* text = stackalloc byte[TextSize];
        byte* query = stackalloc byte[TextSize];
        double* reals = stackalloc double[RealCount];
        double* numbers = stackalloc double[RealCount];
        int* begins = stackalloc int[IntegerCount];
        int* ends = stackalloc int[IntegerCount];
        int* lexemeBegins = stackalloc int[IntegerCount];
        int* lexemeEnds = stackalloc int[IntegerCount];
        int* tokens = stackalloc int[IntegerCount];
        int* values = stackalloc int[IntegerCount];
        return Work(text, query, reals, numbers, begins, ends, lexemeBegins, lexemeEnds, tokens, values, value);
    }

    // One piece, and each array a place in it, as PENDING.md has it.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static double ArraysAsOneOnStack(int value)
    {
        byte* locals = stackalloc byte[ArraysSize];
        return WorkIn(locals, value);
    }

    // One method for both modes, which tests its context's setting on entry.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static double ArraysChosen(FrameChoice context, int value)
    {
        byte* locals;
        if (context.LocalsOnStack)
        {
            byte* onStack = stackalloc byte[ArraysSize];
            locals = onStack;
        }
        else
        {
            locals = context.Frames + PlaceInFrames;
        }

        return WorkIn(locals, value);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static double ArraysInFrames(FrameChoice context, int value) => WorkIn(context.Frames + PlaceInFrames, value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double WorkIn(byte* locals, int value)
    {
        var text = locals;
        var query = text + TextSize;
        var reals = (double*)(query + TextSize);
        var numbers = reals + RealCount;
        var begins = (int*)(numbers + RealCount);
        var ends = begins + IntegerCount;
        var lexemeBegins = ends + IntegerCount;
        var lexemeEnds = lexemeBegins + IntegerCount;
        var tokens = lexemeEnds + IntegerCount;
        var values = tokens + IntegerCount;
        return Work(text, query, reals, numbers, begins, ends, lexemeBegins, lexemeEnds, tokens, values, value);
    }

    // What a short call of such a routine does: a line of text blanked and
    // copied, a few tokens noted in each integer array, a few numbers
    // stored and added up. Some tens of nanoseconds, where the routine it
    // is modelled on takes far longer.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double Work(byte* text, byte* query, double* reals, double* numbers, int* begins, int* ends, int* lexemeBegins, int* lexemeEnds, int* tokens, int* values, int value)
    {
        const int LineLength = 80;
        const int TokenCount = 8;

        new Span<byte>(text, LineLength).Fill((byte)' ');
        new Span<byte>(text, LineLength).CopyTo(new Span<byte>(query, LineLength));

        var sum = 0.0;
        for (var token = 0; token < TokenCount; token++)
        {
            begins[token] = value + token;
            ends[token] = begins[token] + 1;
            lexemeBegins[token] = ends[token];
            lexemeEnds[token] = lexemeBegins[token] + 1;
            tokens[token] = lexemeEnds[token];
            values[token] = tokens[token] + query[token];
            reals[token] = values[token];
            numbers[token] = reals[token] * 0.5;
            sum += numbers[token];
        }

        return sum;
    }
}
