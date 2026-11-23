using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;

namespace Dracocephalum.Fortrania.Benchmarks;

/// <summary>
/// What it costs a method to choose, each time it is entered, where its
/// working buffers lie: a method that takes them from the stack and asks
/// nothing, against one that tests a setting of its context first and
/// takes them from the stack or from a fixed place in a block of the
/// context's, as translated code now does. <c>DESIGN.md</c>, <em>Where a
/// method keeps its working buffers is the context's to say</em>, has
/// what was found. Measured for a method that is small and called often,
/// where a test on entry weighs most, with a small buffer and with one
/// near the most a method takes from the stack.
/// The last form asks nothing either and always takes them from the block,
/// which is what a translation made for a shallow stack alone would do.
/// </summary>
/// <remarks>
/// The methods are written as the translator writes them: unsafe, never
/// compiled into their caller, their stack memory left as it is found. Each
/// writes at both ends of its buffer and reads the two back, so that the
/// buffer is used and what is measured beside that is the entry.
/// </remarks>
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
[SkipLocalsInit]
public unsafe class StackEntryBenchmarks
{
    // A benchmark is an instance method, which is what the library that
    // runs it looks for, whether or not it reads anything of its instance.
#pragma warning disable CA1822
    private const int SmallSize = 64;
    private const int LargeSize = 4000;
    private const string SmallCategory = "64 bytes";
    private const string LargeCategory = "4000 bytes";

    // Where this method's buffers would lie in the context's block, as the
    // translator would have laid it out.
    private const int PlaceInFrames = 1024;

    private FrameChoice _onStack = null!;
    private FrameChoice _inFrames = null!;

    [GlobalSetup]
    public void Allocate()
    {
        _onStack = new FrameChoice(localsOnStack: true);
        _inFrames = new FrameChoice(localsOnStack: false);
    }

    [GlobalCleanup]
    public void Free()
    {
        _onStack.Release();
        _inFrames.Release();
    }

    [Benchmark(Baseline = true, OperationsPerInvoke = BenchmarkConfig.Repeats)]
    [BenchmarkCategory(SmallCategory)]
    public int TakeSmallFromStack()
    {
        var sum = 0;
        for (var repeat = 0; repeat < BenchmarkConfig.Repeats; repeat++)
        {
            sum += SmallOnStack(repeat);
        }

        return sum;
    }

    [Benchmark(OperationsPerInvoke = BenchmarkConfig.Repeats)]
    [BenchmarkCategory(SmallCategory)]
    public int ChooseSmallAndTakeFromStack()
    {
        var sum = 0;
        for (var repeat = 0; repeat < BenchmarkConfig.Repeats; repeat++)
        {
            sum += SmallChosen(_onStack, repeat);
        }

        return sum;
    }

    [Benchmark(OperationsPerInvoke = BenchmarkConfig.Repeats)]
    [BenchmarkCategory(SmallCategory)]
    public int ChooseSmallAndTakeFromFrames()
    {
        var sum = 0;
        for (var repeat = 0; repeat < BenchmarkConfig.Repeats; repeat++)
        {
            sum += SmallChosen(_inFrames, repeat);
        }

        return sum;
    }

    [Benchmark(OperationsPerInvoke = BenchmarkConfig.Repeats)]
    [BenchmarkCategory(SmallCategory)]
    public int TakeSmallFromFrames()
    {
        var sum = 0;
        for (var repeat = 0; repeat < BenchmarkConfig.Repeats; repeat++)
        {
            sum += SmallInFrames(_inFrames, repeat);
        }

        return sum;
    }

    [Benchmark(Baseline = true, OperationsPerInvoke = BenchmarkConfig.Repeats)]
    [BenchmarkCategory(LargeCategory)]
    public int TakeLargeFromStack()
    {
        var sum = 0;
        for (var repeat = 0; repeat < BenchmarkConfig.Repeats; repeat++)
        {
            sum += LargeOnStack(repeat);
        }

        return sum;
    }

    [Benchmark(OperationsPerInvoke = BenchmarkConfig.Repeats)]
    [BenchmarkCategory(LargeCategory)]
    public int ChooseLargeAndTakeFromStack()
    {
        var sum = 0;
        for (var repeat = 0; repeat < BenchmarkConfig.Repeats; repeat++)
        {
            sum += LargeChosen(_onStack, repeat);
        }

        return sum;
    }

    [Benchmark(OperationsPerInvoke = BenchmarkConfig.Repeats)]
    [BenchmarkCategory(LargeCategory)]
    public int ChooseLargeAndTakeFromFrames()
    {
        var sum = 0;
        for (var repeat = 0; repeat < BenchmarkConfig.Repeats; repeat++)
        {
            sum += LargeChosen(_inFrames, repeat);
        }

        return sum;
    }

    [Benchmark(OperationsPerInvoke = BenchmarkConfig.Repeats)]
    [BenchmarkCategory(LargeCategory)]
    public int TakeLargeFromFrames()
    {
        var sum = 0;
        for (var repeat = 0; repeat < BenchmarkConfig.Repeats; repeat++)
        {
            sum += LargeInFrames(_inFrames, repeat);
        }

        return sum;
    }

#pragma warning restore CA1822

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int SmallOnStack(int value)
    {
        byte* locals = stackalloc byte[SmallSize];
        return Use(locals, SmallSize, value);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int SmallChosen(FrameChoice context, int value)
    {
        byte* locals;
        if (context.LocalsOnStack)
        {
            byte* onStack = stackalloc byte[SmallSize];
            locals = onStack;
        }
        else
        {
            locals = context.Frames + PlaceInFrames;
        }

        return Use(locals, SmallSize, value);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int SmallInFrames(FrameChoice context, int value) => Use(context.Frames + PlaceInFrames, SmallSize, value);

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int LargeOnStack(int value)
    {
        byte* locals = stackalloc byte[LargeSize];
        return Use(locals, LargeSize, value);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int LargeChosen(FrameChoice context, int value)
    {
        byte* locals;
        if (context.LocalsOnStack)
        {
            byte* onStack = stackalloc byte[LargeSize];
            locals = onStack;
        }
        else
        {
            locals = context.Frames + PlaceInFrames;
        }

        return Use(locals, LargeSize, value);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int LargeInFrames(FrameChoice context, int value) => Use(context.Frames + PlaceInFrames, LargeSize, value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int Use(byte* locals, int size, int value)
    {
        *(int*)locals = value;
        *(int*)(locals + size - sizeof(int)) = value + 1;
        return *(int*)locals + *(int*)(locals + size - sizeof(int));
    }
}
