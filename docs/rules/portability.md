# Portability

Applies when reading or writing binary data, using hardware intrinsics, or
touching anything that differs by operating system, processor or byte order.

## The rule

**One build of the runtime, and one translation of a program, runs
everywhere .NET does**, little-endian and big-endian, x64 and ARM64,
Windows, Linux and macOS. Nothing is decided when a program is translated
that its FORTRAN sources decide per platform; it is decided when the
program runs or made not to matter.

## Byte order

**Hand-written code never assumes a byte order.** That is the whole rule.
Nothing more is needed because a file format that records its own byte
order, as the binary files of the reference sources do, is converted by the
code that reads it, and that code is translated with the rest.

- Where hand-written code turns bytes into a wider type or back — the
  runtime's unformatted file input and output is the main place — it goes
  through `System.Buffers.Binary.BinaryPrimitives` or checks
  `BitConverter.IsLittleEndian`. It never reinterprets a span as a wider type
  on the assumption that the machine is little-endian.
- A new file is written in the byte order of the machine writing it, as a
  program built by the reference compiler writes it.

## Where a program differs by platform

FORTRAN sources often come in a variant for each platform and compiler, as
the reference sources do, where a few routines in a hundred differ. The
translator works from one copy, the base, and takes from another platform's
only the units a source's settings name. What differs is of three kinds,
and each has one answer:

| What differs | Here |
|---|---|
| What the compiler's library does for the program: how a line of text ends, how the process exits, how a line reaches the screen | the runtime's, decided when the program runs: it reads either line ending and writes the line ending of the system its context is on, the machine's unless the context is told another |
| What the language leaves to the compiler: whether a character's code can be negative | one answer everywhere, listed in `VARIANCES.md`: a character is an unsigned byte |
| What the program says of the platform itself: a name it reports, a limit it sets | a variant of the unit for each platform, which the library chooses between when it is made, from its context; `DESIGN.md`, *A source may take a routine from another platform's copy*. A step after the translation that rewrites the unit is the other way, for an answer no platform's copy gives |

A unit that differs by platform is never translated as it stands and left
so: that would freeze one platform's answer into a translation that runs on
all of them.

## Alignment

**What the translator places is aligned to its type, except where the
source fixes the place.** The instance's block comes from the allocator,
aligned for any type; the emitter puts each variable kept there at a
multiple of its element size, up to 8; and the stack is the just-in-time
compiler's, aligned likewise. The layout is worked out when translating,
so it is the same on a 32-bit processor as on a 64-bit one. There are two
exceptions:

- `EQUIVALENCE` fixes in bytes where each of the variables it joins
  begins, and the language lets one begin where its type would not be
  aligned.
- A call may pass a number of one type where the procedure takes another,
  an `INTEGER` array element for a `DOUBLE PRECISION` argument; the
  procedure then reads an 8-byte value at a multiple of 4.

**x64 and ARM64 read such a place as it stands**, any type from any
address, at most a little slower where it crosses a cache line, so the
translation reads and writes it like any other.

**32-bit ARM may not.** .NET runs on it under Linux, and there a
floating-point load or store, or a 64-bit integer loaded as a pair of
words, needs an address that is a multiple of 4; one that is not can stop
the program with a bus error. This is from what is known of the
processor, and has not been run: no such machine is available to the
build. Every type the translator handles is 4 or 8 bytes, so a call that
passes one type for another stays on a multiple of 4; only an
`EQUIVALENCE` that shares a `CHARACTER` variable with a number, which the
standard forbids and the translator accepts, can go below it.

**32-bit ARM is not a target of the published packages.** *The rule*
above does not name it, nothing is tested on it, and no package is made
for it; whoever needs it translates their program for it themselves.

**The translator is still to write such a place so that it runs there.**
`PENDING.md`, *A variable off its alignment is read as unaligned*, has the
plan; it is not built. The reference sources do not need it: `EQUIVALENCE`
puts no variable of theirs off its alignment, and a translation's report of
such places, `TranslationResult.UnalignedPlaces`, says so each time.

## Intrinsics

Every use of a hardware intrinsic has a path for hardware without it. Prefer
the cross-platform `Vector128<T>`, `Vector256<T>` and `Vector512<T>` API, which
serves x64 and ARM64 from one implementation, over an instruction set named
directly; name one directly only where a measurement shows the difference. A
vector path that depends on element order within a lane is checked on both
byte orders or guarded so that big-endian machines take the scalar path.

## What is not yet verified

No big-endian machine is available to the build, so big-endian support is by
construction and not by test. A test that reads a file of the non-native
order exercises the conversions and is welcome, but optional. Wherever
compatibility is claimed, say that it has not been run on big-endian
hardware. The same holds for 32-bit ARM, under *Alignment* above.
