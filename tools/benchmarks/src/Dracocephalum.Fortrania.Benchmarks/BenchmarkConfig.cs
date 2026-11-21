using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Loggers;
using BenchmarkDotNet.Toolchains.InProcess.Emit;
using BenchmarkDotNet.Validators;
using Perfolizer.Horology;

namespace Dracocephalum.Fortrania.Benchmarks;

/// <summary>
/// How every benchmark here is run. The quick job is the default, and is
/// for deciding while a change is being made: a benchmark takes about a
/// second. The full job is for a figure that goes into a change description
/// when the quick one is too noisy to decide by.
/// </summary>
/// <remarks>
/// Both run inside this process. The library's usual way builds a project
/// of its own for every benchmark, which is most of what a run then waits
/// for, and that project would be built under this repository's analyzers.
/// </remarks>
internal sealed class BenchmarkConfig : ManualConfig
{
    /// <summary>
    /// How many times a benchmark method does its operation in one call.
    /// What is measured here takes a few nanoseconds, which is what calling
    /// the method costs too. The library can measure that cost and take it
    /// away, at the price of as many iterations again and of a figure that
    /// is the difference of two noisy ones; done this many times a call,
    /// the cost is under a fiftieth of what is measured, and is left in.
    /// </summary>
    public const int Repeats = 64;

    // Many short iterations and not a few long ones: on a machine doing
    // other work an iteration is either disturbed or not, and the minimum
    // over more of them is more likely to hold one that was not.
    private const int QuickWarmups = 2;
    private const int QuickIterations = 8;
    private static readonly TimeInterval QuickIteration = TimeInterval.FromMilliseconds(50);

    // Counted and not left to the library, which goes on to a hundred
    // iterations of half a second while the figures will not settle, and on
    // a disturbed machine they do not.
    private const int FullWarmups = 5;
    private const int FullIterations = 20;

    public BenchmarkConfig(bool full)
    {
        var job = full
            ? Job.Default.WithId("Full").WithLaunchCount(1).WithWarmupCount(FullWarmups).WithIterationCount(FullIterations)
            : Job.Default.WithId("Quick").WithLaunchCount(1).WithWarmupCount(QuickWarmups).WithIterationCount(QuickIterations).WithIterationTime(QuickIteration);

        AddJob(job.WithToolchain(InProcessEmitToolchain.Instance).WithEvaluateOverhead(false));

        // A reference built without optimization is measured as slowly as it
        // runs, and nothing in the figures says so. A solution that leaves a
        // referenced project out builds that project so, whatever it was asked.
        AddValidator(JitOptimizationsValidator.FailOnError);
        AddLogger(ConsoleLogger.Default);
        AddColumnProvider(DefaultColumnProviders.Instance);

        // The mean of a disturbed run says little; the least time an
        // iteration took is the figure to compare by on a busy machine.
        AddColumn(StatisticColumn.Min);
        AddExporter(MarkdownExporter.GitHub);

        // A computation path allocates nothing per call, and the full job
        // shows a benchmark that does. It costs a further stage each, which
        // the quick job does without.
        if (full)
        {
            AddDiagnoser(MemoryDiagnoser.Default);
        }

        // Beside the build's output, which git ignores wherever it is run from.
        WithArtifactsPath(Path.Combine(AppContext.BaseDirectory, "BenchmarkDotNet.Artifacts"));
    }
}
