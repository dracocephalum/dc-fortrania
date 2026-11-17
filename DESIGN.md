# Design

Why the repository is shaped the way it is. Type comments say what the code
does; this page holds the decisions a reader could not infer from it.

## Translated code is committed, and it is an intermediate product

The library is produced in three steps:

    FORTRAN 77 sources  --translator-->  C# sources  --compiler-->  NuGet package
    (never in this repository)           (committed)                (what is published)

The middle step is committed on purpose. A change to the translator then
shows up in review as a diff in exactly the routines it affects, a build
needs neither the original sources nor a translator run, and anyone can read
what the published assembly was compiled from.

It is still machine output, and it is treated as such:

- It is never edited by hand. A wrong translation is fixed in the translator
  and the output regenerated.
- It is not the product. The package compiled from it is, together with the
  hand-written API over it.
- It lives in components of its own, apart from hand-written code, so that
  its licence and its relaxed build settings cover nothing else.

The rule that follows from this is
[`docs/rules/translated-code.md`](docs/rules/translated-code.md).

## Two licences, split by component

Hand-written work is under the Apache License 2.0: the translator, the
runtime library, the public API, tests and documents. Translated code is not.
It derives from the SPICE Toolkit, which has no open-source licence and is
governed by the rules its owner publishes, so it stays under those rules.

The split is by folder and never by file within a project. A component that
holds translated code has its own `LICENSE.md` and holds nothing a person
wrote; every other component is covered by the root [`LICENSE`](LICENSE) and
[`NOTICE`](NOTICE). A reader can therefore tell which terms apply from where
a file is, and each translated file repeats it in its header for the reader
who arrives at the file alone.

## Byte order is the format's concern, not ours

Binary kernels record their own byte order and the reference toolkit converts
when it reads one of the other order. That logic is translated with the rest,
and the .NET runtime runs on both orders, so the library relies on the two of
them rather than adding a layer of its own. What hand-written code owes is
narrower: it never assumes an order —
[`docs/rules/portability.md`](docs/rules/portability.md).

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
