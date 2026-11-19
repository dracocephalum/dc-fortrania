# TODO

Open items for this repository that are not a ticket yet: steps
initialization could not finish, decisions deferred, manual actions still
owed. Each entry says what would close it, so it can be picked up cold. When
an item becomes real work, open a ticket and remove it from here; when it is
done, remove it. An agent that leaves something unfinished, blocked, or
undecided adds it here rather than mentioning it once in a conversation.

## Decisions pending

- [ ] **The default branch has no server-side protection.** The repository
      is private, and the host refuses the shipped ruleset on a private
      repository under its free plan. The merge settings are applied:
      squash only, branch deleted on merge. Until this is closed, the only
      thing keeping changes off `main` is the rule never to commit there.
      The options: a paid plan, making the repository public, or accepting
      no gate. Close by: one of the first two, then
      `gh api repos/<owner>/<name>/rulesets --input .github/rulesets/protect-main.json`.

- [ ] **Warnings-as-errors is relaxed for translated code, for now.**
      Decided, and written down in `docs/rules/translated-code.md`,
      *Building it*: a component that holds translated code turns
      `TreatWarningsAsErrors` and the analyzers off in its own `.csproj`. No
      such component exists yet. Close by: turning both back on once the
      translator's output builds clean, and deleting that section of the
      rule.
- [ ] **`tools/spice-translation` refers to the translator as a project.**
      A cross-component `ProjectReference`, allowed while no package feed
      exists, with the comment `agentics/rules/layout.md` asks for. Close by:
      a `PackageReference` to the translator's package once there is a feed.
- [ ] **The translated toolkit is written to `cache/translated/`, which git
      ignores.** `docs/rules/translated-code.md` asks that a translated file
      carry the routine's own comments, the original disclaimer among them,
      and the translator does not carry them yet. Close by: carrying them,
      then giving the translation a component of its own under
      `libraries/`, with its `LICENSE.md`, and committing it in a commit of
      its own.
- [ ] **How packages are versioned.** For dc-fortrania's pipelines; see
      `PENDING.md`, *Fortrania becomes a repository of its own*. The
      options: the version read from the latest tag, with a development
      build counted on from it as a pre-release (`1.2.0-dev.14`), which
      MinVer does with nothing to keep in a file; or a version kept in a
      file and a height counted from where it last changed, which
      Nerdbank.GitVersioning does. Either keeps the commit hash the build
      already stamps. Close by: choosing one, before the first pipeline.
- [ ] **What starts a release.** A pushed tag (`v1.2.0`) is the usual
      start, and fits a version read from tags; a release made on GitHub,
      or starting the pipeline by hand, are the others. Who may start one
      is part of it. Close by: choosing, with the versioning above.
- [ ] **Where development packages go.** GitHub Packages keeps them
      beside the repository, but asks for a token to read a package even
      from a public repository; a pipeline's own artifacts need nothing,
      and are read by hand. Close by: choosing, with the development
      pipeline.
- [ ] **Where the publishing pipeline runs.** GitHub Actions has nuget.org's
      trusted publishing and so needs no stored key; Azure DevOps would
      need one. The development pipeline is on GitHub either way. Close
      by: choosing, before the first release.
- [ ] **No component holds translated code yet, so the licence split exists
      only on paper.** `NOTICE`, `DESIGN.md` and
      `docs/rules/translated-code.md` describe it. Close by: the first such
      component, created with its own `LICENSE.md`, the relaxed build
      settings, and package metadata that points at that file.
