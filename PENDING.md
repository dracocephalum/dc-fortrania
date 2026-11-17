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
entry's code into another's. Analysis decides that for each routine. In the
reference sources it holds everywhere: of 550 entries in 133 routines, every
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

Not yet checked for the reference sources: that no entry's code uses a dummy
argument of another entry. FORTRAN forbids it; analysis is to confirm it.

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
- A name starting with `E` or `D` directly after a length, as in
  `REAL*8 E1`, which the lexer reads as a number.
- Tab-form source.
- A routine whose entries are not disjoint, which needs the single body with
  a selector described under *An ENTRY becomes a method of its own*.

## Settings for what differs by platform

Where the reference toolkit differs by platform in a value and not in logic,
the longest file name and the most files open at once, the value is to come
from a settings class the runtime is given, with the larger of the
reference's values as the default, in place of a constant in translated
code. `docs/rules/portability.md` has the table of differences.
