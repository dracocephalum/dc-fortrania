# Dracocephalum.Fortrania.Cli

`fortrania`, the command that translates FORTRAN 77 to C#. It is a .NET
tool.

## What it is

    dotnet tool install --global Dracocephalum.Fortrania.Cli
    fortrania translate csharp --name Example.Library --scaffold

Run in a folder that holds a settings file, `fortrania.json` or
`*.fortrania.json`, it translates what the file names and, with
`--scaffold`, writes a project file where there is none, so that
`dotnet build` builds the result. A settings file can be this short:

    {
      "output": ".",
      "sources": [
        { "name": "Core", "files": [ { "paths": [ "fortran/*.f" ] } ] }
      ]
    }

Each source becomes one class, `Example.Library.Core` here, with a method
for every subroutine and function, and each program unit a file of it
under `Generated/`. The translated code keeps the routines, their names
and their comments, and runs on
[Dracocephalum.Fortrania.Runtime](https://www.nuget.org/packages/Dracocephalum.Fortrania.Runtime).

`fortrania --help` lists the options. At a prompt a word may be shortened
to any start of it that is no other's: `fortrania t cs --name Example`.

To translate as a project builds, with no tool installed, use
[Dracocephalum.Fortrania.Build](https://www.nuget.org/packages/Dracocephalum.Fortrania.Build).

## What it does not do yet

It translates fixed-form FORTRAN 77 as far as the sources it was written
for use it: subroutines and functions with their entry points, kept and
initialized variables, character values, formatted and unformatted input
and output, procedures passed as arguments. `GO TO` and statement labels,
`COMMON` and `COMPLEX` are among what it does not translate yet. It
refuses a program unit that uses any of them, naming the construct and
the line, and never writes part of one.

## What it writes

What this tool writes is made from what it was given, and carries the terms
of that and not this package's.

## Terms

Apache-2.0, the text of which is at <https://www.apache.org/licenses/LICENSE-2.0>.

## Where to ask

Questions, faults and requests go to <https://github.com/dracocephalum/dc-fortrania/issues>.
The design and its reasons are at <https://github.com/dracocephalum/dc-fortrania>.
