# dc-fortrania

> Initialized with <https://github.com/dracocephalum/dc-agentics>. The
> choices made, and the toolkit commit, are in
> [`.agentics.yaml`](.agentics.yaml); open items are in [`TODO.md`](TODO.md).

Fortrania: a FORTRAN 77 to C# translator, and the small managed runtime
library the translated code runs on, with no native binaries. It was
written to translate NASA's SPICE Toolkit, and holds nothing of it.

## What is in here

Each category folder holds independent components, one solution each. The
**map of components lives in each category's `README.md`**; this file is the
map of categories.

| Category | What lives there | Map |
|---|---|---|
| `libraries/` | packages published to NuGet | [`libraries/README.md`](libraries/README.md) |

## Working here

- One solution per component; **no root solution** — build the component you
  are changing.
- Build configuration is inherited from the root by every component.
- Every component has its own `README.md`: what it is, how to run it, how to
  test it.
- `agentics/` and the toolkit's shims under `.claude/skills/` are managed by
  the toolkit and replaced whole by a sync, a shim of the same name included;
  this repository's own rules live in `docs/rules/`.

## Conventions and rules

`AGENTS.md` is the index. The rules under `agentics/rules/` are the toolkit's
standard for code, tests, dependencies, pull requests, and review — for people
as much as for agents — and a sync replaces that folder whole, so an edit
there is lost. The rules this repository adds are under `docs/rules/`, with
their own index there.

## Licence

Apache License 2.0 — see [`LICENSE`](LICENSE). Copyright 2026 Chris Har.

That covers the translator and the runtime. What is translated with them
keeps the terms of its sources: [`DESIGN.md`](DESIGN.md), *A translation
keeps the terms of its sources*.
