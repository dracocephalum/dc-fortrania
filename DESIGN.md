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
