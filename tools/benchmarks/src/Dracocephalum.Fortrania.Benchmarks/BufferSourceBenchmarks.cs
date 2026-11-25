using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Dracocephalum.Fortrania.Runtime;

namespace Dracocephalum.Fortrania.Benchmarks;

/// <summary>
/// What it costs a method to read, on entry, where its working buffers are
/// as three ways and not two: from the stack, from its place in the copy
/// of the arena open, or through a call that opens a copy where none is,
/// against the one test of the stack translated code made before. Measured
/// for each place on its own, for a method that is small and called often,
/// where a test on entry weighs most: one that calls nothing, and one that
/// calls something, as nearly every translated procedure does.
/// <c>DESIGN.md</c>, <em>Where a method keeps its working buffers is the
/// context's to say</em>, has what was found.
/// </summary>
/// <remarks>
/// The methods are written as the translator writes them: unsafe, never
/// compiled into their caller, their stack memory left as it is found, the
/// region read through an object as a library reads its own. Each writes
/// at both ends of its buffer and reads the two back, so that the buffer is
/// used and what is measured beside that is the entry.
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
    public int TestTwoWaysOnStack()
    {
        var sum = 0;
        for (var repeat = 0; repeat < BenchmarkConfig.Repeats; repeat++)
        {
            sum += TwoWays(_onStack, repeat);
        }

        return sum;
    }

    [Benchmark(OperationsPerInvoke = BenchmarkConfig.Repeats)]
    [BenchmarkCategory(StackCategory)]
    public int TestThreeWaysOnStack()
    {
        var sum = 0;
        for (var repeat = 0; repeat < BenchmarkConfig.Repeats; repeat++)
        {
            sum += ThreeWays(_onStack, repeat);
        }

        return sum;
    }

    [Benchmark(Baseline = true, OperationsPerInvoke = BenchmarkConfig.Repeats)]
    [BenchmarkCategory(ArenaCategory)]
    public int TestTwoWaysInArena()
    {
        var sum = 0;
        for (var repeat = 0; repeat < BenchmarkConfig.Repeats; repeat++)
        {
            sum += TwoWays(_inArena, repeat);
        }

        return sum;
    }

    [Benchmark(OperationsPerInvoke = BenchmarkConfig.Repeats)]
    [BenchmarkCategory(ArenaCategory)]
    public int TestThreeWaysInArena()
    {
        var sum = 0;
        for (var repeat = 0; repeat < BenchmarkConfig.Repeats; repeat++)
        {
            sum += ThreeWays(_inArena, repeat);
        }

        return sum;
    }

    [Benchmark(Baseline = true, OperationsPerInvoke = BenchmarkConfig.Repeats)]
    [BenchmarkCategory(CallingStackCategory)]
    public int TestTwoWaysCallingOnStack()
    {
        var sum = 0;
        for (var repeat = 0; repeat < BenchmarkConfig.Repeats; repeat++)
        {
            sum += TwoWaysCalling(_onStack, repeat);
        }

        return sum;
    }

    [Benchmark(OperationsPerInvoke = BenchmarkConfig.Repeats)]
    [BenchmarkCategory(CallingStackCategory)]
    public int TestThreeWaysCallingOnStack()
    {
        var sum = 0;
        for (var repeat = 0; repeat < BenchmarkConfig.Repeats; repeat++)
        {
            sum += ThreeWaysCalling(_onStack, repeat);
        }

        return sum;
    }

    [Benchmark(Baseline = true, OperationsPerInvoke = BenchmarkConfig.Repeats)]
    [BenchmarkCategory(CallingArenaCategory)]
    public int TestTwoWaysCallingInArena()
    {
        var sum = 0;
        for (var repeat = 0; repeat < BenchmarkConfig.Repeats; repeat++)
        {
            sum += TwoWaysCalling(_inArena, repeat);
        }

        return sum;
    }

    [Benchmark(OperationsPerInvoke = BenchmarkConfig.Repeats)]
    [BenchmarkCategory(CallingArenaCategory)]
    public int TestThreeWaysCallingInArena()
    {
        var sum = 0;
        for (var repeat = 0; repeat < BenchmarkConfig.Repeats; repeat++)
        {
            sum += ThreeWaysCalling(_inArena, repeat);
        }

        return sum;
    }

#pragma warning restore CA1822

    // As translated code took its buffers before: the stack where the
    // region said so, its place otherwise.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int TwoWays(Region region, int value)
    {
        byte* locals = region.Start + Place;
        if (region.IsOnStack)
        {
            byte* onStack = stackalloc byte[Size];
            locals = onStack;
        }

        return Use(locals, Size, value);
    }

    // As translated code takes them now: the stack, its place, or a call
    // that opens a copy of the arena first. The stack is tested first:
    // testing the arena first costs the stack more than it saves the arena.
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
            return Opened(region, value);
        }

        return Use(locals, Size, value);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int TwoWaysCalling(Region region, int value)
    {
        byte* locals = region.Start + Place;
        if (region.IsOnStack)
        {
            byte* onStack = stackalloc byte[Size];
            locals = onStack;
        }

        return Callee(locals, value);
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
            return Opened(region, value);
        }

        return Callee(locals, value);
    }

    // Never reached here: the regions measured have a copy open or keep the
    // stack. It is what the branch calls, so that the method is compiled as
    // translated code is.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int Opened(Region region, int value)
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
    // in the form of before and of now, and the memory they lie in.
    private sealed class Region
    {
        private const int Bytes = 64 * 1024;

        public Region(BufferSource source)
        {
            CurrentSource = source;
            IsOnStack = source == BufferSource.Stack;
            Start = (byte*)NativeMemory.AllocZeroed(Bytes);
        }

        public BufferSource CurrentSource { get; set; }

        public bool IsOnStack { get; }

        public byte* Start { get; private set; }

        public void Release()
        {
            NativeMemory.Free(Start);
            Start = null;
        }
    }
}
