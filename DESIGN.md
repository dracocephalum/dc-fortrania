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

## Fortrania's lexer finds no keywords

FORTRAN 77 gives blanks no meaning outside a character constant and reserves
no words. `DO 10 I = 1, 5` and `DO10I=1,5` are the same statement, and
`DO10I = 1.5` is an assignment to a variable. Whether a statement begins with
a keyword can only be decided by looking at the whole of it.

So the work is split. `Lexer` removes blanks and returns a run of letters and
digits as one name, without looking for keywords. The parser first decides
what kind of statement it has, and then splits a keyword off the front of a
name where that kind calls for one.

The alternative rejected was to treat blanks as separators, as later
standards do. It would lex the reference sources, which are written with
blanks in the conventional places, but it would not be FORTRAN 77, and the
translator is meant to be general.

## What a name means is kept per program unit, not per use

In FORTRAN 77 a name stands for one thing throughout a program unit: a
variable, a named constant, a function, a subroutine. `F(X)` is an array
element everywhere in the unit or a function reference everywhere in it,
and the text of one statement cannot say which. Declarations and every use
in the unit decide it together.

So the parser leaves `F(X)` undecided, as one kind of node, and analysis
answers once per name: the `SymbolTable` of a unit gives each name its kind
and type. A later stage that meets `F(X)` looks `F` up. No second tree with
the answer written into each node is built, because the answer never
differs from node to node.

The two exceptions are narrow and are handled where they arise. Inside a
function its own name is the variable that holds the result. And a statement
function's dummy arguments are names of that one statement, sharing only
their type with the unit's variable of the same name.

## What crosses program units is a summary, not a tree

To translate a call, Fortrania has to know the procedure called: how many
arguments it takes, which are arrays, what type each is. That is a fact about
another program unit, often in another file.

Each unit is therefore reduced to a `UnitSummary`: how each of its entry
points is called, and every call it makes itself. A summary holds nothing of
the unit's text. A program is read in two passes: the first keeps only the
summaries, in a `ProcedureCatalogue`, and the second reads each file again
and translates it with the catalogue at hand. No more than one file's tree
is in memory at a time, and a file needs translating again only when it, an
include file it names, or the summary of something it calls has changed.

The role is that of a C header, except that nobody writes it: FORTRAN 77 has
no declarations of other units' procedures, so the summary is drawn from the
definition.

Calls are set against definitions with a light hand. The language asks that
arguments agree in type and shape, but a compiler that takes one file at a
time cannot check it, and programs written for such compilers lean on
arguments being passed by address: one value where an array is expected, an
array of one type read as another, a function called as a subroutine for its
side effects. `CallChecker` passes all of these and refuses only what no
address can stand in for, such as a wrong number of arguments or text where
a number is expected.
