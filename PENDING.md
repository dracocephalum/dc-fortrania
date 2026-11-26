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
They may be one project or two: two are translated apart, the first's
summary kept beside its project file and the translation of the second
reading it, as `DESIGN.md` has under *A project is translated apart by
the summaries of those it calls into*; *What is translated is set out in
settings files, and the project is its own* says how each is described.

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

## What the command does not do yet

`DESIGN.md`, *The command is `fortrania`, and its first word what to
do*, has what is built. Left to do:

- **Whether one tool package can give more than one command**, should a
  short name of its own, `f2cs`, ever be wanted beside `fortrania`.
- **A program that stands alone**, for a machine without the .NET tool
  installed, released under the name of its target.

## What a translation's settings do not do yet

`DESIGN.md`, *What is translated is set out in settings files, and the
project is its own*, has what is built: the settings, their rules, the
project they make, and the translator that translates one project or
several from them. Left to do:

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
- **How long a run of an array is done at once.** A loop that only
  copies or fills an array is given to the runtime from sixteen
  elements on, a number measured on one machine of 64 bits and written
  down twice, in the translator for a length it knows and in the
  runtime for the rest; `DESIGN.md` has it under *A loop that copies or
  fills an array is done at once where it is long*. A setting is to
  give another, for a platform where the two ways cross elsewhere, a
  processor of 32 bits say. The translator would then write the number
  into each test it emits and not leave it to the runtime's constant,
  so that a translation carries what it was tuned to. Not built until a
  platform is measured that wants another.
- **Paths that go deeper than one folder.** The last part of a path may
  have `*` and `?`; `**` for folders at any depth is not read.
- **The rest of what a translation is told**, under *What the translator
  is told*: the convention, the diagnostics switched off, the runtime as
  source, the placement of saved state.

## Translating as a step of the build

Built: `DESIGN.md`, *A project translates its FORTRAN as it builds*, has
the package. Left to do:

- **The translated library's repository taking it**, with its settings
  files beside the project file of each project, and each project's
  summary committed beside it.
- **Two settings files that name one download differently are refused**:
  the same variable naming the same folder with another address or hash.
  A run sees both and does not check yet; a build sees only the files of
  one project, so across projects this is a check of the repository's.
- **The runtime as source** can come the same way: the package adds the
  runtime's files to the compilation, as *The runtime can be handed out as
  source* describes.
- **An editor's build of a project's design** runs no translation, and a
  translation that lies outside the project's folder is not seen by the
  editor until a build has written its inventory.
- **Sources can be fetched**, as `DESIGN.md` describes under *A variable
  can say where to download the folder it names*. Still to come: the
  archive format of the toolkit's own packages, `.tar.Z`, which .NET does
  not read.

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
5. **The command**, `fortrania translate csharp`, published as a .NET
   tool. Built; `DESIGN.md` has it under *The command is `fortrania`,
   and its first word what to do*.
6. **Which runtime a translation runs on.** Emitted code calls the
   runtime's API, so a package's user has to know which runtime goes with
   which translator. The rule already decided for a scaffolded library,
   the runtime of the translator's own version, is to be the rule for the
   packages: one version number for the two.

**The command and the build package are taken ahead of the cleanup
round.** The optimization round's three rewrites are done, and the
translated library's repository waits for a translation it can commit
and build on a server, which needs step 5, which is built, and then
*Translating as a step of the build*. The cleanup round follows them, and still comes
before anything is released.

**Development packages come before all of that.** The translated library
is built in a repository of its own and takes the translator and the
runtime as packages, so packages of a development version are made from
the first pipeline on, long before a release. How they are numbered and
where they go is in `DESIGN.md`, *A package's version is in the code, and
the build says what kind it is*.

**It is the first repository with pipelines.** Those that build every pull
request and publish each merge are built; `DESIGN.md` has them under *A
package's version is in the code, and the build says what kind it is*.
Left:

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

The emitter once turned the analysed program into C# text in one step, so
that what it decided and what it wrote were mixed. The optimization round,
step 3 of *Before Fortrania is published*, put a tree in between, and is
built on it:

    parse → analyse → lower to the program tree → passes → a writer

**The program tree is the program after lowering, with nothing of
FORTRAN's left in it.** Blocks, assignments, calls, loops and
conditionals; storage, as locals, saved state and common blocks; typed
memory, as addresses and offsets; calls into the runtime, for text and
for input and output; and comments, as nodes of their own, so that they
pass through every stage. It is the translator's own, and so its shape is
ours, which is why the emitter writes text and not the C# compiler's
tree; see *Back ends beyond C# source*.

**A pass takes a tree and gives one.** Four exist: the layout of working
buffers, the character constants' block, and two of the rewrites under
*More that the translator can write more simply*; `DESIGN.md` has each.
Still to become passes:

- joining constants, which the emitter does as it builds the tree;
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

**How far it is.** The tree is built, and `DESIGN.md` has it under *An
expression is a tree before it is text* and *A program unit is a tree
before it is a file*: the emitter makes a tree of each program unit, and
the C# writer gives the same files, to the byte, that the emitter gave
before it, over the toolkit's 2,215, in no more time. What is left of
this section's plan:

- **Where a blank line goes among comments** is decided as the comments
  are added, from what stands before them, and is in the tree as a node.
  A pass that adds or takes away a statement beside a comment would
  leave the spacing as it was; the rule is to move to the writer when a
  pass first does that.
- **Types and parameters are text in the tree**: `double*`, `int n`, as
  C# spells them. A writer for another language needs them as nodes.
- **The part of a class that belongs to no program unit**, how an
  instance is made over its context, is still written as text.
- **The settings name no pass of a user's own**, and the tree is not
  public. Two settings switch a pass of the translator's off,
  `inlineTextConstants` and `plainArrayLoops`.

Two things the first piece settled:

- **The translator's own arithmetic is a node of its own.** An address, a
  length or a position is a sum or a product of whole numbers that the
  translator made up, where grouping does not matter and none is written;
  the program's own operators keep theirs. A pass may fold the first kind
  freely and the second not at all.
- **Parentheses that must be written are in the tree**, the program's own
  among them, since they fix how a real expression is evaluated. All
  others are the writer's, from how strongly each node binds.

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

- **A character value compared with a constant.** Done for equality;
  `DESIGN.md` has it under *Equality with a constant is tested as
  equality*. An ordering against a constant, `NAME .LT. 'K'`, still goes
  through the runtime's comparison, and is rare.
- **A whole array copied or cleared by a `DO` loop.** Done for the
  plainest loop; `DESIGN.md` has it under *A loop that copies or fills
  an array is done at once where it is long*. A loop over part of an
  array, one with a step, and one that copies character elements are
  left as they are.
- **A whole power of an integer known when translating.** A real raised
  to 2, 3 or 4 is done, in the runtime and not by a pass; `DESIGN.md`
  has it under *An integer power multiplies, in the reference compiler's
  order*. An integer raised to such a power still goes through the
  runtime's loop, and is rare in the reference sources.

The toolkit's translation is the measure of which are worth it: each is
counted there before it is built, as the joined constants were.

## Working buffers: what is not built

`DESIGN.md`, *Where a method keeps its working buffers is the context's to
say*, has what is built: a context chooses the stack or its arena, each
method with arrays or character variables takes them as one piece from
one or the other by a test on entry, each project has a region laid out
from its own calls, and a call through a procedure argument runs in a
copy of the arena of its own. Left to do:

- **A translation fixed to one mode**, stack only or arena only, a setting
  of the translator's that writes each procedure as one method with no
  test, no entry and no opener, and registers its project for that mode.
  The context already refuses a library translated for the other mode
  than it asks for; nothing yet makes such a library, the translator
  writing `BufferPlacement.Default` always. Since cores, a call inside a
  project makes one test, so what fixing saves is that test, under a
  nanosecond on a method that does nothing and nothing measured on one that
  works. Fixed to the stack, the gain is mostly code that is simpler to
  read and to set beside the FORTRAN: no region, the buffers taken one by
  one as before the pass. Fixed to the arena, a method would also have no
  `stackalloc`, and so no frame pointer or stack cookie, which measured
  fastest of all on a method that does nothing. The core against a method
  with no test is to be measured again before either is built.
- **Arrays kept in a unit's block only for being too large for the
  stack**, over 4,096 bytes, are still there and not on the working side,
  so a unit's block holds more than its saved state; and a place for
  joining character values that would be longer is refused. On the
  working side both would take a place like any other buffer.
- **What a statement function takes** stays on the stack in both modes, a
  few bytes for a value on its way.
- **A procedure the host passes** reaches translated code through no
  forwarder, so the managed layer is to open and close a copy of the
  arena for it, `Push` and `Pop`, as a forwarder does.
- **The page that cannot be touched at each end of a copy of the arena**, and under
  the setting that clears or fills every local a known value after each
  method's buffers, checked when the method returns; `TODO.md` has both.
- **Guard bytes between regions** in a debug build of the runtime,
  checked as a copy is closed, against a mistake in the layout that would
  let one routine overwrite another's buffers with nothing to see.
- **The comparison to the last digit with the reference compiler's
  build**, which was not run again in the arena's mode.
- **A routine called again while it runs throws**, below.

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
system, as `stackalloc` does under the class's `[SkipLocalsInit]`.

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

## Measurements still to make

[`docs/rules/performance.md`](docs/rules/performance.md) asks that a change
made for speed come with a benchmark, and `tools/benchmarks` is where one
is written. These are the questions it is to answer next, each a change
that is not to be made before it does. A question that is answered leaves
this list, for `DESIGN.md` where the answer shaped the code.

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

## Messages in other languages

Not designed yet; what follows is the idea and what it has to answer. A
program's messages are character constants in its source. A translation
could give each one a number, write them all to a file beside the code,
and have the program ask the runtime for the text of that number in the
context's language, with the constant itself as the answer where there is
none. Languages are then made after the translation and apart from it,
as assemblies or files of their own.

**What makes it possible.** A character value is an address and a length
already, so a message of another length is passed as easily as the
constant; and constants are joined when translating, so a message
written across continuation lines is one text to translate.

**What it has to answer.**

- **Which constants are messages.** Most are not: a keyword compared
  with, a name looked up, a format, a file name. To translate one of
  those is to break the program. So the settings are to say where
  messages are, the argument of a procedure that takes one, the first of
  the reference sources' `SETMSG` say, and nothing is taken for a
  message that the settings do not name.
- **A number that lasts.** One that counts constants in order changes
  when a source does, and every translation made so far with it. A
  number worked out from the text, and from where it is used if the same
  text is to be told apart, stays with its message.
- **No cost where it is not used.** A translation without the setting
  writes the constant as now. With it, a context that has no language
  gives the constant back after one test; one that has a language looks
  the number up in a table, which is to be an index and not a search.
- **Bytes, not characters.** The runtime's text is UTF-8 bytes, and a
  FORTRAN program cuts, pads and wraps by the byte. A message in another
  script can be cut inside a character or wrapped at the wrong width,
  and a longer one is cut where it is stored in a variable of fixed
  length.
- **The language is the context's**, as its files and its console are,
  and not the thread's.

**Who decides what is a message, and when.** The decision is made while
the translator still has the FORTRAN expression and its names, and the
program tree carries the outcome: a node of its own for a message, with
its number and the constant, which a writer turns into the call that
asks. By the time an expression is tree, a constant is an address and a
length and nothing says what it was for.

- **A classifier first.** One question, put for each character constant
  with a small account of its use: its text, its program unit, and what
  it is there as, the argument of which procedure in which position, the
  value given to which variable, a format, one side of a comparison. The
  answer is whether it is a message. The one that comes with the
  translator goes by rules in the settings; another can be given in its
  place, and may know what no rule says.
- **Passes of a user's own later**, when the tree is public, as *A tree
  between the analysis and the writers* has it. A classifier shows a
  caller one small record; a pass shows the whole tree, and every node
  shown is a promise.
- **A message that goes by way of a variable** is not seen where it is
  passed: `MSG = '...'` and, lines later, `CALL SETMSG ( MSG )`. To be
  counted in the reference sources before anything is built for it. A
  rule for a constant assigned to a variable that the same unit then
  passes as a message may be enough, without following values about.
- **Every constant is listed**, in the file the numbers go to, with its
  use and whether it was taken for a message, so that what the rules
  missed can be found by reading.

**How a message is reached.** A constant that is not a message is at a
place in its library's text block, fixed when the program is translated.
A message cannot be: its bytes and its length differ by language. So a
message is reached by its number in a table of addresses and lengths
that the context fills for its language, the constants' own where it
has none. That is one load more than a constant costs, and only
messages pay it.

**A first trial is to take little.** Only what is passed whole to a
procedure that shows it or stores it as one message, an error message
above all, and nothing a program takes apart. Where a procedure wraps
what it shows, it wraps by the byte and at a width in bytes; for a
language whose characters are several bytes, or whose words are not
parted by blanks, the runtime would have to offer the wrapping, by
characters and at the boundaries of words, and the program be made to
use it. That is a change to a program's own routines and not to its
constants, and is not part of a first trial.

## Back ends beyond C# source

The emitter writes C# source text from a tree of its own, with no tree of
C# objects in between: the output's shape is fully ours, the
original comments have to pass through, and there are over a million lines
to write. That is the back end the translated library is built with, and the
only one that runs everywhere, since it needs nothing at run time. The tree
between, under *A tree between the analysis and the writers*, is the
translator's own and keeps all three; each back end below starts from it.

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
code. `docs/rules/portability.md`, *Where a program differs by platform*,
has the kinds of difference and how each is answered.

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
