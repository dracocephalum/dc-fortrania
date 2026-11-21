using BenchmarkDotNet.Running;
using Dracocephalum.Fortrania.Benchmarks;

// A run is asked for by name, so that a change measures what it touches and
// nothing else: with no argument the benchmarks are listed and none is run.
// Everything but --full is BenchmarkDotNet's own, --filter above all.
const string FullSwitch = "--full";

var full = args.Contains(FullSwitch, StringComparer.OrdinalIgnoreCase);
var passedOn = args.Where(argument => !string.Equals(argument, FullSwitch, StringComparison.OrdinalIgnoreCase)).ToArray();
if (passedOn.Length == 0)
{
    passedOn = ["--list", "flat"];
}

var summaries = BenchmarkSwitcher
    .FromAssembly(typeof(BenchmarkConfig).Assembly)
    .Run(passedOn, new BenchmarkConfig(full));

return summaries.Any(summary => summary.HasCriticalValidationErrors || summary.Reports.Any(report => !report.Success)) ? 1 : 0;
