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
- [ ] **No component holds translated code yet, so the licence split exists
      only on paper.** `NOTICE`, `DESIGN.md` and
      `docs/rules/translated-code.md` describe it. Close by: the first such
      component, created with its own `LICENSE.md`, the relaxed build
      settings, and package metadata that points at that file.
