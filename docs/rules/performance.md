# Performance

Applies when writing or reviewing any code that runs while a result is being
computed or a kernel is being read: the Fortrania runtime, everything the
translator emits, and the hand-written API over it. It does not apply to the
translator's own code, which runs once at build time and follows the toolkit's
rules unchanged.

This document applies on top of
`agentics/rules/coding/csharp/csharp-coding-rules.md`; where they differ, this
one holds.

## The rule

**Performance is the first design constraint.** After correctness against the
reference toolkit, which is a precondition and not a trade-off, throughput
decides. Where idiom, convenience or a general-purpose abstraction costs time
or allocation on a computation path, the faster form is the right one.

The benchmark to beat is the native C build of the toolkit doing the same
work. A managed port that is markedly slower has not met its purpose.

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
  created or a kernel is loaded.
- LINQ, iterators, closures and boxing.
- Interface or virtual dispatch on a call made per element or per evaluation.
- Exceptions as control flow. An error the reference toolkit signals through
  its own error state is carried the same way and turned into an exception
  only at the public boundary.
- `async`. Kernel input is small, random and synchronous; an awaited read
  costs more than it saves, and a pointer cannot be held across an `await`.

## Where the toolkit's rules give way

| Toolkit rule | Here |
|---|---|
| Never return `T[]` or `List<T>`; return a read-only collection interface | results are written into a caller-supplied `Span<T>` or returned as a struct |
| Records for data that crosses a boundary | `readonly struct`, or a mutable struct passed by `ref`, when it is numeric data on a computation path |
| Every `async` method takes a `CancellationToken` | still true, and there are no `async` methods on a computation path |
| Extract shared behaviour on the third use | not when the shared form adds a call or a branch the specialised forms do not have |

Everything else in the toolkit's rules stands, including naming, comments and
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
