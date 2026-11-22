using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Loggers;
using BenchmarkDotNet.Toolchains.InProcess.Emit;
using BenchmarkDotNet.Validators;

namespace Dracocephalum.Fortrania.Benchmarks;

/// <summary>
/// How BenchmarkDotNet runs a benchmark here, for a time in nanoseconds and
/// what a call allocates. Whether a candidate is faster is the paired
/// runner's to say; this is for the figure beside it, on a machine whose
/// speed is steady.
/// </summary>
/// <remarks>
/// It runs inside this process. The library's usual way builds a project
/// of its own for every benchmark, which is most of what a run then waits
/// for, and that project would be built under this repository's analyzers.
/// </remarks>
internal sealed class BenchmarkConfig : ManualConfig
{
    /// <summary>
    /// How many times a benchmark method does its operation in one call.
    /// What is measured here takes a few nanoseconds, which is what calling
    /// the method costs too. Done this many times a call, that cost is
    /// under a fiftieth of what is measured, and is left in: measuring it
    /// to take it away costs as many iterations again and gives a figure
    /// that is the difference of two noisy ones.
    /// </summary>
    public const int Repeats = 64;

    // Counted and not left to the library, which goes on to a hundred
    // iterations of half a second while the figures will not settle, and on
    // a disturbed machine they do not.
    private const int Warmups = 5;
    private const int Iterations = 20;

    public BenchmarkConfig()
    {
        AddJob(Job.Default
            .WithLaunchCount(1)
            .WithWarmupCount(Warmups)
            .WithIterationCount(Iterations)
            .WithToolchain(InProcessEmitToolchain.Instance)
            .WithEvaluateOverhead(false));

        // A reference built without optimization is measured as slowly as it
        // runs, and nothing in the figures says so. A solution that leaves a
        // referenced project out builds that project so, whatever it was asked.
        AddValidator(JitOptimizationsValidator.FailOnError);
        AddLogger(ConsoleLogger.Default);
        AddColumnProvider(DefaultColumnProviders.Instance);

        // The mean of a disturbed run says little; the least time an
        // iteration took is the figure to go by on a busy machine.
        AddColumn(StatisticColumn.Min);
        AddExporter(MarkdownExporter.GitHub);

        // A computation path allocates nothing per call, and this shows a
        // benchmark that does.
        AddDiagnoser(MemoryDiagnoser.Default);

        // Beside the build's output, which git ignores wherever it is run from.
        WithArtifactsPath(Path.Combine(AppContext.BaseDirectory, "BenchmarkDotNet.Artifacts"));
    }
}
