# Rules of this repository

The rules this repository adds to the toolkit's. The toolkit's live under
`agentics/rules/` and are replaced whole by every sync; these are never
touched by one. A rule is a file beside this index and a row below, in the
same two columns as the root `AGENTS.md` table, with links relative to the
repository root — the sync copies the rows into the guidelines block of the
root `AGENTS.md`, so a rule added here is reachable without editing that file.
`agentics/rules/layout.md`, *The toolkit's rules and the repository's*, is the
rule, and its *Precedence* is the warning: a rule here that disagrees with a
toolkit rule has no referee. To replace one, exclude it in `.agentics.yaml`
and write the replacement; to amend one, say so in the document's first lines
and give the row a trigger of its own. Nothing guarantees an amendment holds
against a toolkit rule that still exists.

| When you are | Read |
|---|---|
| Writing or reviewing code that runs on a computation or kernel-reading path, or choosing between a safe and an unsafe, an allocating and a non-allocating, or a general and a specialised form | [`docs/rules/performance.md`](docs/rules/performance.md) — performance is the first design constraint here; amends the C# coding rules |
| Reading or writing binary data, using hardware intrinsics, or touching anything that differs by operating system, processor or byte order | [`docs/rules/portability.md`](docs/rules/portability.md) |
| Creating or building a component that holds code translated from the SPICE toolkit, changing what the translator emits, or handling the original FORTRAN sources | [`docs/rules/translated-code.md`](docs/rules/translated-code.md) |
