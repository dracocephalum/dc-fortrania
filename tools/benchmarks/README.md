# benchmarks

Measures the Fortrania runtime, and the forms the translator could emit,
so that a change made for speed comes with its figures.

| | |
|---|---|
| Category | `tools/` |
| Kind | tool |
| Solution | `Dracocephalum.Fortrania.Benchmarks.slnx` |
| Projects | `src/Dracocephalum.Fortrania.Benchmarks`, `test/Dracocephalum.Fortrania.Benchmarks.Tests` |

## What it does

Each benchmark class sets what the runtime does today, as the baseline,
against another way of doing the same, written in `TextCandidates` or in
the benchmark itself. A candidate that proves faster moves into the runtime
or the emitter; one that does not is deleted with its benchmark. The tests
hold every candidate to the runtime's own answers, since a faster way that
gives another result is of no interest.

It is built on BenchmarkDotNet and refers to the runtime as a project, so
what is measured is the runtime as it is in the tree.

## Run it

    dotnet build Dracocephalum.Fortrania.Benchmarks.slnx --nologo -v:q
    dotnet test  Dracocephalum.Fortrania.Benchmarks.slnx

From this folder, always in `Release`:

    dotnet run -c Release --project src/Dracocephalum.Fortrania.Benchmarks
    dotnet run -c Release --project src/Dracocephalum.Fortrania.Benchmarks -- --filter '*ShortCopy*'
    dotnet run -c Release --project src/Dracocephalum.Fortrania.Benchmarks -- --filter '*' --full

With no argument it lists the benchmarks and runs none. `--filter` takes a
pattern over `Class.Method`, and everything else BenchmarkDotNet's own
command line takes is passed on. The table is the last thing printed, and
is written as markdown under the build's output, in
`BenchmarkDotNet.Artifacts/results/`, ready for a change description.

| Job | For | A benchmark takes |
|---|---|---|
| quick, the default | deciding while a change is made | about a second and a half; all of them in little over a minute |
| `--full` | figures for a change description, with what each call allocates | some twenty-five seconds |

It is no part of `dotnet test` and of no pipeline: a shared runner's
timings mean nothing.

## Reading the figures

**Compare by `Min` on a machine that is doing anything else.** `Mean` and
`Ratio` take every iteration, the disturbed ones too. A difference counts
when it is there on a second run.

**A machine that saves power gives figures that jump.** On the machine
this was written on, a virtual one on a balanced power plan with its
processor at 43% of its speed, the same benchmark gave 1.2 ns on one run
and 2.3 ns on the next, and iterations of one run differed as much. A plan
that keeps the processor at full speed is the remedy, and nothing here
changes a machine's settings.

## Configuration

None.

## Notes

What the figures depend on, each with its reason beside the code:

- **Benchmarks run inside the tool's own process**, `BenchmarkConfig`. The
  library's usual way builds a project for each, which is most of what a
  run then waits for.
- **Every method is compiled once, fully optimized**, in the project file.
  Left to the runtime, a method is compiled again in the background a while
  after it is first called, and in a run this short that falls in the
  middle of a measurement. The figures are therefore without the
  optimizations a long-running program earns from its own profile;
  `TODO.md` at the root has the entry.
- **A benchmark method does its operation 64 times**, `BenchmarkConfig.Repeats`,
  so that calling the method is no part of the figure worth taking away.
- **The values of one benchmark lie in one page**, `CharacterMemory`, a
  fixed distance apart: where two values lie in memory changes what a copy
  between them costs.
- **A reference built without optimization stops the run.** The solution
  holds the runtime's project for that reason: built from a solution that
  leaves it out, the runtime is built for debugging whatever configuration
  was asked for.
