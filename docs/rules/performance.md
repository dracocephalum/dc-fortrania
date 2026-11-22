# Performance

Applies when writing or reviewing any code that runs while a translated
program computes or reads a file: the Fortrania runtime, and everything the
translator emits. It does not apply to the translator's own code, which runs
once when a program is translated and follows the C# coding rules unchanged.

This document applies on top of
`agentics/rules/coding/csharp/csharp-coding-rules.md`; where they differ, this
one holds.

## The rule

**Performance is the first design constraint.** After correctness against the
reference compiler's build of the same program, which is a precondition and
not a trade-off, throughput decides. Where idiom, convenience or a
general-purpose abstraction costs time or allocation on a computation path,
the faster form is the right one.

The measure is the same FORTRAN compiled natively. A translation that is
markedly slower than that has not met its purpose.

## What that permits

These are expected here, and are not findings in a review:

- `unsafe` code, raw pointers and `ref` arithmetic, where they remove a bounds
  check or a copy the JIT does not remove on its own.
- Pinned or native memory for state that lives as long as the library
  instance, so that a pointer to it stays valid without a `fixed` block at
  every use.
- Hardware intrinsics and `Vector128`, `Vector256` and `Vector512`, always
  with a path for hardware that lacks them; see `portability.md`.
- `Span<T>`, `ReadOnlySpan<T>`, `ref struct` and `stackalloc` in signatures
  and bodies, in place of arrays and collection interfaces.
- Mutable structs, fixed-size buffers and explicit layouts where the data is
  numeric and its shape is fixed.

## What it rules out on a computation path

- Heap allocation per call. State is allocated once, when the instance is
  created or a file is opened.
- LINQ, iterators, closures and boxing.
- Interface or virtual dispatch on a call made per element or per evaluation.
- Exceptions as control flow. An error a FORTRAN program signals through
  state of its own is carried the same way, and is turned into an exception
  only by whoever wraps the translated code for .NET callers.
- `async`. The input of a translated program is small, random and
  synchronous; an awaited read costs more than it saves, and a pointer cannot
  be held across an `await`.

## Where the C# coding rules give way

| C# coding rule | Here |
|---|---|
| Never return `T[]` or `List<T>`; return a read-only collection interface | results are written into a caller-supplied `Span<T>` or returned as a struct |
| Records for data that crosses a boundary | `readonly struct`, or a mutable struct passed by `ref`, when it is numeric data on a computation path |
| Every `async` method takes a `CancellationToken` | still true, and there are no `async` methods on a computation path |
| Extract shared behaviour on the third use | not when the shared form adds a call or a branch the specialised forms do not have |

Everything else in the C# coding rules stands, including naming, comments and
one type per file.

## Claims are measured

A change made for speed comes with a benchmark that shows it, written in
`tools/benchmarks` with BenchmarkDotNet's attributes, which measures the
runtime as it is in the tree. It is decided by that tool's paired run, the
candidate against what is there today, and the ratios go in the change
description; its `README.md` says how to run it and how to read what it
prints. "Should be faster" is not a reason to add `unsafe` code or an
intrinsic path; a measurement is. The same holds in reverse: a change that
makes a computation path slower says so and says why it is worth it.
