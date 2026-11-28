# Dracocephalum.Fortrania.Runtime

The library that C# translated from FORTRAN 77 by Fortrania calls as it
runs. A translation refers to it; there is little reason to refer to it
otherwise.

It supplies what a FORTRAN program takes from its language and its
compiler's library:

- **Character values**: assignment, comparison and concatenation with
  the padding the language defines.
- **Input and output**: units, formatted, list-directed and unformatted
  records, direct and sequential access, internal files, over files on
  disk, files in memory or streams a host gives.
- **A context**, `FortranContext`, which holds everything one running
  program keeps: its libraries, what their routines keep between calls,
  its open files. Two contexts share nothing, so two programs, or two
  copies of one, run side by side in a process.

A translated library is reached through a context:

    using var context = new FortranContext();
    var core = context.Get<Example.Library.Core>();

It is written in managed C# with no native code, and depends on nothing
but the base class library. The translator and the runtime carry one
version number: a translation runs on the runtime of the version that
made it.

Documentation and where to report a fault:
<https://github.com/dracocephalum/dc-fortrania>.
