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
pinned for the length of the call, which a managed layer over a translated
library does on the caller's behalf.

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

Where the memory comes from is the context's allocator, under *How much
goes on the stack is a setting*, *Memory comes from an allocator, in two
layers*.

To be measured before the defaults are fixed: what an allocated and
untouched block costs in physical memory on each platform, and what the
test on entry costs in a routine of the 50.

## One caller at a time, and a pool of contexts

A context is not safe for two callers at once, since the routines of its
libraries share state. A managed layer over them is to detect it: each call
marks the context as in use with an interlocked exchange and fails if it
already was, at a cost of a few nanoseconds, with an option to turn the
check off. It is not
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
- **Files left out before they are read**, an `exclude` beside `files`, for
  sources that hold something no translation is to see.
- **Variants of a source for one platform.** A file entry may say which
  operating system and which architecture it is for, `os` and
  `architecture`, either, both or neither; the entries with neither are the
  base, and the most specific entry that matches the machine wins, the
  operating system before the architecture. The toolkit's test program is
  the first that would use it: of its families, the Windows package's
  copies of three differ from Linux's. `F_ZZASC2` expects other kernels
  refused, and is rewritten after translation until this is built, when
  the rewrite goes, though a variant chosen by operating system is then
  wrong for a context told to write another platform's line ending;
  `F_DNEARP` has a case that Linux's copy lacks; `F_TANGPT2` allows wider
  tolerances, which the translation does not need. The test package of
  each platform is then a source of its own, fetched and checked against
  its own hash. A unit that differs is emitted
  once for each variant and a shim of its name chooses between them, by a
  choice made once when the library is made over its context, from the
  context's `OperatingSystem` and `Architecture`, which default to the
  machine's. The reference sources turned out not to need it: one routine
  of theirs has to know the platform, and a command run after the
  translation rewrites that one. So it is not built until a program needs
  it.
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
  project, to be committed, which suits a library whose sources cannot be
  published with it, as the reference sources cannot. A kept translation is redone only when its
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
  benchmark project. A first measurement, of the toolkit's tests that
  handle many kernel files, found a record's read at some 15 µs and its
  write at 20 µs, on a slowed machine, and both small beside what opening
  files cost there, where a scanner looks at each file that was written
  when it is next opened; a buffer has yet to show it
  is worth what it complicates, which is a file open in two units or two
  programs at once.

## Input and output is to be looked at again as a whole

Not designed yet: what follows is the ground to start from, and none of it
is decided. The aim is that a developer controls where each file is and how
it is reached, and that what they get without asking is the fastest that is
safe.

**What there is.** Two things are easily taken for one. The *access* is
FORTRAN's and comes from the `OPEN` statement: direct, which is records of
one length read and written by number, and is every binary kernel; or
sequential, which is records in order with `REWIND` and `BACKSPACE`, and in
the reference sources is text: text kernels, logs, the screen and the
keyboard. The *store* is where the bytes are: a disk file reached at an
offset with no buffer, bytes in memory that are only read, a file in memory
that is written, a stream of the host's, and later a mapped file. Both
kinds of access are built on one contract, `FortranFile`, which reads and
writes at an offset and keeps no position, so every store serves both.

**What is untidy.** A name reaches a store in three ways with a fixed order
between them: the context's one provider, a registered stream, and
registered bytes. A context has one provider, so kernels in memory and
output on disk takes a provider written by hand. A buffer has no place to
go but inside each store. A provider is told a name and a mode and nothing
else.

**Three layers, and what each might do.**

- *The translated program* knows a name and what its `OPEN` said: the
  access, the form, the record length, the status. The translator can add
  what the FORTRAN never had: which library, which program unit, perhaps
  which statement. All of it is to be passed without the program's source
  or its behaviour changing.
- *The runtime* keeps what is FORTRAN's: units, records, formats,
  positions. To choose between: it also holds a few general abilities of
  its own, a buffer among them, written once and light; or it passes every
  request on and holds none. The first keeps providers small, the second
  keeps the runtime from having to change.
- *The provider* decides what a name is. A few plain ones would come with
  the runtime: disk, memory, bytes to read, a stream. One more would choose
  among others by rule, and take a function of the host's for the rule,
  set up the way services are registered with a host. The streams and
  bytes registered today would become one of the plain ones, in front of
  the rest, so that a name has one way to a store.

**Further ground.**

- A buffer or a cache is safe only for a file no other unit and no other
  program has open, so a request would say which it is, or the provider
  would.
- A store whose bytes are in memory could hand out a span over them in
  place of copying a record, as an ability a file may or may not have.
- `OPEN` in FORTRAN 77 cannot say that a file is only to be read, so the
  runtime cannot tell a kernel being read from one being written, and the
  fastest stores need to know. It would come from the host, or from a rule
  on the name.
- Whether each read and write carries what the opening did, or only the
  opening does: a call per record is the path that has to stay short.

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

## Before Fortrania is published

Fortrania was begun beside the library it was written to translate, NASA's
SPICE Toolkit, and moved to this repository with its history. It holds
nothing of the toolkit's, so nothing stops it being published but its
readiness.

**Not before it has been tested on the toolkit, and made fast and clean.**
In this order, because each step changes what the next one looks at:

1. **The toolkit's tests agree with the reference compiler's build.** Done
   for the outcome of every family and case. The check is the translated
   library's and is run in its repository, since it needs the toolkit's
   sources.
2. **Agreement to the last digit**, over the main library's core routines.
   Done by hand for some 25 of them; to become a job there, and to reach
   further.
3. **An optimization round**, for speed: the runtime's hot paths and the
   code the translator emits, measured with a benchmark project as
   `docs/rules/performance.md` asks, and the rewrites listed under *More
   that the translator can write more simply*, which make the translator
   an optimizer for what keeps the behaviour exactly. It is built on the
   tree of *A tree between the analysis and the writers*, which comes
   first, and each rewrite is a pass over it. Each change is held
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

**Development packages come before all of that.** The translated library
is built in a repository of its own and takes the translator and the
runtime as packages, so packages of a development version are made from
the first pipeline on, long before a release. How they are numbered and
where they go is in `DESIGN.md`, *A package's version is in the code, and
the build says what kind it is*.

**It is the first repository with pipelines.**

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
  neither the toolkit's sources nor the compiler.
- **The reference compiler comes from the machine, not from a package.**
  No package of the .NET ecosystem carries gfortran, and one would weigh
  some hundreds of megabytes for each platform. Where a pipeline needs
  it, to check Fortrania's samples again, the pipeline installs it as a
  step of its own, as the Fortran community's setup action does on Linux,
  macOS and Windows runners. A test that needs it looks for it on the path
  and, where it is missing, says that it was skipped and why, and never
  passes in silence.

What is learnt setting these up, the steps, what failed and the rules
that came of it, is written down as it happens, to become rules and
templates of the agentics toolkit for every repository after this one.

## A tree between the analysis and the writers

The emitter turns the analysed program into C# text in one step, so what
it decides and what it writes are mixed: the joining of constants under
*Constants are joined when translating* in `DESIGN.md` is done on the
strings as they are written, and a command that changes a routine after
the translation reads the output back with the C# compiler, changes that
tree and writes it again. The optimization round, step 3 of *Before
Fortrania is published*, puts a tree in between, and is built on it:

    parse → analyse → lower to the program tree → passes → a writer

**The program tree is the program after lowering, with nothing of
FORTRAN's left in it.** Blocks, assignments, calls, loops and
conditionals; storage, as locals, saved state and common blocks; typed
memory, as addresses and offsets; calls into the runtime, for text and
for input and output; and comments, as nodes of their own, so that they
pass through every stage. It is the translator's own, and so its shape is
ours, which is why the emitter writes text and not the C# compiler's
tree; see *Back ends beyond C# source*.

**A pass takes a tree and gives one.** What the translator now does in
the emitter, or after it, becomes a pass, and so does what is planned:

- joining constants, and the rewrites under *More that the translator can
  write more simply*;
- the layout of working buffers of *How much goes on the stack is a
  setting*;
- the SPICE tool's platform rewrite, which becomes a pass the tool gives
  the translator rather than a second reading of its output; *Settings
  for what differs by platform* is the runtime's side of the same.

**A writer only writes.** Each is a walk over the tree, choosing no more
than spelling and layout, and the C# writer is the first.

**It is not the framework's CodeDOM, though it is the same idea.** CodeDOM
was one tree with a writer for each .NET language, and it has nothing for
what the translation is made of: no pointers, no `fixed`, no `stackalloc`
and no UTF-8 literals, each of which would be a snippet of C# text in the
tree. Nor does it change any more. Its neutrality was also only as wide as
what its languages shared, and so is this tree's: a writer for another
language needs a runtime in that language too.

**F# and Visual Basic are tried while the nodes are designed.** Not as
writers yet, but as a check that the tree is a common core with small
variations rather than C# in disguise. A few of the samples are written
by hand in each, to find what the core must not take for granted:

- **F#** has pointers, but a read or a write through one is a call of
  its native pointer functions rather than an operator. Its functions are expressions,
  with no `return`, `break` or `goto`, so an early exit is a flag or a
  restructured loop.
- **Visual Basic** has `GoTo`, `Exit` and `Return`, and no pointers at
  all; there, the managed-array shape under *Back ends beyond C# source*
  is the answer.

Each writer says which of these it has, pointers, jumps and early exits
among them, and a pass lowers what it lacks into what it has before the
writer sees the tree. Which differences the core holds and which a pass
removes is what the hand-written samples are to settle.

**Built a piece at a time, each piece checked against the output it
replaces.** Expressions first, where most of the string handling is, with
the present writing kept for statements in the meantime; then statements.
The C# writer is to give the same files, byte for byte, as the emitter
before it, over the toolkit's whole translation, so each step is checked
by comparing two folders before any test is run, and the translation's
time is measured before and after.

**Opened to others last.** Once the tree has settled under the C# writer,
it is the published interface for tools of others: its types public in
the translator's package, so that a .NET tool takes and gives trees in
memory, and a JSON form over them for tools in other languages, with a
schema and a version in each file. Each published node is a promise, and
the reason this waits for the move to dc-fortrania and the cleanup round.
After that, a translation's settings may name passes of a user's own, to
run before or after a stage.

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

What a method needs only while it runs, a local array, a character
variable, a place where a value is joined, its working buffers, lies on
the stack when it is small and in the unit's block otherwise, as
`DESIGN.md` has it under *What a unit keeps between calls*. The limit is
the emitter's own, 4,096 bytes; a place for joining that would be longer
is refused. One library is to serve both a deep stack and a shallow one,
by a choice of the context.

**Two modes, chosen per context.** In the stack mode a method's buffers
are on the stack, as now. In the heap mode they are at a fixed place in
a block of working buffers that the context holds. Each method's buffers
are one piece in either mode, and the method tests the context's setting
once on entry, a branch that is always predicted the same way:

    byte* _locals;
    if (_context.LocalsOnStack)
    {
        byte* onStack = stackalloc byte[17600];
        _locals = onStack;
    }
    else
    {
        _locals = _context.Frames + 41200;
    }

    byte* chrbuf = _locals + 0;
    byte* eqryc = _locals + 2000;

A buffer over the 4,096 bytes is at its fixed place in both modes, so the
block exists in both, and a place for joining that is longer is no longer
refused.

**The fixed places are laid out at translation.** FORTRAN 77 has no
recursion, and the stack report proves it of a translation, so a method
can start where the buffers of its deepest caller end: methods that can
run at the same time never share bytes, and methods that never do share
them, as calls one after another at one depth share the stack. The block
is then exactly the deepest chain, 76,533 bytes for the toolkit, and a
method's place is a constant: nothing is taken or given back on a call,
nothing is to be undone after an exception, and the block never grows.
The translator asserts that every place lies inside the block, from the
same walk of the calls that makes the report.

**A unit's saved state stays apart.** What `SAVE` and `DATA` keep is the
program's state and stays in each unit's block, loaded on its first call
and held until the context is disposed. What is in that block now only
for being too large for the stack moves to the working side, so the unit
blocks hold state alone.

**Regions.** The block has a region for each library a program uses,
since a library's layout starts at its own beginning: 76 KB for the
toolkit and 91 KB for its test program, which calls into it. Projects
never call each other both ways, so regions apart are enough.

**A callback that calls back in gets a region of its own.** A procedure
the host passes, a user function for a search, may call the library
while a chain is running, and the layout cannot know of it; that is
legal FORTRAN as long as it calls no routine that is running. Every
procedure the host passes goes through the managed boundary, and the
boundary gives each such nested entry the next level's region: of the
library's full size, taken the first time the level is reached within an
outermost call, reused by every later callback at that level, and given
back when the outermost call returns. Level 0 is taken when the context
is made and is never given back. The method does not know its level; the
boundary moves `Frames` and puts it back. Past a depth of 16 the boundary
throws rather than take another. In the stack mode a nested chain goes
further down the thread's stack instead.

**A routine called again while it runs throws.** FORTRAN 77 does not allow
it, and the toolkit's state is not made for it: a user function for
`GFUDS` that calls `GFUDS` overwrites the search's saved state. Only a
unit with saved state can be harmed, since its buffers are per call or
per level, and only while a callback runs, so only a unit with `SAVE`
state that can be running then carries a flag, a field of the instance:

    if (_running_GFUDS == _context.CallNumber)
    {
        throw new FortranRecursionException("GFUDS");
    }

    _running_GFUDS = _context.CallNumber;

and before every return, `_running_GFUDS = 0;`. The boundary numbers
each outermost call, so a flag an exception left set holds an old number
and matches nothing: no `try`/`finally` is needed. The translator finds
those units by the walk that makes the report: those on a chain to a call
through a procedure argument.

**Memory comes from an allocator, in two layers.** `IMemoryAllocator`
hands out `MemoryBlock`s: a pointer, a length, the alignment asked for, 64
bytes unless asked otherwise, and whether it is zeroed. Disposing a block
gives it back, once however often it is disposed. It hands out large
pieces only, the regions, the units' blocks of saved state and files
loaded, never a method's buffers one call at a time.

- **The shared allocator**, one for the process, is safe for any thread,
  since contexts on every thread use it. It takes from native memory, or
  from pinned arrays, `GC.AllocateUninitializedArray<byte>(size, pinned:
  true)`, which never move and which the collector frees, offsetting
  within the array where the alignment is above 8. It pools: a block given
  back at or under a threshold, 1 MB unless set, goes to a list for its
  size class, the powers of two, and is handed out again; a larger one goes
  back to the system; a cap on the bytes pooled and a `Trim` keep an idle
  process from holding memory. The framework's own array pool is not used,
  since its arrays can be moved by the collector unless pinned for as long
  as they are lent. Pooling pays here, where one context's blocks serve the
  next across threads, which is how a pool of contexts comes and goes
  cheaply; see *One caller at a time, and a pool of contexts*.
- **Each context's allocator** stands in front of the shared one, with no
  lock, since a context has one caller at a time. It keeps what the context
  gives back while it lives, the regions of nesting levels after each
  outermost call, and hands it out again; it goes to the shared allocator
  only when it has nothing to give and when the context is disposed. Its
  settings are its own: a threshold, a cap, or the host's own allocator
  behind it. A lock left out saves little on allocations this rare; what
  the layer gives is the reuse and the settings.

**The settings can change while the program runs.** The runtime makes the
shared allocator reachable, and its settings are one object that is never
changed, only replaced whole; each allocation and each release reads it
once, so changing them takes no lock, and one that races a change goes by
the old settings or by the new. A block keeps its own size class,
alignment and source, so a block taken under the old settings is given
back rightly under the new ones. Lowering the threshold or the cap trims
the pool to it, and turning pooling off empties it; blocks in use are not
touched. A context's allocator takes the same settings object.

**Saved state is zeroed, working buffers are not.** *Memory is zeroed
unless asked otherwise*, under *Saved state is placed at translation and
loaded at run time*, holds for the units' blocks. The regions of working
buffers ask for memory unzeroed, which comes straight from the pool or the
system, as `stackalloc` would under `[SkipLocalsInit]`.

**A file can be read from memory.** The runtime reads every file through
`FortranFile`, which has a length and reads and writes at an offset, both
`long`. One more implementation reads from a region of memory: a pointer,
a length, an owner that keeps the memory alive and is disposed with the
file, and whether it may be written. A provider gives names to regions,
so that `FURNSH` of a name reads from memory. Two ways make a region:

- **Loading** takes a block the size of the file from the shared
  allocator, aligned to a page, and reads the file into it once, unbuffered
  where the system allows it, so that a large file is not held in the
  system's file cache as well while it is read. After that nothing is read
  from disk and no page fault comes, which suits a program that uses most
  of a file and wants every call to take the same time.
- **Mapping** maps the file into memory, reads nothing at first and then
  only the pages used, and shares them with other processes; it is
  *Memory-mapped kernels*, below.

SPICE still copies each record it reads into its own buffers; what memory
saves is the system call for each one. A pinned array holds at most about
2^31 elements, so under 16 GB of `long`; native memory and a mapping have
no such limit. A DAF reaches its own limit near the same size, since its
addresses count 8-byte words in a 32-bit integer. JPL's DE441 is 3.1 GB.

**A file written in memory is to grow past 2 GB.** `MemoryFileProvider`,
in `DESIGN.md`, keeps each file in one array of bytes, so under 2 GB. Kept
in an array of `long` a file reaches 16 GB, which is the limit a DAF has
anyway, and every record starts on an 8-byte boundary; blocks from the
allocator, chained, have no limit at all. To be chosen when a file that
size is first wanted, since the tests write nothing near it.

How a name is given to one kind of file or another is under *Input and
output is to be looked at again as a whole*.

**A program that does recurse** has units the report finds; those keep
their buffers on the stack in both modes.

**Guarding the layout.** A mistake in it would let one routine overwrite
another's buffers with nothing to see. The toolkit's tests and the
bit-for-bit comparison are to run in both modes, and a debug build of the
runtime can put guard bytes between regions and check them at the
boundary.

Two designs were set aside on the way. Limits and budgets that split a
method's buffers between the stack and the heap at run time cost a cut
to compute and a choice per buffer, for a gain over two modes that no one
had asked for. Taking each method's buffers from the heap on every call
and giving them back on return, which is what f2rust does for its large
arrays, costs an allocation per call and a release on every way out.

**What the report showed.** `DESIGN.md`, *A translation
says how much stack each procedure can take*, has how it is made. Over
N0067, with today's fixed limit of 4,096 bytes, and counting only what
the translation takes itself:

- The main library's deepest procedures are the geometry finders,
  `GFPOSC`, `GFSNTC` and `GFSUBC` at 76,533 bytes and 53 calls deep; the
  everyday entry points take 55 to 70 KB (`SPKEZR` 61,742, `PXFORM`
  54,090, `SINCPT` 66,887), and time conversion about 6 KB (`STR2ET`
  5,948).
- Of its 2,041 procedures, 41 can take more than 64 KB and 166 more than
  16 KB; 755 take anything of their own, and the most any takes of its own
  is `EKFIND`'s 17,600 bytes, in locals each under the limit.
- What makes the deepest chains deep is the toolkit's dynamic frames,
  whose routines come in copies, `ZZSPKEZ0` and `ZZSPKEZ1` among them,
  that stand in for recursion; the frame routines take some 6 KB each,
  and the error-message path at the bottom of a chain about 4 KB.
- No procedure is reached again beneath itself, no variable lies off its
  alignment, and no call passes a 4-byte number where an 8-byte one is
  taken.

The just-in-time compiler's frames come on top, for chains of about 50
calls. So the stack mode fits the default stack of a .NET thread on
desktop and server systems, a megabyte or more, with room to spare; the
heap mode is for a thread that was given a small one, or a caller that is
deep already, and leaves the thread's stack only the compiler's frames.
Whether the heap mode is slower is for the benchmark project to say. It
reuses its bytes as the stack does and fits in the second level of cache,
and it clears nothing on entry, where `stackalloc` clears every buffer
today: `EKFIND` clears 17,600 bytes on each call.

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
- **`MOD` of real values.** It is C#'s `%`, which the runtime does by a
  call to `fmod`, and SPICE reduces angles and times with it. `fmod` is
  exact, so only an exact form may take its place, or the results move;
  the question is whether one is faster.
- **Searching and comparing text.** `INDEX` and the comparisons, `LGE`
  and the three like it, are loops over bytes in the runtime.
  `Span.IndexOf` and `SequenceCompareTo` are vectorized by .NET and give
  the same answers; whether that pays on values as short as FORTRAN's
  mostly are is the question, beside *Short copies* above.
- **The toolkit's small vector and matrix routines.** `VDOT`, `MXV`,
  `MXM`, `VCRSS` and their like are the most called arithmetic of the
  toolkit, and the just-in-time compiler vectorizes no loop by itself.
  Each could be a unit left out and written by hand with `Vector128` and
  `Vector256`, provided its additions keep their order, so that the
  results stay bit for bit; for three elements the gain may be small, and
  a product of 3-by-3 matrices is the likelier case.

The functions of the mathematical library are not among these. `SIN`,
`EXP`, `ATAN2` and a real exponent call the platform's C runtime, as the
reference compiler does, which is why the two agree bit for bit; a
faster form of our own, or `Math.FusedMultiplyAdd` where the reference
compiler multiplies and adds apart, would give that up. A form of our own
that gives the same results on every platform would be a variance to
offer, not a measurement to make.

## Back ends beyond C# source

The emitter writes C# source text directly from the analysed program, with
no tree of C# objects in between: the output's shape is fully ours, the
original comments have to pass through, and there are over a million lines
to write. That is the back end the translated library is built with, and the
only one that runs everywhere, since it needs nothing at run time. The tree
that is to come between, under *A tree between the analysis and the
writers*, is the translator's own and keeps all three; each back end below
then starts from it.

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

## A variable off its alignment is read as unaligned

`docs/rules/portability.md`, *Alignment*, has why: 32-bit ARM needs a
floating-point value, and a 64-bit integer loaded as a pair of words, at a
multiple of 4. Every type the translator handles is 4 or 8 bytes, so only
an `EQUIVALENCE` that shares a `CHARACTER` variable with a number can put
one elsewhere, and its offsets are all known when translating. The
toolkit has none such.

- **Each such variable is read and written through `Unsafe.ReadUnaligned`
  and `Unsafe.WriteUnaligned`**, at that place alone, and everything else is
  written as it is now. There is no setting for it: on x64 and ARM64 the
  just-in-time compiler is expected to turn the two into the plain load and
  store, so the code runs as fast everywhere and correctly on 32-bit ARM
  besides. The benchmark project is to confirm that from the code it
  generates.
- **Passing one as an argument copies it.** The procedure called reads its
  argument with plain loads, so the caller passes an aligned copy and
  copies it back after the call when the argument can be written.

Not built until a program needs it. A type of fewer than 4 bytes, should
`INTEGER*2`, `LOGICAL*1` or `BYTE` ever be read, would be a second source.

## .NET 10 first, and .NET 11 beside it

The libraries target .NET 10, the release supported for longest, until
November 2028, and what is built for .NET 10 runs on .NET 11 as well.
Where .NET 11 has an API worth using, its new helpers of the vector types
among them, a project targets both, `net10.0;net11.0`, so that its package
holds a build for each, and the code that uses the API is under `#if
NET11_0_OR_GREATER`, the symbol the SDK defines. Whether the processor has
an instruction set is asked when the code runs, `Avx2.IsSupported` or
`Vector256.IsHardwareAccelerated`, never by a symbol. Translated code is the
same for both; only hand-written code carries the condition. Building both
needs the .NET 11 SDK.

.NET 11 does not start on an x64 processor below x86-64-v2, which adds
SSE4.2 and POPCNT among others; Windows 11 asks the same, and so does every
processor Windows 10 supports. That is no reason here to stay on .NET 10,
which is chosen for its support, not for older hardware.
