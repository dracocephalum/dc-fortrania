# Portability

Applies when reading or writing binary data, using hardware intrinsics, or
touching anything that differs by operating system, processor or byte order.

## The rule

**One build of the library runs everywhere .NET does**, little-endian and
big-endian, x64 and ARM64, Windows, Linux and macOS. Nothing is decided at
translation time that the reference toolkit decides per platform; it is
decided at run time or made not to matter.

## Byte order

**Hand-written code never assumes a byte order.** That is the whole rule;
`DESIGN.md`, *Byte order is the format's concern, not ours*, says why nothing
more is needed.

- Where hand-written code turns bytes into a wider type or back — the
  runtime's unformatted file input and output is the main place — it goes
  through `System.Buffers.Binary.BinaryPrimitives` or checks
  `BitConverter.IsLittleEndian`. It never reinterprets a span as a wider type
  on the assumption that the machine is little-endian.
- A new file is written in the byte order of the machine writing it, as the
  reference toolkit does.

## Where the reference toolkit differs by platform

The FORTRAN sources come in one variant per platform and compiler. Between
the Linux and Windows variants of the library, 23 of 1,415 routines differ,
and every difference is one of these:

| Difference | Here |
|---|---|
| The platform, compiler and text line terminator the library reports about itself | reported from the running machine |
| Whether text files end lines with LF or CR-LF | read either; write the running machine's |
| Whether a character's code can be negative | never; a character is an unsigned byte |
| Limits on file name length and on files open at once | the larger value |
| How the process exits, and how a line is written to standard output | the runtime library's concern, not the translated code's |

The translator works from one variant. A routine that differs by platform is
either translated with the difference resolved as the table says, or replaced
by a hand-written implementation; it is never translated as it stands, which
would freeze one platform's answer into the library.

## Intrinsics

Every use of a hardware intrinsic has a path for hardware without it. Prefer
the cross-platform `Vector128<T>`, `Vector256<T>` and `Vector512<T>` API, which
serves x64 and ARM64 from one implementation, over an instruction set named
directly; name one directly only where a measurement shows the difference. A
vector path that depends on element order within a lane is checked on both
byte orders or guarded so that big-endian machines take the scalar path.

## What is not yet verified

No big-endian machine is available to the build, so big-endian support is by
construction and not by test. A test that reads a kernel of the non-native
order exercises the conversions and is welcome, but optional. Wherever
compatibility is claimed, say that it has not been run on big-endian
hardware.
