# Dracocephalum.Fortrania

Fortrania's translator as a library: it reads fixed-form FORTRAN 77 and
writes C# that keeps the original routines, their names and their
comments, and runs on
[Dracocephalum.Fortrania.Runtime](https://www.nuget.org/packages/Dracocephalum.Fortrania.Runtime).

Most uses want one of the two packages built on it and not this one:

| To | Use |
|---|---|
| translate from a prompt or a script | [Dracocephalum.Fortrania.Cli](https://www.nuget.org/packages/Dracocephalum.Fortrania.Cli), the command `fortrania` |
| translate as a project builds | [Dracocephalum.Fortrania.Build](https://www.nuget.org/packages/Dracocephalum.Fortrania.Build) |

Refer to this one to run a translation from a program of your own, or to
write a plugin, which takes part in a translation: before the sources are
read, before the files are written and after.

    var result = Translator.Run(project, new TranslationOptions());

`Translator.Run` takes one project or several, each described by its
settings, and gives back what it wrote and what it reported.

It translates the FORTRAN 77 that the sources it was written for use, and
refuses a program unit that uses what it does not translate yet, naming
the construct and the line.

Documentation, the design and its reasons, and where to report a fault:
<https://github.com/dracocephalum/dc-fortrania>.
