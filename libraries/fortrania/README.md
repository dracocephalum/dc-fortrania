# fortrania

A FORTRAN 77 to C# translator, with the small managed runtime library the
translated code runs on.

| | |
|---|---|
| Category | `libraries/` |
| Kind | library |
| Solution | `Dracocephalum.Fortrania.slnx` |
| Projects | `src/Dracocephalum.Fortrania`, `test/Dracocephalum.Fortrania.Tests` |

## What it does

Fortrania reads fixed-form FORTRAN 77 source and emits C# that keeps the
original routine structure and comments. It exists to translate the SPICE
toolkit for this repository, in the tradition of `f2c`: the translated code
depends on a runtime library that supplies the FORTRAN intrinsics, string
semantics and record-based file input and output, written directly in managed
C# with no native code.

It is a general FORTRAN 77 translator and knows nothing about SPICE. Whatever
is particular to one body of source, which routines to replace by hand, what
to name the output, is configuration given to it, never code inside it.

The work is done by the library, in stages that each know only the one
before:

| Stage | Takes | Gives | State |
|---|---|---|---|
| Source form | physical lines | whole statements with their comments | done |
| Lexer | a statement | tokens | done |
| Parser | tokens | a syntax tree per program unit | started: statements are classified |
| Analysis | every program unit | symbols, types, and what each call does to its arguments | not started |
| Emitter | the analysed program | C# source | not started |

Everything up to the emitter is independent of the output language, and the
emitter sits behind a seam of its own, so another back end is an addition
and not a rewrite. The command-line front end, `f2csharp`, will be a thin
program over this library and hold no logic of its own.

## Run it

Run these from this folder; the repository root has no solution, so a build
there fails with `MSB1003` on purpose.

    dotnet build Dracocephalum.Fortrania.slnx --nologo -v:q
    dotnet test  Dracocephalum.Fortrania.slnx

There is no command-line front end yet. When one is added it will be invoked
as `f2csharp`.

## Configuration

None.

## Notes

- The translator and runtime are not specific to astronomy and are expected
  to move to a repository of their own once they are stable.
- Which code here is held to the performance rule and which is not:
  [`docs/rules/performance.md`](../../docs/rules/performance.md).
