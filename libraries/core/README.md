# core

A FORTRAN 77 to C# translator, with the small managed runtime library the
translated code runs on.

| | |
|---|---|
| Category | `libraries/` |
| Kind | library |
| Solution | `Dracocephalum.Fortrania.slnx` |
| Projects | `src/Dracocephalum.Fortrania`, the translator; `src/Dracocephalum.Fortrania.Runtime`, what translated code calls; `src/Dracocephalum.Fortrania.Cli`, the command, `fortrania`; and a test project for each under `test/` |

## What it does

Fortrania reads fixed-form FORTRAN 77 source and emits C# that keeps the
original routine structure and comments. It was written to translate NASA's
SPICE Toolkit, in the tradition of `f2c`: the translated code
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
| Parser | tokens | a syntax tree per program unit | done for every construct the reference sources use; [`PENDING.md`](../../PENDING.md) lists what is left |
| Analysis | every program unit | symbols, types, and what each call does to its arguments | started: each unit has a symbol table that says what every name in it stands for, every expression is typed and checked against its place, and constant expressions are evaluated; each unit has a summary of how it is called and what it calls, every call is set against the procedure it reaches, and what each procedure reads and writes through its arguments is worked out across the program |
| Emitter | the analysed program | C# source | started: subroutines and functions over integer, real, double precision, logical and character values, with their arrays, substrings, concatenation, loops, branches and calls of one another, and what a unit keeps between calls: saved and initialized variables, with a method for each `ENTRY`, statement functions, procedures passed as arguments, variables that share storage, and of input and output the statements that connect and inquire, unformatted records read and written by number, text read and written by a format or list-directed, to a file or a character variable, `BACKSPACE`, main programs, and the procedures a program takes from its compiler's library. Every program unit of the reference sources' main library is translated, and the translation compiles whole; what the language has beyond those is listed in [`PENDING.md`](../../PENDING.md) |

The emitter refuses a program unit that uses what it does not translate yet,
naming the construct and the line, and never writes a partial translation.

Everything up to the emitter is independent of the output language, and the
emitter sits behind a seam of its own, so another back end is an addition
and not a rewrite. A whole translation, several projects made of several FORTRAN libraries
each, is described by one settings file and done by `Translator.Run`. The
command, `fortrania`, is a thin program over that and holds no logic of
its own.

## Run it

Run these from this folder; the repository root has no solution, so a build
there fails with `MSB1003` on purpose.

    dotnet build Dracocephalum.Fortrania.slnx --nologo -v:q
    dotnet test  Dracocephalum.Fortrania.slnx

The command is a .NET tool. From a build of this folder it runs as

    dotnet run --project src/Dracocephalum.Fortrania.Cli -- cs path/to/settings.json

and, installed from its package, as

    dotnet tool install --global Dracocephalum.Fortrania.Cli
    fortrania cs [settings] [--force]

With no settings file named it takes the one `fortrania.json` or
`*.fortrania.json` in the folder it is run in. `fortrania --help` says
the rest; [`DESIGN.md`](../../DESIGN.md), *The command is `fortrania`,
and what to produce its first word*, has why it is shaped so.

## Configuration

None.

## Notes

- Which code here is held to the performance rule and which is not:
  [`docs/rules/performance.md`](../../docs/rules/performance.md).
