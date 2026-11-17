# TODO

Open items for this repository that are not a ticket yet: steps
initialization could not finish, decisions deferred, manual actions still
owed. Each entry says what would close it, so it can be picked up cold. When
an item becomes real work, open a ticket and remove it from here; when it is
done, remove it. An agent that leaves something unfinished, blocked, or
undecided adds it here rather than mentioning it once in a conversation.

## Initialization

- [ ] **Merge settings and the default-branch ruleset are not applied** —
      there is no remote yet. Close by: publishing the repository, then
      `/source-control setup`, which runs the three commands under
      *Repository settings* in `agentics/rules/source-control/github.md`.
- [ ] **Change tracking is `none` and source control is `local` because
      there is no remote.** Close by: once the repository is on GitHub,
      decide whether `change-tracking.tracker` becomes `github-issues` and
      `source-control.mode` becomes `auto` in `.agentics.yaml`.
- [ ] **Web, hosting and EF Core package pins were not exercised.**
      `Microsoft.AspNetCore.OpenApi`, `Microsoft.Extensions.Hosting`,
      `FluentValidation`, `Serilog.Sinks.TestCorrelator` and
      `Microsoft.EntityFrameworkCore.InMemory` in `Directory.Packages.props`
      are the toolkit's values and no project here references them. Close
      by: removing the entries, or verifying them the day one is used.

## Decisions pending

- [ ] **The licence check fails on one test-only package.**
      `Microsoft.Testing.Extensions.CodeCoverage` ships its licence as a
      file, the vendor's own library terms rather than an SPDX identifier,
      so `nuget-license` cannot classify it and exits non-zero. Every other
      package in the graph passes. It is in the *Review* tier of
      `agentics/rules/dependencies.md`: a person decides. The package is
      referenced by test projects only and is never shipped. Close by:
      either accepting it, with an entry in `license-overrides.json` pinned
      to the version and the reason recorded, or replacing the coverage
      collector.

- [ ] **Choose a licence, and name the copyright holder.** The repository
      has no `LICENSE` file, which means all rights reserved. The intent is
      two licences in one repository, so this is not a single choice:
  - the translator and runtime under `libraries/fortrania/`, and all other
    hand-written code: `Apache-2.0` (patent grant, the default for anything
    shared) or `MIT` (shorter, attribution only);
  - any code translated from the SPICE toolkit: **not** an open-source
    licence. It stays under the toolkit owner's published rules, with the
    original disclaimer kept and a header on each generated file saying it
    is a machine translation that the toolkit's authors do not support.

  Close by: the licence and the legal name of the copyright holder, then
  `LICENSE`, `NOTICE`, the *Licence* section of `README.md`, and
  `repository.licence` and `repository.copyright-holder` in
  `.agentics.yaml`.
- [ ] **Whether translated SPICE code is committed here or only generated
      locally.** Committing makes the repository self-contained and
      reviewable; generating keeps roughly a million lines of machine output
      out of history and leaves only the NuGet package as the published
      form. The original FORTRAN sources are never committed either way.
      Close by: the choice, recorded in `DESIGN.md`.
- [ ] **Relax warnings-as-errors for the generated SPICE library.** Decided:
      the component that holds translated code builds with
      `TreatWarningsAsErrors` off and the style analyzers off, set in its own
      `.csproj`, never in a nested `Directory.Build.props`. Close by:
      applying it when that component is created, and a second entry here to
      turn it back on once the translator's output is clean.
