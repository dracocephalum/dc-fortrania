# TODO

Open items for this repository that are not a ticket yet: steps
initialization could not finish, decisions deferred, manual actions still
owed. Each entry says what would close it, so it can be picked up cold. When
an item becomes real work, open a ticket and remove it from here; when it is
done, remove it. An agent that leaves something unfinished, blocked, or
undecided adds it here rather than mentioning it once in a conversation.

## Decisions pending

- [ ] **The local rules still speak as the translated library's
      repository did.** `docs/rules/performance.md` and
      `docs/rules/portability.md` were written when the translator, the
      runtime and the translated SPICE library shared a repository, and
      name the toolkit and its kernels where they mean translated code and
      its files. `DESIGN.md`, `PENDING.md` and `GLOSSARY.md` have had what
      was only the library's taken out, and may still hold a sentence that
      was. Close by: reading each through once and rewording.
- [ ] **Where the publishing pipeline runs.** GitHub Actions has nuget.org's
      trusted publishing and so needs no stored key; Azure DevOps would
      need one. The development pipeline is on GitHub either way. Close
      by: choosing, before the first release.
- [ ] **The release pipeline is not written.** Decided: pushing a tag
      starts it, `v` and the version for everything that follows the
      root `Version.props`, the component's folder name before it
      (`build/v0.3.1`) for a component with a file of its own; it builds
      that commit as a release and publishes to nuget.org, and nothing
      else ever publishes there. It refuses a tag that is not the version
      in the code, a commit that is not on the default branch, and a
      version nuget.org already has. Close by: the workflow, and trusted
      publishing set up on the nuget.org account, before the first
      release.
- [ ] **Nothing stops a version that was released from being built
      again.** `Version.props` holds the next release's number and is
      raised by hand after a release; a ci build made before it is raised
      sorts below the release it follows. Close by: a step of the ci
      pipeline that fails when the version in the code is already on
      nuget.org, once there is a release to compare with.
- [ ] **The deploy job has not removed a version.** It has published, twice.
      It removes once a package has more `ci` versions than it keeps, and
      whether the run's own token may do that is the part most likely to
      need mending: the host calls that ability a preview. Close by: a run
      that removes.
- [ ] **A package says little about itself.** No readme is packed, since
      the component's has links that only resolve in the repository, and
      the translator has no command to be packed as a tool. Close by: a
      readme written for the package's page, named in the pipeline's
      trigger list after the entry that takes documents away, and the
      command of
      `PENDING.md`, *The command is `fortrania`, and the target language
      its first word*.
- [ ] **A build on a developer's machine has no feed.** It is numbered
      `-dev` and packed to whatever folder is named; nothing reads it
      from there. Close by: a folder feed named by an environment
      variable, if testing a change in the repository that uses the
      packages turns out to need one.
- [ ] **Nothing makes a translation clear what it takes from the stack.**
      It is left as the stack had it, as FORTRAN leaves it, and a program
      that reads a local it never set shows that only by a wrong answer.
      Wanted: a setting of the translator's that has every local cleared,
      or filled with a value that stands out, for finding such a program.
      Close by: the setting, and what it writes in place of the class's
      `[SkipLocalsInit]`.
- [ ] **The benchmarks measure code compiled once, without a profile.**
      `tools/benchmarks` turns the runtime's tiered compilation off so that
      a short run measures one thing, and so measures none of what a
      long-running program gains from its own profile, a call compiled
      into its caller above all. Close by: a job that runs with tiering on
      and warms up until the code has settled, in a process of its own,
      for the figures of a change that turns on a call's cost.
- [ ] **A paired figure moves with where the compiled code lies.** The
      runner in `tools/benchmarks` repeats to a percent within one set of
      benchmarks, and the same comparison moved by ten to twenty points
      when the set, or the order of compiling, changed. Close by: each
      comparison measured in several processes of its own, the order of
      compiling varied, and the range of their ratios reported in place
      of one.

- [ ] **The block of working memory has nothing around it.** The runtime
      has the block, `WorkingMemory`, with its regions and its levels; no
      translated code uses it yet. A page that cannot be touched at each
      end of a block is decided and not built: it needs the operating
      system's own calls to protect memory, one way on Windows and
      another elsewhere, which the runtime has none of so far. Close by:
      the two pages, with a platform that has neither call left as it is
      now, and `docs/rules/portability.md` saying so.
- [ ] **A call through an argument takes a whole block.** Each level of
      such calls has a block as large as the first, every region in it,
      113,528 bytes for the reference sources with their test program,
      though the procedure called and what it reaches mostly need a
      small part. Ways to take less, each with more code behind it: a
      level sized by what the procedure called can reach, which the
      translator knows for one of its own; levels that share one block
      and start where the chain that led to the call ends, which needs
      that end known when the call is made; or a level's block given
      back when the outermost call returns. Close by: the bytes a
      level really uses measured over the reference sources' tests, and
      one of these taken if they are far below a block.
- [ ] **A package added later is public from its first version.** The
      repository is public and its two `ci` packages are private, each
      told not to take its access from the repository; a visitor finds
      neither. A package the pipeline publishes for the first time has
      that setting on, and is readable by anyone until it is turned off
      on the package's page, which is the owner's to do, with this
      repository given write access there and those that read it given
      theirs. It is done the day a package is added, straight after the
      merge that first publishes it. Close by: a command that sets it,
      should the host come to have one, written into the pipeline.
