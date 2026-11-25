using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Dracocephalum.Fortrania.Runtime;

namespace Dracocephalum.Fortrania.Benchmarks;

/// <summary>
/// What a call of a method that has working buffers costs as translated
/// code is written: a method that tests three ways on entry, the stack, its
/// place in the open copy of the arena, or a call that opens a copy, against
/// one written as an entry and a core. The core tests the stack alone and is
/// what a call from inside a project reaches; the entry, compiled into its
/// caller, tests whether a copy is open and is what a call from outside
/// reaches. Measured for each place, for a method that is small and called
/// often, where a test on entry weighs most: one that calls nothing, and one
/// that calls something, as nearly every translated procedure does.
/// <c>DESIGN.md</c>, <em>Where a method keeps its working buffers is the
/// context's to say</em>, has what was found.
/// </summary>
/// <remarks>
/// The methods are written as the translator writes them: unsafe, never
/// compiled into their caller but for the entry, their stack memory left as
/// it is found, the region read through an object as a library reads its
/// own. Each writes at both ends of its buffer and reads the two back, so
/// that the buffer is used and what is measured beside that is the entry.
/// </remarks>
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
[SkipLocalsInit]
public unsafe class BufferSourceBenchmarks
{
    // A benchmark is an instance method, which is what the library that
    // runs it looks for, whether or not it reads anything of its instance.
#pragma warning disable CA1822
    private const int Size = 64;
    private const string StackCategory = "on the stack";
    private const string ArenaCategory = "in the arena";
    private const string CallingStackCategory = "calling, on the stack";
    private const string CallingArenaCategory = "calling, in the arena";

    // Where this method's buffers lie in the region, as the translator
    // would have laid it out.
    private const int Place = 1024;

    private Region _onStack = null!;
    private Region _inArena = null!;

    [GlobalSetup]
    public void Allocate()
    {
        _onStack = new Region(BufferSource.Stack);
        _inArena = new Region(BufferSource.Arena);
    }

    [GlobalCleanup]
    public void Free()
    {
        _onStack.Release();
        _inArena.Release();
    }

    [Benchmark(Baseline = true, OperationsPerInvoke = BenchmarkConfig.Repeats)]
    [BenchmarkCategory(StackCategory)]
    public int ThreeWaysOnStack()
    {
        var sum = 0;
        for (var repeat = 0; repeat < BenchmarkConfig.Repeats; repeat++)
        {
            sum += ThreeWays(_onStack, repeat);
        }

        return sum;
    }

    [Benchmark(OperationsPerInvoke = BenchmarkConfig.Repeats)]
    [BenchmarkCategory(StackCategory)]
    public int CoreOnStack()
    {
        var sum = 0;
        for (var repeat = 0; repeat < BenchmarkConfig.Repeats; repeat++)
        {
            sum += Core(_onStack, repeat);
        }

        return sum;
    }

    [Benchmark(OperationsPerInvoke = BenchmarkConfig.Repeats)]
    [BenchmarkCategory(StackCategory)]
    public int EntryOnStack()
    {
        var sum = 0;
        for (var repeat = 0; repeat < BenchmarkConfig.Repeats; repeat++)
        {
            sum += Entry(_onStack, repeat);
        }

        return sum;
    }

    [Benchmark(Baseline = true, OperationsPerInvoke = BenchmarkConfig.Repeats)]
    [BenchmarkCategory(ArenaCategory)]
    public int ThreeWaysInArena()
    {
        var sum = 0;
        for (var repeat = 0; repeat < BenchmarkConfig.Repeats; repeat++)
        {
            sum += ThreeWays(_inArena, repeat);
        }

        return sum;
    }

    [Benchmark(OperationsPerInvoke = BenchmarkConfig.Repeats)]
    [BenchmarkCategory(ArenaCategory)]
    public int CoreInArena()
    {
        var sum = 0;
        for (var repeat = 0; repeat < BenchmarkConfig.Repeats; repeat++)
        {
            sum += Core(_inArena, repeat);
        }

        return sum;
    }

    [Benchmark(OperationsPerInvoke = BenchmarkConfig.Repeats)]
    [BenchmarkCategory(ArenaCategory)]
    public int EntryInArena()
    {
        var sum = 0;
        for (var repeat = 0; repeat < BenchmarkConfig.Repeats; repeat++)
        {
            sum += Entry(_inArena, repeat);
        }

        return sum;
    }

    [Benchmark(Baseline = true, OperationsPerInvoke = BenchmarkConfig.Repeats)]
    [BenchmarkCategory(CallingStackCategory)]
    public int ThreeWaysCallingOnStack()
    {
        var sum = 0;
        for (var repeat = 0; repeat < BenchmarkConfig.Repeats; repeat++)
        {
            sum += ThreeWaysCalling(_onStack, repeat);
        }

        return sum;
    }

    [Benchmark(OperationsPerInvoke = BenchmarkConfig.Repeats)]
    [BenchmarkCategory(CallingStackCategory)]
    public int CoreCallingOnStack()
    {
        var sum = 0;
        for (var repeat = 0; repeat < BenchmarkConfig.Repeats; repeat++)
        {
            sum += CoreCalling(_onStack, repeat);
        }

        return sum;
    }

    [Benchmark(OperationsPerInvoke = BenchmarkConfig.Repeats)]
    [BenchmarkCategory(CallingStackCategory)]
    public int EntryCallingOnStack()
    {
        var sum = 0;
        for (var repeat = 0; repeat < BenchmarkConfig.Repeats; repeat++)
        {
            sum += EntryCalling(_onStack, repeat);
        }

        return sum;
    }

    [Benchmark(Baseline = true, OperationsPerInvoke = BenchmarkConfig.Repeats)]
    [BenchmarkCategory(CallingArenaCategory)]
    public int ThreeWaysCallingInArena()
    {
        var sum = 0;
        for (var repeat = 0; repeat < BenchmarkConfig.Repeats; repeat++)
        {
            sum += ThreeWaysCalling(_inArena, repeat);
        }

        return sum;
    }

    [Benchmark(OperationsPerInvoke = BenchmarkConfig.Repeats)]
    [BenchmarkCategory(CallingArenaCategory)]
    public int CoreCallingInArena()
    {
        var sum = 0;
        for (var repeat = 0; repeat < BenchmarkConfig.Repeats; repeat++)
        {
            sum += CoreCalling(_inArena, repeat);
        }

        return sum;
    }

    [Benchmark(OperationsPerInvoke = BenchmarkConfig.Repeats)]
    [BenchmarkCategory(CallingArenaCategory)]
    public int EntryCallingInArena()
    {
        var sum = 0;
        for (var repeat = 0; repeat < BenchmarkConfig.Repeats; repeat++)
        {
            sum += EntryCalling(_inArena, repeat);
        }

        return sum;
    }

#pragma warning restore CA1822

    // One method that tests three ways on entry.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int ThreeWays(Region region, int value)
    {
        byte* locals = region.Start + Place;
        if (region.CurrentSource == BufferSource.Stack)
        {
            byte* onStack = stackalloc byte[Size];
            locals = onStack;
        }
        else if (region.CurrentSource == BufferSource.None)
        {
            return OpenedThreeWays(region, value);
        }

        return Use(locals, Size, value);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int ThreeWaysCalling(Region region, int value)
    {
        byte* locals = region.Start + Place;
        if (region.CurrentSource == BufferSource.Stack)
        {
            byte* onStack = stackalloc byte[Size];
            locals = onStack;
        }
        else if (region.CurrentSource == BufferSource.None)
        {
            return OpenedThreeWays(region, value);
        }

        return Callee(locals, value);
    }

    // The entry, compiled into its caller, and the core it calls.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int Entry(Region region, int value) =>
        region.CurrentSource == BufferSource.None ? OpenedCore(region, value) : Core(region, value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int EntryCalling(Region region, int value) =>
        region.CurrentSource == BufferSource.None ? OpenedCore(region, value) : CoreCalling(region, value);

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int Core(Region region, int value)
    {
        byte* locals = region.Start + Place;
        if (region.CurrentSource == BufferSource.Stack)
        {
            byte* onStack = stackalloc byte[Size];
            locals = onStack;
        }

        return Use(locals, Size, value);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int CoreCalling(Region region, int value)
    {
        byte* locals = region.Start + Place;
        if (region.CurrentSource == BufferSource.Stack)
        {
            byte* onStack = stackalloc byte[Size];
            locals = onStack;
        }

        return Callee(locals, value);
    }

    // Never reached here: the regions measured have a copy open or keep the
    // stack. They are what the branches call, so that the methods are
    // compiled as translated code is.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int OpenedThreeWays(Region region, int value)
    {
        region.CurrentSource = BufferSource.Arena;
        try
        {
            return ThreeWays(region, value);
        }
        finally
        {
            region.CurrentSource = BufferSource.None;
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int OpenedCore(Region region, int value)
    {
        region.CurrentSource = BufferSource.Arena;
        try
        {
            return Core(region, value);
        }
        finally
        {
            region.CurrentSource = BufferSource.None;
        }
    }

    // A call such as nearly every translated procedure makes.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int Callee(byte* locals, int value) => Use(locals, Size, value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int Use(byte* locals, int size, int value)
    {
        *(int*)locals = value;
        *(int*)(locals + size - sizeof(int)) = value + 1;
        return *(int*)locals + *(int*)(locals + size - sizeof(int));
    }

    // Stands in for a project's region: what it says of where buffers are,
    // and the memory they lie in.
    private sealed class Region
    {
        private const int Bytes = 64 * 1024;

        public Region(BufferSource source)
        {
            CurrentSource = source;
            Start = (byte*)NativeMemory.AllocZeroed(Bytes);
        }

        public BufferSource CurrentSource { get; set; }

        public byte* Start { get; private set; }

        public void Release()
        {
            NativeMemory.Free(Start);
            Start = null;
        }
    }
}
