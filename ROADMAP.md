# Roadmap

Where Fortrania is going. The stages are in the order they are to be
reached, each named for what it ends with. Each stage
says what it is for, what it needs first and roughly what it takes, and
names where the detail is. It is the least exact of the root documents on
purpose: a stage far off is a paragraph, and is worked out when it comes
near.

| For | Read |
|---|---|
| what a stage's parts are and how each is to be built | [`PENDING.md`](PENDING.md), the sections a stage names |
| what is owed or undecided now | [`TODO.md`](TODO.md) |
| what is built, and why it is shaped as it is | [`DESIGN.md`](DESIGN.md) |

A stage is done when its parts are built and their reasoning is in
`DESIGN.md`; it is then cut down here to a line under *Done*. A stage that
changes is changed here first, and `PENDING.md` follows.

## Done

- **The reference sources translate, and agree with the reference
  compiler's build.** NASA's SPICE Toolkit, its main library, its support
  library and its test program: every test family but one whose failing
  cases compare run times, and the values compared to the last bit. The
  check is the translated library's and lives in its repository.
- **A round for speed.** On the reference sources' test program the
  translation takes less processor time than the reference compiler's
  build at `-O2`. `DESIGN.md` has each change with its measurement.
- **The command and the build package**, `fortrania translate csharp` and
  translating as a step of a build, with development packages from every
  merge.

## A first release

What stands between the code as it is and packages on nuget.org. In this
order, since each changes what the next looks at:

1. **Input and output looked at again as a whole.** It changes the calls
   a translation makes to the runtime, so it comes before any translation
   is committed anywhere. `PENDING.md`, *Input and output is to be looked
   at again as a whole* and *Input and output still to build*.
2. **A cleanup round**, against the C# rules, so that what it tidies is
   the code that stays. What is `public` is decided here type by type,
   the translation's settings above all, with the translator's tunables
   in one group; `TODO.md` has that entry. With it, **a file of what a run
   reported**, which a build says again: `PENDING.md`, *What a
   translation reports is written to a file, and later has codes*.
3. **A translated library says what made it:** the build package tags
   the package of a project it translates, and a scaffold writes that
   package's page. `PENDING.md`, *A library translated with Fortrania
   says so*.
4. **The release pipeline**, which publishes the packages and attaches
   the command, as a program that stands alone, to the release on the
   repository's host; and the check that a released version is not built
   again. `TODO.md` has each, and `PENDING.md`, *What the command does
   not do yet*, the program.

It needs: the translated library's repository to have committed a
translation made with the packages, which is the proof that they serve.
`PENDING.md`, *Before Fortrania is published*, has what is settled of the
pipelines.

## All of FORTRAN 77

Fortrania translates the FORTRAN 77 that its reference sources use, and
reports the rest as unsupported. This stage makes it a translator for the
language, which is what the libraries written in it need: most of the
numerical libraries of that time lean on the first two items below, which
the reference sources happen never to use.

- **`GO TO` in every form, statement labels, a `DO` that ends at a label,
  the arithmetic `IF`, and alternate returns.** The largest part, and
  less than it looks: what FORTRAN 77 allows of a jump the target
  language allows too, so most are written as jumps. It starts with a
  count, over a few libraries, of which forms occur and where they land.
  A jump into a block, which older code has, and an entry point reached
  by a jump are the two that need a unit written another way.
- **`COMMON` and `BLOCK DATA`.** Storage that several units share by its
  place and not by its name, and may each see as other types.
- **`COMPLEX`**, its constants and its arithmetic.
- **`FORMAT` statements, Hollerith constants, tab-form source**, and the
  input and output not built yet.
- **A procedure of the language passed as an argument**, and the smaller
  refusals the translator lists.
- **A setting that has a unit reached again throw**, as the reference
  compiler has one. A unit that calls itself is refused already.
- **Storage of two gigabytes or more in a program unit.**
- **Conformance tests against a reference compiler**, so that the
  language is covered by tests of its own and not by one library's.

It needs: *A first release*, so that the tree is finished and its passes
are known to hold. `PENDING.md`, *FORTRAN 77 that Fortrania does not handle yet*, *Jumps:
what FORTRAN 77 allows is what the target language allows*,
*Storage of two gigabytes or more in a program unit*, *Conformance tests
against a reference compiler* and *A tree between the analysis and the
writers*.

## FORTRAN before 77

FORTRAN 66 and the dialects around it are for the most part what *All of
FORTRAN 77* already covers. What differs is a short list, each a setting of a
translation:

- **A `DO` loop runs at least once**, where FORTRAN 77's may run not at
  all.
- **Text is kept in numbers**, by Hollerith constants in `DATA` and in
  calls, since the language has no `CHARACTER` type.
- **The extensions of the compilers of the time** that a program at hand
  turns out to need, each added when one does.

It needs: *All of FORTRAN 77*. It is small beside it.

## Fortran 90 and 95

The largest stage, several times what FORTRAN 77 has taken, since three
things the translator rests on stop holding:

- **Every argument is an address.** An array of assumed shape brings its
  bounds with it, so a call passes a description of the array and not
  only where it starts.
- **No procedure calls itself.** Kept state, the layout of working
  buffers, and a large local array kept in its unit's block all rest on
  that; a `RECURSIVE` procedure has none of it.
- **Every size is known when translating.** An allocatable array and a
  pointer are sized as the program runs.

And the language itself is much larger: free-form source, which is a
second reader of source; modules; derived types; kinds; expressions and
assignments of whole arrays and of sections of them; internal procedures;
optional and keyword arguments; generic interfaces and defined operators;
and some seventy more procedures of the language. Fortran 95 adds little
to that: `FORALL`, and procedures declared `PURE` or `ELEMENTAL`.

It needs: *All of FORTRAN 77*, and the tree one step higher than it is, with an
element of an array and an argument passed by reference still seen as
that. `PENDING.md`, *The language written and the safety of what is
written are two choices*, has that step, which the checked ways of
reaching memory need as well.

## Fortran 2003 and after

Not planned in any detail.

- **Fortran 2003:** types that extend one another and procedures bound
  to them, which the target language has close counterparts for; working
  with C; procedure pointers; stream input and output.
- **Fortran 2008 and 2018:** submodules, `BLOCK`, `DO CONCURRENT`, and
  more of working with C. Coarrays, which make one program of several
  images running side by side, are a runtime of their own and are left
  out until something needs them.

It needs: *Fortran 90 and 95*.

## Beside the stages

These follow no stage. Each can be taken when it is wanted, and says what
it needs.

**What a translation is written as.**

- **Another .NET language**, Visual Basic or F#, through the runtime's
  package, and **ways of reaching memory that are checked**, chosen by a
  setting and not by the language. They need the tree finished, and the
  checked ways need it one step higher. `PENDING.md`, *The language
  written and the safety of what is written are two choices*.
- **Back ends beyond source text:** compiling in memory, writing
  intermediate language, and expression trees, each of which makes
  FORTRAN loadable as a program runs. `PENDING.md`, *Back ends beyond C#
  source*.
- **A language outside .NET.** Possible, since the tree is not C#'s; the
  runtime written again in that language is the larger part. Not
  planned.
- **Passes of a user's own**, once the tree is public.

**What the runtime offers.**

- **One heap for a context's memory**, native, pinned or pooled, and
  **a context's environment variables.** `PENDING.md`, *A context's
  native memory comes from one heap* and *A context's environment
  variables*.
- **A pool of contexts** for a host that serves many callers.
  `PENDING.md`, *One caller at a time, and a pool of contexts*.
- **Files mapped into memory.** `PENDING.md`, *Memory-mapped kernels*.
- **The runtime handed out as source.** `PENDING.md`, *The runtime can
  be handed out as source*.

**What a translation reports.**

- **A code for each kind of message**, once the set has been designed as
  a whole. It waits for nothing and nothing waits for it. `PENDING.md`,
  *What a translation reports is written to a file, and later has
  codes*.

**Variants of a translation.**

- **Extended precision**, **characters of 16 bits**, and **messages in
  other languages.** `PENDING.md` has a section for each.
- **A setting for each variance from the reference compiler.**

**The platform.** .NET 10 first, and .NET 11 beside it when it is
released; `PENDING.md` has it under that name.
