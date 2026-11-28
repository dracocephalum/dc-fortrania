# Dracocephalum.Fortrania

Fortrania's translator as a library: it reads fixed-form FORTRAN 77 and
writes C# that keeps the original routines, their names and their
comments.

## What it is

Most uses want one of the two packages built on it and not this one:

| To | Use |
|---|---|
| translate from a prompt or a script | [Dracocephalum.Fortrania.Cli](https://www.nuget.org/packages/Dracocephalum.Fortrania.Cli), the command `fortrania` |
| translate as a project builds | [Dracocephalum.Fortrania.Build](https://www.nuget.org/packages/Dracocephalum.Fortrania.Build) |

Refer to this one to run a translation from a program of your own, or to
write a plugin, which takes part in a translation: before and after the
sources are read, the program is analysed and the files are written.

    var result = Translator.Run(project, new TranslationOptions());

`Translator.Run` takes one project or several, each described by its
settings, and gives back what it wrote and what it reported. What it
writes runs on
[Dracocephalum.Fortrania.Runtime](https://www.nuget.org/packages/Dracocephalum.Fortrania.Runtime).

## What it does not do yet

It translates the FORTRAN 77 that the sources it was written for use.
`GO TO` and statement labels, `COMMON` and `COMPLEX` are among what it
does not translate yet, and it refuses a program unit that uses any of
them, naming the construct and the line.

## What it writes

What this tool writes is made from what it was given, and carries the terms
of that and not this package's.

## Terms

Apache-2.0, the text of which is at <https://www.apache.org/licenses/LICENSE-2.0>.

## Where to ask

Questions, faults and requests go to <https://github.com/dracocephalum/dc-fortrania/issues>.
The design and its reasons are at <https://github.com/dracocephalum/dc-fortrania>.
