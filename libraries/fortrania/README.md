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

Only the first step of the front end exists so far: classifying physical
source lines as comment, initial or continuation lines.

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
- Throughput of the translated code and of the runtime is the first design
  constraint: [`docs/rules/performance.md`](../../docs/rules/performance.md).
  The translator itself runs once at build time and is held to the ordinary
  rules.
