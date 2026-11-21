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

- [ ] **The local rules still speak as the translated library's
      repository did.** `docs/rules/performance.md` and
      `docs/rules/portability.md` were written when the translator, the
      runtime and the translated SPICE library shared a repository, and
      name the toolkit and its kernels where they mean translated code and
      its files. `DESIGN.md`, `PENDING.md` and `GLOSSARY.md` have had what
      was only the library's taken out, and may still hold a sentence that
      was. Close by: reading each through once and rewording.
- [ ] **How packages are versioned.** For the pipelines; see `PENDING.md`,
      *Before Fortrania is published*. A development package needs a
      number that rises with every build and sorts below the release it
      leads to, so a pre-release with a counter after a dot
      (`0.1.0-ci.42`): four numbers and a label is not a version NuGet
      keeps, and a counter joined to the label sorts as text. Where the
      counter comes from is the choice: the pipeline's own run number,
      given to `dotnet pack` as a property, with nothing kept in a file
      and nothing a local build can reproduce; the version read from the
      latest tag with the commits since counted on, which MinVer does; or
      a version kept in a file and a height counted from where it last
      changed, which Nerdbank.GitVersioning does. Each keeps the commit
      hash the build already stamps. Close by: choosing one, before the
      first pipeline.
- [ ] **What starts a release.** A pushed tag (`v1.2.0`) is the usual
      start, and fits a version read from tags; a release made on GitHub,
      or starting the pipeline by hand, are the others. Who may start one
      is part of it. Close by: choosing, with the versioning above.
- [ ] **Where development packages go.** GitHub Packages keeps them
      beside the repository, but asks for a token to read a package even
      from a public repository; a pipeline's own artifacts need nothing,
      and are read by hand; a folder on the developer's machine, named by
      an environment variable, serves a local build of both repositories
      with no host at all. Close by: choosing, with the development
      pipeline.
- [ ] **Where the publishing pipeline runs.** GitHub Actions has nuget.org's
      trusted publishing and so needs no stored key; Azure DevOps would
      need one. The development pipeline is on GitHub either way. Close
      by: choosing, before the first release.
- [ ] **The packages have no metadata yet.** Neither project says what its
      package is called, described as, or licensed under, and the
      translator has no command to be packed as a tool. Close by: package
      metadata inherited from the root, a `dotnet pack` that gives a
      package for the runtime and one for the translator, and the command
      of `PENDING.md`, *The command is `fortrania`, and the target
      language its first word*.
- [ ] **Every `stackalloc` of a translation clears its memory on each
      call.** Nothing asks for `[SkipLocalsInit]`, so a routine of the
      reference sources clears 17,600 bytes on every call, which FORTRAN
      does not ask for: a local has no value until it is given one. Close
      by: emitting the attribute, or a setting for the translated
      projects, with the reference sources' tests and the comparison to
      the last digit passing, which would show any routine that reads a
      local it never set.
