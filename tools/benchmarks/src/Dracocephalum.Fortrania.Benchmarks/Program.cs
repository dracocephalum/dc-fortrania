using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using BenchmarkDotNet.Running;
using Dracocephalum.Fortrania.Benchmarks;
using Dracocephalum.Fortrania.Runtime;

// A run is asked for by name, so that a change measures what it touches and
// nothing else: with no argument the benchmarks are listed and none is run.
// Patterns run the paired measurement, which is the one to decide by;
// --absolute hands what follows it to BenchmarkDotNet, for times that mean
// something only on a machine whose speed is steady.
const string AbsoluteSwitch = "--absolute";

var assembly = typeof(BenchmarkConfig).Assembly;
if (args.Length == 0)
{
    Console.WriteLine("Give one or more patterns over Class.Method, * for anything, to measure in pairs;");
    Console.WriteLine($"or {AbsoluteSwitch} and BenchmarkDotNet's own arguments. The benchmarks:");
    foreach (var method in BenchmarkCatalog.ListMethods(assembly))
    {
        Console.WriteLine("  " + method);
    }

    return 0;
}

if (string.Equals(args[0], AbsoluteSwitch, StringComparison.OrdinalIgnoreCase))
{
    var summaries = BenchmarkSwitcher.FromAssembly(assembly).Run(args[1..], new BenchmarkConfig());
    return summaries.Any(summary => summary.HasCriticalValidationErrors || summary.Reports.Any(report => !report.Success)) ? 1 : 0;
}

// Code built without optimization is measured as slowly as it runs, and
// nothing in the figures says so.
foreach (var measured in new[] { assembly, typeof(Fortran).Assembly })
{
    if (measured.GetCustomAttribute<DebuggableAttribute>() is { IsJITOptimizerDisabled: true })
    {
        Console.Error.WriteLine($"{measured.GetName().Name} was built without optimization. Run with -c Release.");
        return 1;
    }
}

var cases = BenchmarkCatalog.SetUp(assembly, args);
if (cases.Count == 0)
{
    Console.Error.WriteLine("No benchmark has a candidate that matches. Run with no argument for the list.");
    return 1;
}

var runner = new PairedRunner(TimeProvider.System);
Console.WriteLine("| Benchmark | Case | Baseline | Candidate | Ratio | Middle half | Baseline ns | Candidate ns | |");
Console.WriteLine("|---|---|---|---|--:|---|--:|--:|---|");
foreach (var (instance, benchmark) in cases)
{
    foreach (var candidate in benchmark.Candidates)
    {
        var result = runner.Measure(benchmark.Baseline.Run, candidate.Run, benchmark.OperationsPerInvoke);
        var verdict = !result.IsDifferent ? "same" : result.Ratio < 1 ? "faster" : "slower";
        var where = string.Join(", ", new[] { benchmark.Group, benchmark.Parameters }.Where(part => part.Length > 0));
        Console.WriteLine(string.Create(
            CultureInfo.InvariantCulture,
            $"| {benchmark.ClassName} | {where} | {benchmark.Baseline.Name} | {candidate.Name} | {result.Ratio:0.00} | {result.LowerQuartile:0.00} to {result.UpperQuartile:0.00} | {result.BaselineNanoseconds:0.00} | {result.CandidateNanoseconds:0.00} | {verdict} |"));
    }
}

foreach (var instance in cases.Select(entry => entry.Instance).Distinct())
{
    BenchmarkCatalog.Release(instance);
}

return 0;
