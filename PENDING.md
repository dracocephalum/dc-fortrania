# Pending

Approaches that are decided and not built yet. Each section says how
something will be done and why, so that whoever builds it does not have to
decide again. The types and assemblies named here do not exist until they
are built; the names are the ones the code is to use.

When a section is built, its reasoning moves to [`DESIGN.md`](DESIGN.md) in
the same change, and the section is deleted here. What is merely owed or
undecided is in [`TODO.md`](TODO.md), not here.

## Text is UTF-8 bytes end to end

A FORTRAN `CHARACTER` value is a fixed-length run of single bytes, and the
translated code works on runs of bytes. Those bytes are defined to be UTF-8.
ASCII is already valid UTF-8 and no multi-byte character contains an ASCII
byte, so the toolkit's own parsing is unaffected and text it does not inspect
passes through intact.

- The translator emits every string literal as a `u8` literal, which is
  static data and costs no allocation.
- A .NET `string` exists only at the boundary: in the managed API's
  convenience overloads, and where the runtime opens a file and has to hand
  the operating system a path. Nothing on a computation path converts to or
  from UTF-16.
- The checks the toolkit makes on its own inputs are kept as they are: text
  stored in kernel files, time strings, and the ASCII-only matching of body
  and frame names. Unicode is for what the toolkit carries without
  interpreting, file paths above all.
- Lengths are in bytes. The boundary refuses a string that would be cut in
  the middle of a character rather than passing a broken one inward.

## Two API layers

    ISpiceCore    Dracocephalum.Astro.Spice            hand-written, managed    converts, validates, throws
        |
    ISpiceLib     Dracocephalum.Astro.Spice.Toolkit    generated, FORTRAN-shaped    pointers, no conversion

`ISpiceLib` is the translated library, SPICELIB, as it is: one member per public
routine, with the routine's own argument list, caller-supplied buffers for
results, and errors left in the toolkit's error state. It is generated with
the code it fronts and lives with it. Its arguments are trusted to be well
formed, so the toolkit's own checks are the only ones made.

The assembly is named for the toolkit and the interface for the library
within it, because the toolkit holds more than SPICELIB, its support library
and its utility programs, and those can join the same assembly later behind
interfaces of their own.

`ISpiceCore` is the layer a .NET program is meant to use, in the role the
reference toolkit's C wrappers play over its translated C. It takes strings,
spans and ordinary .NET types, pins what it is given, sizes and fills the
buffers, checks lengths once, checks the error state after each call and
throws. It is hand-written, so it is a component of its own under the
repository's licence. A caller of this layer never sees a pointer.

The interface exists for substitution in tests and for a second
implementation, not as the call path. The implementation is a sealed class,
and code that calls it per evaluation holds the class, or is generic over it,
so that the call is direct and can be inlined —
[`docs/rules/performance.md`](docs/rules/performance.md) rules out interface
dispatch on a computation path.

`ISpiceCore` is shaped for C# first, and for callers outside .NET second;
neither gives anything up for another language. Visual Basic cannot use a
span, so a member that takes or returns one cannot be called from it. Such
members get an array-taking counterpart for Visual Basic, added beside the
core as further overloads or extension methods that forward to it. The core
members themselves are never reshaped to suit, and where the two pull apart
the counterpart pays the cost, a copy for instance, not the core.

## The translated layer passes pointers

Every argument of a translated routine is a raw pointer: `double*` for a
number or an array of them, `int*` for integers and logicals, and `byte*`
with an explicit length in bytes for text, which is how FORTRAN itself passes
a `CHARACTER` argument. Translated code calls translated code the same way.

Pointers and not spans, because that is what the source language does:

- A FORTRAN call passes an address and nothing else. A routine may be handed
  `A(3)` and treat it as the start of an array, and many arrays are declared
  with no size at all. A span needs a length that the source does not have.
- An array access stays an array access: `STATE(I)` becomes `state[i - 1]`,
  so translated code can be read beside the routine it came from.
- The same array is often passed as both input and output, which a pointer
  permits without ceremony.
- A facade for callers outside .NET, C exports or COM, needs this shape
  anyway and becomes a one-line forward per routine.
- There is no bounds check and one register per argument.
- The reference sources pass arguments that do not agree with what the
  procedure declares, and a pointer honours each as the original does: a
  single variable or a constant where an array is expected, a whole array
  where one value is, a double precision array to a routine that moves
  integers, a logical or double precision variable where an integer is
  expected. Where the types differ the emitter casts the pointer. One
  function entry is called as a subroutine; its value is dropped.

A value that is not a variable, a constant or an expression, is copied to a
temporary whose address is passed. When its type is not the one the procedure
declares, as with a real constant given for a double precision argument in
one test program, the temporary is at least as large as the declared type,
so that the procedure never reads or writes past it; the original leaves
what lies there to chance.

Two obligations follow, and both are the design's to meet, not the caller's
to remember.

**Nothing a pointer reaches may move.** Storage that outlives a call, the
`SAVE` variables of every routine and any large local array, is native
memory owned by the library instance and freed with it. Small local arrays
are `stackalloc`. A managed array is never handed inward without being
pinned for the length of the call, which `ISpiceCore` does on the caller's
behalf.

**A character constant is a `u8` literal, and its address is taken as it
stands.** The compiler stores the bytes of a `u8` literal in the assembly
itself, and the span it yields points into the loaded image: memory that was
never on the managed heap, so nothing can move it, and no `fixed` block is
needed. `CALL CHKIN ( 'SPKEZ' )` therefore passes a pointer at no cost.
Measured on .NET 10.0.11, x64: the address was unchanged after five
compacting collections, the page belonged to the assembly's image and was
read-only, and the byte after the constant was zero. Three limits come with
it:

- It holds for as long as the assembly is loaded. Loaded into a collectible
  context and unloaded, the image and every such pointer go with it; the
  library is not to be used that way.
- It is not extended to an ordinary `string` literal, which lives on the
  heap and stays put only by an implementation detail.
- It is not extended to constant data wider than a byte. The image stores
  such data little-endian, so on a big-endian machine the runtime has to
  hand back a converted copy made elsewhere. Numeric constants and the
  values of `DATA` statements are copied into the instance's native memory
  when the instance is created, which a `DATA`-initialized variable needs in
  any case, being both writable and per instance.

Being read-only is useful in itself: translated code that writes through a
pointer to a constant, which FORTRAN forbids and faulty code does, stops at
once and does not go on with a changed constant.

**A checked build stands in for the bounds checks.** A wrong index through a
pointer corrupts memory silently, so a translator fault would surface far
from its cause. Most arrays in the source do have declared dimensions, and
in a checked build the translator emits a test of each index against them
that fails at the faulty statement. The regression tests run against the
checked build; the published package is the unchecked one.

## A library is one class, and a program unit one file of it

The code translated from one set of sources is a single partial class, and
each program unit adds its methods to it in a file of its own. An instance of
the class is one independent copy of the library: every routine is an
instance method, and everything the FORTRAN keeps between calls belongs to
the instance. Being partial, the class also takes hand-written files, which
is how a routine the translator is told to leave out is supplied.

A library that calls another, as the toolkit's support library calls its
main one, is a class of its own. Both are made over the one context of
their program, and the second finds the first there, as `DESIGN.md` has it.
What is not built is translating them apart: the summaries of the first
written to a file beside its code, and the translation of the second
reading them, where today both are translated in one run. *What is translated is
set out as projects and their sources* says how several are named.

A main program is a method like any other, which is built. Not built: when
an executable is asked for, a `Main` is emitted that makes the context,
gives it the console and the command line, lets it run commands, calls
that method and returns the exit code. A library has none.

`STOP` throws an exception of the runtime's, from any depth, and so does the
`EXIT` the runtime supplies. The emitted `Main` catches it and returns its
code; a program that hosts the library catches it like any other, and is not
ended by a routine it called. The reference sources do end the program from
library code: their default action on an error calls `EXIT`, and two files
of the main library have a `STOP`.

## Saved state is placed at translation and loaded at run time

Measured from the declarations of the main library: 327 of its 1,415 program
units keep state between calls, 100.9 MB in all. The 50 units that keep more
than 4 KB hold all but about 0.1 MB of it, the kernel pool alone 26 MB, and
only 0.7 MB belongs to variables a `DATA` statement initializes. A program
that converts times and nothing else should not pay for the rest, so state
is not all allocated with the instance.

Two decisions are kept apart, because the second cannot undo the first.
Of them the emitter so far has one placement, *Lazy*, and one loading, on
the first call; `DESIGN.md` describes it. The shared block, the choice
between placements and every choice at run time are still to be built.

**Placement, by the translator.** A unit's state is either part of one block
shared by all such units, which exists as long as the instance does and is
reached at a fixed offset with no test, or a block of its own behind a
pointer, which the unit's routines test on entry. An option of the
translator chooses:

- *Auto*, the default: shared below a size, 4 KB unless told otherwise, and
  separate from there up.
- *Eager*: all shared. No routine tests anything, and all the memory comes
  with the instance.
- *Lazy*: all separate.

The translator's project file can also name single units to place one way
or the other, whatever the mode.

**Loading, at run time.** A separate block is allocated on the first call
that needs it, or with the instance; the translated library carries its
default and the program that creates an instance may override it, for all
such blocks or for named ones. This reaches only what the translator placed
separately: a unit in the shared block cannot be deferred afterwards, which
is why placement is the translator's to decide and not the runtime's.

The values of `DATA` statements are written when the block they belong to
is created.

**Memory is zeroed unless asked otherwise.** FORTRAN leaves a saved variable
undefined until it is given a value, so a conforming program never reads
what the allocation left there, and memory that is not zeroed is faster to
get when blocks are reused, as they are when instances come and go. But the
reference build keeps saved variables in storage the system zeroes, so
nothing has ever shown that the sources do not lean on it. Unzeroed memory
is therefore an option and not the default, and it is to be offered only
after the regression tests give the same results with the memory filled
with zeros and with each of two other patterns; filling with a pattern stays
available as a third choice for that purpose. Local variables differ: the
reference build already leaves them unset on every call, so local arrays are
not zeroed.

Where the memory comes from is a third choice of the creating program:

- Native memory, freed when the instance is disposed.
- Pinned arrays of the garbage collector, which need no disposal and are
  zeroed and committed at once.
- A pool of pinned blocks, from which an instance takes its blocks and to
  which it gives them back when disposed. Creating an instance then asks
  the system for nothing, which suits instances that come and go, and with
  unzeroed memory nothing is cleared either. The blocks are allocated
  pinned and kept by the pool; an array of the framework's ordinary pool may
  be moved by the collector unless it is pinned for as long as it is lent.

To be measured before the defaults are fixed: what an allocated and
untouched block costs in physical memory on each platform, and what the
test on entry costs in a routine of the 50.

## One caller at a time, and a pool of contexts

A context is not safe for two callers at once, since the routines of its
libraries share state. `ISpiceCore` detects it: each call marks the context
as in use with an interlocked exchange and fails if it already was, at a
cost of a few nanoseconds, with an option to turn the check off. It is not
tied to a thread, because code that awaits may resume on another thread and
still be one caller. The translated layer below has no such check.

A context also holds the kernels loaded into its libraries, so making one
for each request of a server means loading them again each time. The
fitting pattern there is a factory that keeps a pool and lends from it, and
what it pools is contexts, each whole with its libraries, never a library
by itself: a library is nothing without the context it was made over, and
the libraries of one context belong together.

The factory is told once how a context is made and which kernels it is to
hold. What it hands out is a lease: the caller uses the context's libraries
through it and disposes it, which gives the context back, so a lease can be
registered with a dependency-injection container as a transient or a
per-request service and ends with the request. Before a context is lent
again its error state is reset. Open questions for when it is built: what
happens to a context whose borrower loaded or unloaded kernels or changed
the kernel pool, discarded or restored; how many contexts the pool keeps
and whether a caller waits or gets a new one when all are lent; and whether
a context with its kernels loaded can be copied, block for block, more
cheaply than a new one can load them.

The factory is not the toolkit's alone, since any translated program is
hosted the same way. It is to be a project of its own beside the runtime, a
hosting package, which also takes what spans contexts: settings read from
the host's configuration, memory shared between them, and registration with
the usual container. The translator and the runtime depend on none of it.

## What the translator is told

Required: the source files, where their include files are, and where to
write.

With defaults: the namespace and the class name; whether the class and its
methods are public or internal; the convention the procedures follow; the
diagnostics each emitted file switches off for itself; library or
executable; the runtime as a reference or as source; an interface to emit
over the public routines; whether the comments of the source are carried
over; and the placement of saved state, above.

The convention is the shape procedures present to their callers, and it is
a type a caller of the library can derive from, `EmitConvention`. Fortrania's
own is the default and that of `f2c` is supplied, so that code written
against that translator's output can call ours. It decides names, how a
logical result comes back and where the length of a text argument goes, and
is to decide one more thing when it is built: whether an argument the
procedure never writes may be passed by value. Under `f2c` every argument
stays a pointer.

For a real project: the summaries of libraries the sources call into; the
units to leave out because they are written by hand; and the settings for
what differs by platform.

These are too many for a command line, so they are read from a project file,
with flags for the common ones.

**With nothing but the required three, the result builds and runs.** The
aim for any FORTRAN 77 program is that translating it and then running
`dotnet run` in the output folder is all there is to do. So beside the code
the translator writes a .NET project file, unless one is there already,
which it never overwrites. The defaults that make this work:

- Executable when the sources hold a main program, library when they hold
  none. More than one main program is an error that asks which is meant.
- The namespace and class name are taken from the main program's name, or
  from the output folder's when there is none.
- An include file is looked for beside the source that names it.
- The project targets the framework of the SDK the translator runs on and
  allows unsafe code.
- An executable gets the runtime as source. Its project then references no
  package at all, so the SDK alone builds and runs it, with nothing to
  download. A library references the runtime package of the translator's
  own version, so that the two always agree and several libraries in one
  program share one runtime.
- At run time the standard units are the console, a file name is relative
  to the current folder, and the command line reaches the program through
  the procedures the runtime supplies for it.

This is also the path the conformance tests take: translate, run, and set
the output against the reference compiler's.

## The command is `fortrania`, and the target language its first word

The command-line translator is a .NET tool whose command is `fortrania`,
with what to produce as its first word: `fortrania cs` translates to C#.
Other things it does are words beside that one, `fortrania restore` for
fetching sources among them. The command's name is not the program's: the
tool's project names its command itself.

- A name made of the target, as `f2c` and the Java translator `f2j` are,
  needs a name per target, and the obvious ones are not all free: `f2fs`
  is the Linux file system and its tools. One command with the target as a
  word has no such family to find names for.
- `f2` alone, with the target after it, is another program already, a
  widely installed tool that renames files, and two letters are found by no
  search.
- `fortrania` is the project's own name, and is typed mostly in build files
  and scripts, where its length costs nothing.

The short names stay in use where they clash with nothing. A program that
stands alone, for a machine without the tool installed, may be published
with the project's releases under the name of its target, `f2cs`, and the
project is to be tagged with `f2cs` and `f2csharp` so that a search for
either finds it.

Still to be found out when the tool is built: whether one tool package can
give more than one command. Nothing above depends on the answer.

## What a translation's settings do not do yet

`DESIGN.md`, *What is translated is set out as projects and their sources*,
has what is built: the settings, their rules, and the translator that does
a whole translation from them. Left to do:

- **A command to run it.** `fortrania cs` is to read a settings file and do
  the translation, as *The command is `fortrania`, and the target language
  its first word* has it.
- **Translating libraries apart.** Today every source of the settings is
  read in one run. A library translated by itself is to write its
  summaries to a file beside its code, for a later translation that calls
  into it to read.
- **How much header to write**: the one written now, only the
  `<auto-generated>` lines, or none. A setting with those three values.
- **The routines' comments**, carried into the output. Each FORTRAN comment
  line becomes a `//` line with the text after the comment character as it
  stands, so that what is lined up inside a block of comments stays lined
  up, at the indentation of the code it comes before; `f2c` does much the
  same with `/* */`. The reference sources need it: their rules ask for the
  routine headers, disclaimer included, to be kept.
- **Files left out before they are read**, an `exclude` beside `files`, for
  sources that hold something no translation is to see.
- **Variants of a source for one platform.** A file entry may say which
  operating system and which architecture it is for, `os` and
  `architecture`, either, both or neither; the entries with neither are the
  base, and the most specific entry that matches the machine wins, the
  operating system before the architecture. A unit that differs is emitted
  once for each variant and a shim of its name chooses between them, by a
  choice made once when the library is made over its context, from the
  context's `OperatingSystem` and `Architecture`, which default to the
  machine's. Not needed for the reference sources, see `DESIGN.md`, *The SPICE
  Toolkit's translation is a tool of its own*, and so not built until a
  program needs it.
- **Paths that go deeper than one folder.** The last part of a path may
  have `*` and `?`; `**` for folders at any depth is not read.
- **The rest of what a translation is told**, under *What the translator
  is told*: the convention, the diagnostics switched off, the runtime as
  source, the placement of saved state.

## Translating as a step of the build

To be looked at again once the emitter is complete. A project is to be able
to translate FORTRAN as it builds, the way a project compiles `.proto`
files: a package that is used only at build time brings MSBuild targets and
the translator, and the project names what to translate.

- **The targets run the command-line translator as a program of its own**,
  and load nothing into the build. Visual Studio and `dotnet build` load
  build tasks into different runtimes, and a separate program is the same
  one for both. The package is a project of its own, so that the translator
  knows nothing of MSBuild.
- **One settings file serves the command line and the build**, so that a
  translation is the same from either. It holds what *What the translator
  is told* lists, as JSON. A simple case needs no file: the namespace, the
  class name and where the sources are can be given on the project's item.
- **A source generator is not the way.** One runs inside the editor as the
  user types, and is not meant to read thousands of files and write a
  hundred thousand lines.
- **Where the output goes is a setting of each translation.** Temporary:
  under the project's intermediate folder, compiled and never seen, which
  suits a FORTRAN program of the user's own. Kept: in a folder of the
  project, to be committed, which is what `docs/rules/translated-code.md`
  asks of the reference sources. A kept translation is redone only when its
  sources are there, or when asked for by name, so that the project builds
  on a machine that has only the translated code.
- **The step is skipped when nothing changed**, by the inputs and outputs
  MSBuild compares. The summaries of *What crosses program units is a
  summary, not a tree* would allow redoing single units; that is a later
  refinement.
- **The runtime as source** can come the same way: the package adds the
  runtime's files to the compilation, as *The runtime can be handed out as
  source* describes.

**Sources can be fetched**, as `DESIGN.md` describes under *A variable can
say where to download the folder it names*; it is done as part of a
translation, and does nothing when the folder is there. Still to come: a
`fortrania restore` that does only that, for a pipeline that wants fetching
as a step of its own; the archive format of the toolkit's own packages,
`.tar.Z`, which .NET does not read; and a forced run from the build step,
so that rebuilding a project translates it whole.

## Input and output still to build

`DESIGN.md` has what exists, and it is all the main library of the
reference sources uses: 256 statements in 69 program units. Left for the
rest of the reference sources and for other programs:

- `ENDFILE`, which no source of the reference sources uses.
- Unformatted sequential access, and formatted direct access; `BACKSPACE`
  is translated for formatted sequential access only.
- The other answers `INQUIRE` can give, and `BLANK` in `OPEN`.
- In formats: `G`, `T`, `TL` and `TR`, `H`, `BZ`, `SP`, and a scale factor
  with `F`. The runtime gives a format error for each.

**How the screen and keyboard are given to an instance** is to be looked at
again with the managed layer. An instance starts with none, as a process
started by another may be given no console. The ways wanted are the ones a
started process has: none, the program's own console, or writers and a
reader of the host's. A further one is to turn what the toolkit writes to
its error output into an exception, which interrupts where the original
only informed, and so depends on how the code being run uses that output.

Five things about files are decided and not built:

- A program translated to run on its own has the console, as the FORTRAN
  did: the `Main` the translator emits for an executable calls
  `UseConsole`. A library leaves the screen and keyboard unset, and the
  managed layer decides what it shows of what the toolkit writes there,
  its error messages above all.

- `ISpiceCore` is to offer a stream directly, as an overload that takes
  one, over the context's `Register`.
- A program's contexts are to be configurable from the host's settings, the
  usual application settings among them. `FortranContextOptions` is what
  they are read into; which memory a program's libraries keep their
  variables in is to join it. Several named contexts are wanted, each its
  own program.
- A registered stream belongs to one context while it is registered. A
  stream has a single position, so it is not shared between contexts or
  read by the caller in the meantime.
- What a disk file costs per record, read and written with one system call
  each and no buffer, against a buffered or memory-mapped file, is for the
  benchmark project.

## A setting for each variance from the reference compiler

The runtime follows the reference compiler wherever a program can tell the
difference, and [`VARIANCES.md`](VARIANCES.md) lists the places where it
does not. Each of
those was chosen for a library hosted in another program, and a FORTRAN
program translated to run on its own may count on what the compiler does.

So each entry of that list is to get a setting that turns the compiler's
behaviour back on, when a program is met that needs it and not before, with
the library's choice as the default. The settings belong with the rest of
what the runtime is told, *Settings for what differs by platform*, and a
program translated as an executable may want them all on at once, as one
choice of the translator's.

## Errors stay in the toolkit's error state until the managed boundary

The reference toolkit does not throw. A routine that detects a fault records
a short code such as `SPICE(NOLEAPSECONDS)`, a long message built from a
template with `#` markers that later calls fill in, and the chain of
routines it was called through; every caller then checks a flag and returns
early. The translated code keeps exactly that, because it is what the source
does and because an exception on a computation path is ruled out by
[`docs/rules/performance.md`](docs/rules/performance.md).

- The library always runs in the toolkit's *return* error mode. Its default,
  which ends the process, is never available: the routine that would exit
  is hand-written in the runtime and throws instead.
- `ISpiceLib` leaves the error where the toolkit put it. A caller of that
  layer checks and resets it, as a FORTRAN caller would.
- `ISpiceCore` checks after every call and throws one exception type that
  carries the short code, the long message and the traceback, then resets
  the state. The short code is the stable identifier a program switches on.

## Logging is the managed layer's, and optional

The translated library and the runtime reference no logging package. What
the toolkit would print, error reports and anything written to standard
output, goes to a sink the library instance is given, and is discarded when
there is none.

`ISpiceCore` can be given an `ILogger` and connects it to that sink, so a
host sees the toolkit's reports in its own log at a level it controls. A log
line is never written from a computation path, and a host that passes no
logger pays nothing.

## Messages can be localized, in the managed layer only

The translated code and its messages stay as the toolkit wrote them, in
English and in ASCII: the regression tests compare them, and the short codes
are identifiers, never translated. Localization applies to what a person
reads, the long message on the exception `ISpiceCore` throws.

It is possible because nearly every long message starts from a literal
template. Two things are prepared for it from the start, since neither can
be added afterwards without regenerating everything:

- The error state keeps the template and the values substituted into it,
  beside the finished English text. The handful of routines that set and
  fill a message are hand-written in the runtime to do so.
- The translator writes a catalogue of every message template it meets,
  each with a stable identifier, as a by-product of translation.

A localized message is then the translated template for that identifier,
filled with the same values, looked up for the current UI culture from
ordinary .NET resources shipped as satellite assemblies. A template with no
translation, or a message the source assembled without a literal template,
falls back to the English text.

## A C interface for callers outside .NET

The translated library can be compiled ahead of time into an ordinary native
library that exports C functions, one per routine, each a plain forward to
`ISpiceLib` taking a handle to a program's context as its first argument. It
needs no .NET installation on the machine that uses it. The pointer-based
signatures of the translated layer exist partly so that this facade is
mechanical; the translator generates it from the same routine list.

Exports cannot be instance members, so they are static methods, and a
method becomes a C export only where a project is published ahead of time
as a native library. What that gives is one file with everything compiled
into it, the translated code, the runtime and a copy of .NET's own. Two
such files in one process are two of everything, each with its own table
of handles, so a program loads one native library of ours and never two.

Two libraries are wanted, the second made from the first:

- **The toolkit's own exports all take a handle first.** That is this
  port's native interface, and it keeps what the port adds: several
  independent programs in one process. They come in two styles, the
  FORTRAN routines as they are, and the reference C toolkit's names and
  argument lists with the handle in front. The conversions the second
  style does, C strings, row-major matrices, zero-based indices, are the
  same ones `ISpiceCore` needs, and both are generated from the routine's
  argument list and its documented inputs and outputs, with a short list
  of routines written by hand. The reference C wrappers are not
  translated. A program made for this interface makes a context first,
  through an export that takes the settings, and passes its handle on.
- **A second library stands in for the reference C toolkit as it is.** Its
  project refers to the toolkit and adds the same functions without the
  handle, each a forward to the one with it, so that an existing program
  changes libraries without changing source. It keeps one context of its
  own for the process and makes it the first time one is needed, with what
  a program run on its own has. Published, it is the one file, the
  toolkit's exports and its own.

The default context is that second library's and not the runtime's, as
`DESIGN.md` has it. The other way round was weighed and set aside: with
the functions that take no handle in the toolkit and the second library a
bare stub that only makes the default, the stub has no function of its own
to make it from and would depend on code running by itself when the file
is loaded, and the toolkit would carry functions that fail until something
else has been run.

The handle is the context's own, `FortranContext.Handle`, and an export
turns it back with `FromHandle` and asks the context for the library; no
library is made by the caller. Those two exist in the runtime; the exports
do not yet.

A routine that takes a procedure takes it, in this interface, as a C
function pointer whose first argument is the handle:
`void (*udstep)(void *handle, double *et, double *step)`. Three things
follow from the handle being there:

- The export stores the C pointer in the instance and passes the translated
  routine a fixed static forwarder, which is called with the instance,
  finds the pointer there and calls it. Nothing is allocated. The export
  puts back what was stored before when it returns, since a callback may
  itself start a call that stores another.
- The exports have that same shape, so a C program can pass one of them as
  the callback, as programs written for the reference C toolkit pass its
  default step, refinement and progress routines. That costs two
  transitions per callback, out to native code and straight back in; the
  export can recognise the address of one of its own and pass the managed
  routine directly, if a benchmark asks for it.
- A callback written by the user has the handle it needs to call the
  library from inside.

No .NET exception may pass through a native frame. Every export catches
what the translated code throws, a `STOP` among them, and reports it by its
return value, also when the export was itself reached from a callback.

A call from managed to native code is not marshalled here, since every
argument is a pointer or a number, and still costs a transition in each
direction, some tens of nanoseconds: the thread is marked as running native
code so that the garbage collector need not wait for it. The transition can
be switched off only for a native function that is short, never blocks and
never calls back, which a callback into the library is not.

## A COM interface, on Windows

A second generated facade over `ISpiceLib`, using the source-generated COM
support in .NET, for spreadsheet and scripting hosts. It is separate from
the translated layer so that the layer carries nothing specific to one
operating system. After the C interface, and only if asked for.

## An extended-precision variant

A translator option that maps `DOUBLE PRECISION` to a wider floating-point
type and generates the library a second time, for callers who want less
round-off inside long computations and accept the cost in speed.

What is known before starting: kernel files hold 64-bit values, so results
stay limited by their inputs; .NET has no built-in type wider than 64 bits,
so one has to be written or adopted; the toolkit's convergence tolerances
are tuned for 64 bits and need review; and the regression tests' expected
values no longer apply exactly.

## A variant with 16-bit characters

A translator option that makes the default `CHARACTER` kind a 16-bit unit
and generates the library a second time, so that a caller holding UTF-16
text, a .NET string, a COM string, or `wchar_t` on Windows, passes it
without converting. The two libraries can share one runtime and one
assembly. It is recorded and not planned: converting a short name to bytes
on the stack costs nanoseconds, and the price is a second library to test
and ship. What is known before starting:

- One library has one width throughout. Character values pass between
  units as bare addresses, so units cannot be mixed. The boundary is the
  runtime's input and output: text files are decoded and encoded, and an
  unformatted read or write turns each byte into one unit of the same value
  and back, since the fields of a binary file are of fixed width and some
  code reads characters as raw bytes.
- A kernel still holds bytes, so the variant stores no text a kernel could
  not hold before. File names outside ASCII need no variant: as UTF-8 they
  pass through the byte library untouched.
- Code that takes a character's code to lie below 256 has to be found and
  checked, and so has any `EQUIVALENCE` of character data with numbers.
- It is the default kind's width, and not every character's. Later
  standards have kinds a program declares, the reference compiler's
  32-bit one among them, and each of those would keep a width of its own.
- .NET's `char` is 16 bits on every platform, and `wchar_t` is 32 bits
  outside Windows, so this serves no `wchar_t` caller there.

## Memory-mapped kernels

An optional way of reading a kernel that is opened read-only, in place of a
positional read per record. It is to be added only if a benchmark shows it
is worth it, since a mapped file turns an input error into a fault and does
not suit a file that is being written.

## Conformance tests against a reference compiler

The reference sources do not use all of FORTRAN 77, so their regression
tests cannot show that the translator handles the rest. For that, and to pin
down behaviour the standard leaves to the compiler, the translator is to be
tested against gfortran, one small program at a time:

1. A FORTRAN program written for the test, exercising one construct and
   printing what it computes.
2. The output gfortran's build of it prints, committed beside it as the
   expected output.
3. A test that translates the program, compiles and runs the translation,
   and compares what it prints with the expected output.

The expected output is committed, so running the tests needs no FORTRAN
compiler; only writing a new program, or regenerating the outputs, does, and
then the compiler is found on the `PATH`. The programs are written for this
repository and are its own, unlike the reference sources, so they are
committed. Each construct in *FORTRAN 77 that Fortrania does not handle yet*
is added with its programs.

The same arrangement serves the runtime: how a number is formatted, what
list-directed output looks like and how an unformatted record is laid out
are each a program and its output.

It needs the emitter and enough of the runtime to print, so it starts when
those exist. Until then the compiler is used by hand to settle single
questions, and what it settles is recorded where the answer is used.

## Validating the translated library

Two checks are wanted once the main library of the reference sources runs,
and neither replaces the other.

- **The reference sources' own tests.** They come with a test program and
  its families of test routines, in FORTRAN like the library. Translated
  with the same translator and run, they test the translated library as
  they test the original. They need the rest of what the reference sources
  use beyond the main library, which *Input and output still to build*
  lists, and a way to run a translated main program.
- **The same run under both.** A test program run as the reference compiler
  builds it and as the translator translates it writes two logs, and the
  logs are compared. Where the tests only say pass or fail, this compares
  the numbers themselves, to the last digit where the two agree and with
  the difference shown where they do not. Differences are expected in the
  last digits of what the mathematical library computes, a sine or a
  logarithm, where .NET and the reference compiler's library need not
  round alike; each is to be traced to such a call or treated as a fault.

The second needs the reference compiler and so is run by hand or in a job
of its own, like the regeneration of expected outputs under *Conformance
tests against a reference compiler*.

The first is done, by hand: 2,211 program units in two projects, built
with no error and no warning, and run on Windows beside the reference
compiler's build of the same sources with the same line ending. All 400
families run in both, and every family and every case that fails is the
same in both; `DESIGN.md`, *The SPICE Toolkit's translation is a tool of
its own*, has why two fail. What is left of it is to make it a job that
can be run again.

The second is done by hand as well. A program of ours, not the toolkit's,
makes the test utilities' kernels and calls some 25 of the main library's
routines over 40 epochs: time conversion, states with each aberration
correction, frame transformations, rotations and their conversions,
coordinate conversions, conic elements and propagation, and a sub-point.
It prints each double to 17 significant digits, which tells any two
doubles apart. Built by the reference compiler, against its build of the
toolkit, and translated with the toolkit, it gives 8,604 lines, and the
two outputs are the same byte for byte.

That includes what the mathematical library computes, which was expected
to differ in the last digit. On Windows, .NET's `Math` and this build of
the reference compiler call the same C runtime library, so they agree; on
another platform both call that platform's library, and the two builds
there are to agree with each other, not with the builds on Windows.

What is left is to make the program and its comparison a job that runs
again, over more of the library. It is the toolkit's and not the
translator's, so it goes to dc-spice when that repository exists, as a
tool of its own or as its integration tests, and is not committed here
before the move; see *Fortrania becomes a repository of its own*.

## Fortrania becomes a repository of its own

The translator, its runtime and its command move to a repository of their
own, dc-fortrania, and are published as packages; what is SPICE's stays
here, to become dc-spice. Fortrania is the larger product and the one with
other users, and it holds nothing of the toolkit's, so nothing stops it
being published but its readiness.

**Not before it has been tested on the toolkit, and made fast and clean.**
In this order, because each step changes what the next one looks at:

1. **The toolkit's tests agree with the reference compiler's build.** Done
   for the outcome of every family and case; see *Validating the
   translated library*.
2. **Agreement to the last digit**, the second check there, over the main
   library's core routines. Done by hand for some 25 of them; to become a
   job, and to reach further.
3. **An optimization round**, for speed: the runtime's hot paths and the
   code the translator emits, measured with a benchmark project as
   `docs/rules/performance.md` asks, and the rewrites listed under *More
   that the translator can write more simply*, which make the translator
   an optimizer for what keeps the behaviour exactly. Each change is held
   to the same tests, so the first two steps say whether it changed a
   result.
4. **A cleanup round**, against the C# rules, after the optimization round
   so that what it tidies is the code that stays. A published type is a
   promise, so what is `public` is decided type by type here, the
   translation settings above all.
5. **The command**, `fortrania cs`, published as a .NET tool; see *The
   command is `fortrania`, and the target language its first word*.
6. **Which runtime a translation runs on.** Emitted code calls the
   runtime's API, so a package's user has to know which runtime goes with
   which translator. The rule already decided for a scaffolded library,
   the runtime of the translator's own version, is to be the rule for the
   packages: one version number for the two.

**The move keeps the history.** `libraries/fortrania` is moved with its
history, by rewriting a copy of this repository down to that folder, and
not as a fresh copy of the files. The new repository is initialized with
the agentics toolkit, takes the root build configuration, and divides the
documents by subject: what is the translator's and the runtime's in
`DESIGN.md`, this file, `GLOSSARY.md` and `VARIANCES.md` goes, and what
is the toolkit's stays; `docs/rules/performance.md` and `portability.md`
hold in both, and `translated-code.md` stays. Here the translator and the
runtime are then packages, which closes the cross-component project
reference `TODO.md` lists.

**It is the first repository with pipelines.** Nothing here builds on a
server yet, and dc-fortrania is where that starts:

- **A development pipeline on GitHub.** Every pull request and every push
  to the default branch is built and tested; a push to the default branch
  also packs a development version of each package.
- **A publishing pipeline, started on purpose.** It builds what a release
  names, and publishes to nuget.org with trusted publishing, which trades
  a short-lived token for the pipeline's own identity, so that no API key
  is stored anywhere. nuget.org offers it to GitHub Actions; a publishing
  pipeline elsewhere, Azure DevOps say, would need a key, and that weighs
  in the choice `TODO.md` lists.
- **Its tests need nothing of NAIF's.** Fortrania's samples are its own,
  checked against the reference compiler by hand, so the pipelines need
  neither the toolkit's sources nor the compiler. dc-spice's pipeline,
  later, downloads the sources by their hash and must never upload them,
  as an artifact or a cache anyone else can read, since that would be
  publishing them.
- **The reference compiler comes from the machine, not from a package.**
  No package of the .NET ecosystem carries gfortran, and one would weigh
  some hundreds of megabytes for each platform. Where a pipeline needs
  it, for dc-spice's comparison with the reference build or to check
  Fortrania's samples again, the pipeline installs it as a step of its
  own, as the Fortran community's setup action does on Linux, macOS and
  Windows runners. A test that needs it looks for it on the path and,
  where it is missing, says that it was skipped and why, and never
  passes in silence.

What is learnt setting these up, the steps, what failed and the rules
that came of it, is written down as it happens, to become rules and
templates of the agentics toolkit for every repository after this one.

## More that the translator can write more simply

What keeps the behaviour exactly is done without a setting, as
`DESIGN.md` has it under *Constants are joined when translating*; each of
these is known when translating, and each that is made for speed comes
with its measurement, as `docs/rules/performance.md` asks. A number is the
same only if it is the same bits the reference compiler gives, so a
rewrite of arithmetic is checked against that compiler and not argued.

- **A character value compared with a constant.** `NAME .EQ. 'J2000'`
  goes through the runtime's comparison, which pads the shorter with
  blanks as it goes. With one side a constant of known length, the
  comparison is a compare of that many bytes and a check that the rest of
  the other is blank, which can be written in place.
- **A whole array copied or cleared by a `DO` loop.** A loop whose body
  only assigns one array's elements to another's, or a constant to each,
  with nothing else read or written, is a span copy or fill, when the
  two arrays are known not to overlap.
- **A real raised to a small constant power.** `X**2` as `x * x` is what
  the reference compiler does too; a larger power, or one of another
  type, is checked bit for bit first.

The toolkit's translation is the measure of which are worth it: each is
counted there before it is built, as the joined constants were.

## How much goes on the stack is a setting

What a method needs for itself, a local array, a character variable, a
place where a value is joined, lies on the stack when it is small and in
the instance's block otherwise, as `DESIGN.md` has it under *What a unit
keeps between calls*. The limit is the emitter's own, 4,096 bytes, fixed;
a place for joining that would be longer is refused. It is to be a setting
of the translation, `stackLimit`, with that default, and 0 for nothing on
the stack: a platform with a shallow stack, or a program that recurses
through a deep chain of calls, then sets it lower. A place for joining
that is over the limit goes to the block like a local, and is no longer
refused.

The limit is applied when translating, because every size is known then
and the choice costs nothing when the code runs. Choosing when it runs
would mean writing each such method twice, once on the stack and once in
the block, which is not worth it until one binary has to serve platforms
with different stacks.

## Measurements waiting for a benchmark project

[`docs/rules/performance.md`](docs/rules/performance.md) asks that a change
made for speed come with a benchmark, and the component has no benchmark
project yet. These are the questions it is to answer first, each a change
that is not to be made before it does:

- **Short copies.** Character assignment and concatenation copy with the
  framework's overlap-safe copy. Character values in FORTRAN are mostly
  short, names and brief messages, and an earlier measurement of ours on an
  older framework had a plain block copy 8% ahead at 20 bytes and slightly
  behind at 100. Where the emitter knows two values cannot overlap, as when
  joining into a place of its own, the plain copy could be written. To
  measure on the current framework: both copies at 4, 8, 20, 32 and 100
  bytes, with the length constant and with it variable.
- **Equality of character values.** `.EQ.` and `.NE.` go through the same
  comparison as the ordering operators, which finds the order of the two.
  A test for equality alone can stop earlier and compare whole words.
- **Arguments by value.** Analysis says which arguments a procedure never
  writes; what passing those as values saves, against the pointer every
  argument is today, is not known.
- **Saved state.** The two questions at the end of *Saved state is placed
  at translation and loaded at run time*: what an allocated and untouched
  block costs, and what the test on entry costs.
- **Records.** A record read by number is one system call, and one copy
  unless the item is the whole record. Kernel reading is made of these, so
  what a buffer of several records, or a mapped file, would save is the
  first thing to measure once kernels load.
- **Callbacks from native code.** What the two transitions cost when a C
  program passes one of the library's own exports as a callback, against
  the direct managed call, in a search that calls it per step.
- **Converting text at the managed boundary.** What turning a short .NET
  string into bytes costs per call, which is the whole case for *A variant
  with 16-bit characters*.

## Back ends beyond C# source

The emitter writes C# source text directly from the analysed program, with
no tree of C# objects in between: the output's shape is fully ours, the
original comments have to pass through, and there are over a million lines
to write. That is the back end the translated library is built with, and the
only one that runs everywhere, since it needs nothing at run time.

Analysis is to leave nothing in its result that only a C# emitter could use,
so that other back ends remain additions. Three are wanted, none before the
C# one works. All of them make FORTRAN loadable at run time, as a plug-in to
a .NET program, which the C# back end alone does not.

- **Compile the emitted C# in memory.** The same emitter, then the C#
  compiler as a library. It costs nearly nothing to build, and a large
  dependency and a compile at start-up to use. It needs a just-in-time
  compiler.
- **Emit intermediate language directly.** A second emitter and real work,
  wanted mainly for `GO TO`: a branch there can go anywhere, where C# cannot
  jump into a block. It also needs a just-in-time compiler.
- **Build expression trees.** `System.Linq.Expressions` has an interpreter,
  so this is the one that can run FORTRAN where no code can be generated,
  slowly. Its jumps can enter a block too.

What expression trees accept was measured on .NET 10 and shapes that back
end. A by-reference parameter works, as does passing an array element by
reference, compiled and interpreted. A pointer type is refused outright. A
`Span<T>` is refused as a local and through its indexer, and as a parameter
compiles to an invalid program. An address held as a native integer is an
ordinary value there: it can be a parameter, be moved by an offset and be
passed on, and the memory behind it is read and written by calling small
unsafe helper methods of the runtime, one to read and one to write. A helper
that returns a reference is no use, since a call cannot be assigned to.

So that back end has two possible shapes, to be chosen when it is built:

- Addresses as native integers, over the same unmoving memory as the C# back
  end. It shares the runtime and can call compiled translated code and be
  called by it, at the cost of a helper call for every element.
- Managed arrays with an offset, never a pointer or a span. Its code is
  verifiably safe where the C# back end's is not, and it shares nothing.

The second shape is not tied to expression trees. Written out as source it
is also how a language without pointers or spans, Visual Basic for one,
could be a target of the translator: an array argument is an array and a
starting index, a single value is passed by reference, and text is bytes
with an offset and a length. Three things about it are worth knowing ahead:

- FORTRAN 77 has no recursion, so a local array can be allocated once and
  kept by the library instance; after the first call nothing is allocated.
- Every array access is bounds-checked, and an array argument costs two
  values where a pointer costs one.
- A call that leans on arguments being passed by address has no direct
  form. A single value given where an array is expected needs a temporary
  array copied in and out, and storage read as another type, by such a call
  or by `EQUIVALENCE`, needs runtime helpers that copy or reinterpret bytes.

None of these is for the translated SPICE library, which ships as compiled
C# so that it runs where code cannot be generated at run time.

## The runtime can be handed out as source

Translated code needs the Fortrania runtime, and there are to be two ways to
have it: a reference to the runtime assembly, or the runtime's own source
files written out beside the translated ones, so that everything compiles
into one assembly with no second file to ship. The second suits an
executable, a plugin, a trimmed or ahead-of-time compiled program, and
anyone who has to vet each dependency. Its cost is that a fix to the runtime
reaches such a user only when the files are written out again. Source is
the default for an executable and the reference for a library, for the
reasons under *What the translator is told*; either can be asked for.

The runtime is its own project and assembly from the first day, never part
of the translator, and translated code references nothing else of Fortrania.
Its source files are embedded in the translator as resources, the very files
the runtime assembly is compiled from, so there is no second copy to drift.
An option of the command-line tool chooses between the two ways. A package
that delivers the same files as source, for use without the tool, can follow.

For the files to compile in a project that is not ours, the runtime keeps to
these rules:

- It depends on nothing but the base class library.
- Each file stands alone: it states its own `using` directives and nullable
  setting, and relies on nothing the repository's build configuration
  supplies.
- Its types are public in the runtime assembly and internal when compiled
  into another, decided by one conditional compilation symbol, so that they
  do not become part of that assembly's own surface.
- Its namespace is fixed, and the emitter writes the same code for either
  way.
- Each file carries the licence header, since the file then travels alone.

The project that receives the files has to allow unsafe code, as the
translated code needs in any case.

## FORTRAN 77 that Fortrania does not handle yet

The translator is meant to cover the whole language in the end. What the
reference sources do not use is left until the rest works, and until then
each of these is reported as unsupported and never translated by guess:

- `GO TO` in every form, statement labels as branch targets, and alternate
  returns. The reference sources carry no statement label at all.
- A `DO` that ends at a labelled statement. Every loop in the reference
  sources ends with `END DO`.
- `FORMAT` statements. The reference sources give every format as a
  character constant.
- Hollerith constants.
- Complex constants, and with them complex constant expressions.
- An intrinsic function in a constant expression, which later standards
  allow and FORTRAN 77 does not. The reference sources have none.
- An include file that holds an executable statement or an `ENTRY`. Every
  include file of the reference sources holds declarations alone.
- A name starting with `E` or `D` directly after a length, as in
  `REAL*8 E1`, which the lexer reads as a number.
- Tab-form source.
- A jump from the statements of one entry point into those of another,
  which comes with `GO TO`. A method per entry point cannot hold it; such a
  unit needs the form `f2c` writes, one body with every entry's arguments
  and a selector that says where to start, and a thin method per entry.

## Settings for what differs by platform

Where the reference toolkit differs by platform in a value and not in logic,
the longest file name and the most files open at once, the value is to come
from a settings class the runtime is given, with the larger of the
reference's values as the default, in place of a constant in translated
code. `docs/rules/portability.md` has the table of differences.
