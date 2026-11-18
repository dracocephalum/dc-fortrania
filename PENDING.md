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
main one, is a class of its own that is given the instance it calls into,
so that the two share that instance's state. The summaries of the first are
written to a file beside its code, and the translation of the second reads
them.

A main program is a method like any other. When an executable is asked for,
a `Main` is emitted that hands the command line to the runtime, calls that
method and returns the exit code. A library has none.

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

## One caller at a time

An instance is not safe for two callers at once, since its routines share
state. `ISpiceCore` detects it: each call marks the instance as in use with
an interlocked exchange and fails if it already was, at a cost of a few
nanoseconds, with an option to turn the check off. It is not tied to a
thread, because code that awaits may resume on another thread and still be
one caller. The translated layer below has no such check.

An instance also holds the kernels loaded into it, so creating one for each
request of a server means loading them again each time. The fitting pattern
there is a factory that keeps a pool of instances with their kernels loaded
and lends one to a caller at a time.

The factory is told once which kernels an instance is to hold. What it
hands out is a lease: the caller uses the instance through it and disposes
it, which gives the instance back, so a lease can be registered with a
dependency-injection container as a transient or a per-request service and
ends with the request. Before an instance is lent again its error state is
reset. Open questions for when it is built: what happens to an instance
whose borrower loaded or unloaded kernels or changed the kernel pool,
discarded or restored; how many instances the pool keeps and whether a
caller waits or gets a new one when all are lent; and whether an instance
with its kernels loaded can be copied, block for block, more cheaply than a
new one can load them.

The factory belongs to the managed layer and depends on no container. A
small package of its own may later register it with the usual one.

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

## Files are opened through a provider, and a stream is a file

FORTRAN names a file by a string and refers to it afterwards by a unit
number, and the translated code does the same. What that string resolves to
is the runtime's decision, made by a file provider the library instance is
given:

- By default a name is a path on disk, opened for random access by the
  operating system's own handle, which is the fast path.
- A name can instead be registered against any seekable `Stream`, a
  `MemoryStream` or one of the caller's own, and from then on it behaves as
  a file of that name: it can be opened, inquired about and read at any
  record, and written if the stream allows it. `ISpiceCore` offers this
  directly, as an overload that takes the stream.
- An application can replace the provider altogether, for platforms where
  bundled data is not reachable by path.

Nothing in the translator or the translated code knows which of these is in
use; every input and output statement becomes a call into the runtime, and
only the runtime sees what is behind a unit.

A registered stream belongs to one library instance while it is registered.
A stream has a single position, so it is not shared between instances or
read by the caller in the meantime.

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

## An ENTRY becomes a method of its own

A subroutine or function with `ENTRY` statements has several ways in, and
the reference sources use them as one routine holding saved state with an
operation per entry. Each entry, and the enclosing routine itself, is
emitted as a separate method that takes only its own dummy arguments:

- `SAVE` variables are shared between them, in the routine's state in the
  instance's native memory.
- Other local variables belong to each method; FORTRAN does not keep them
  between calls.
- In a function, each method returns its own result.

This is right only when the entries are disjoint: no entry can be reached
by control running on from the code before it, and no jump crosses from one
entry's code into another's. Analysis decides that for each routine, as
`EntryLayout`, which also gives each entry the statements it reaches. In the
reference sources it holds everywhere: of 593 entries in 144 routines, every
one directly follows a `RETURN`, none is inside a block, and the sources
have no `GO TO`.

A routine that is not disjoint is emitted the way `f2c` does it: one body
with every entry's arguments and a selector that says where to start, and a
thin method for each entry that calls it. That is always correct, and it is
the fallback and not the default because every call then passes every
entry's arguments. It is part of *FORTRAN 77 that Fortrania does not handle
yet*.

Running on from one entry into the next is not the same as the first
calling the second, which is why there is no third form. In one invocation a
local variable set before the boundary is still set after it; the result
variables of a function's entries share storage; and a jump can cross the
boundary in either direction. A call at the end of the first method has none
of these. It could stand in only where analysis shows none of them matters,
and that is to be added only if real code and a benchmark ask for it.

FORTRAN forbids an entry's code to use a dummy argument of another entry,
since nothing was passed for it, and a method that takes only its own
arguments has no such name. The reference sources do it all the same, in two
entries of one routine, each in an error branch its own comment says is
never reached. So it cannot be refused. `EntryLayout` lists such arguments
for each entry, and the method declares each as a local variable of the
argument's type holding zero or blanks: an array has one element and text of
assumed length one character. The statement then does something harmless,
where the original would follow an address nobody set.

## A C interface for callers outside .NET

The translated library can be compiled ahead of time into an ordinary native
library that exports C functions, one per routine, each a plain forward to
`ISpiceLib` taking a handle to a library instance as its first argument. It
needs no .NET installation on the machine that uses it. The pointer-based
signatures of the translated layer exist partly so that this facade is
mechanical; the translator generates it from the same routine list.

Exports cannot be instance members, so the facade is a static class in a
project of its own, the one published as a native library.

Two shapes are wanted, in this order:

- Instance-based exports, with the handle. This is the one that keeps what
  the port adds: several independent instances in one process.
- Exports with the reference C toolkit's own names and argument lists, bound
  to one default instance for the process, so that an existing program can
  change libraries without changing source. The conversions those functions
  do, C strings, row-major matrices, zero-based indices, are the same ones
  `ISpiceCore` needs, and both are generated from the routine's argument
  list and its documented inputs and outputs, with a short list of routines
  written by hand. The reference C wrappers are not translated.

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
- A routine whose entries are not disjoint, which needs the single body with
  a selector described under *An ENTRY becomes a method of its own*.

## Procedures taken from the compiler's library

The reference sources call six procedures that no FORTRAN 77 standard defines
and no file of theirs supplies: `IARGC` and `GETARG` for the command line,
`GETENV`, `SYSTEM`, `EXIT`, and `DATE_AND_TIME`. Each is used in one or two
routines written for exactly that purpose. The runtime is to supply them
under those names, so that the translator treats them as it treats any other
external procedure and knows nothing about them. `SYSTEM` starts another
program, which not every platform allows; there it reports failure.

## Settings for what differs by platform

Where the reference toolkit differs by platform in a value and not in logic,
the longest file name and the most files open at once, the value is to come
from a settings class the runtime is given, with the larger of the
reference's values as the default, in place of a constant in translated
code. `docs/rules/portability.md` has the table of differences.
