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

Each benchmark class sets what is done today, as the baseline, against
other ways of doing the same. A candidate that proves faster moves into the
runtime or the emitter; one that does not is deleted with its benchmark,
and `DESIGN.md` at the root keeps the answer where it shaped the code.

A class is written once, with BenchmarkDotNet's attributes, and measured
in either of two ways:

- **In pairs**, by a runner of this tool's own, which says how a candidate
  stands to its baseline as a ratio. This is the one to decide by.
- **By BenchmarkDotNet**, for a time in nanoseconds and what a call
  allocates, on a machine whose speed is steady.

It refers to the runtime as a project, so what is measured is the runtime
as it is in the tree.

## Run it

    dotnet build Dracocephalum.Fortrania.Benchmarks.slnx --nologo -v:q
    dotnet test  Dracocephalum.Fortrania.Benchmarks.slnx

From this folder, always in `Release`:

    dotnet run -c Release --project src/Dracocephalum.Fortrania.Benchmarks
    dotnet run -c Release --project src/Dracocephalum.Fortrania.Benchmarks -- '*'
    dotnet run -c Release --project src/Dracocephalum.Fortrania.Benchmarks -- 'StackEntry*.Choose*'
    dotnet run -c Release --project src/Dracocephalum.Fortrania.Benchmarks -- --absolute --filter '*StackEntry*'

With no argument it lists the benchmarks and runs none. Patterns over
`Class.Method`, `*` for anything, measure in pairs the candidates they
match, each against its baseline; a comparison takes about half a second.
`--absolute` hands what follows it to BenchmarkDotNet, some twenty-five
seconds a benchmark.

It is no part of `dotnet test` and of no pipeline: a shared runner's
timings mean nothing.

## Reading the figures

The paired run prints a row for each candidate:

| Column | Says |
|---|---|
| Ratio | the candidate's time over the baseline's, the median of 300 pairs; below one is faster |
| Middle half | the range half of the pairs' ratios fell in, which is how far the pairs disagree |
| the last | `faster` or `slower` when that range lies wholly to one side of one, `same` otherwise |
| Baseline ns, Candidate ns | the least time an operation took; to be read as a size and not compared |

**Why pairs.** A machine's speed is not constant, a virtual one's least of
all. On the machine this was written on, a loop that touches no memory took
anything from 60 to 166 milliseconds from one run of it to the next, and
the same benchmark under BenchmarkDotNet gave 8 ns or 20 ns. A pair is two
slices half a millisecond long, one of each, which meet the same machine;
the ratios of three runs then agreed to the second decimal while the
times moved by a factor of three.

**A control tells a real difference from one of placement.** Where a
candidate lies in this assembly and the baseline in the runtime's, a copy
of the baseline placed and called as the candidate is shows what that
alone is worth. A first candidate here looked up to four times faster
before it had one, and was level or slower with it.

## Configuration

None.

## Notes

What the figures depend on, each with its reason beside the code:

- **Every method is compiled once, fully optimized**, in the project file.
  Left to the runtime, a method is compiled again in the background a while
  after it is first called, and in a run this short that falls in the
  middle of a measurement. The figures are therefore without the
  optimizations a long-running program earns from its own profile;
  `TODO.md` at the root has the entry.
- **A benchmark method does its operation 64 times**, `BenchmarkConfig.Repeats`,
  so that calling the method is no part of the figure worth taking away.
- **Code built without optimization stops the run**, in both ways of
  measuring. The solution holds the runtime's project for that reason:
  built from a solution that leaves it out, the runtime is built for
  debugging whatever configuration was asked for.
- **BenchmarkDotNet runs inside the tool's own process**, `BenchmarkConfig`.
  Its usual way builds a project for each benchmark, which is most of what
  a run then waits for.
- **Where two values lie in memory changes what a copy between them
  costs.** A benchmark that copies keeps its values a fixed distance apart
  in one allocation, or measures the allocator's mood.
