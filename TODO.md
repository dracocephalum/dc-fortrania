# TODO

Open items for this repository that are not a ticket yet: steps
initialization could not finish, decisions deferred, manual actions still
owed. Each entry says what would close it, so it can be picked up cold. When
an item becomes real work, open a ticket and remove it from here; when it is
done, remove it. An agent that leaves something unfinished, blocked, or
undecided adds it here rather than mentioning it once in a conversation.

## Decisions pending

- [ ] **The translator's tunables lie loose among its settings.** What
      changes the code a translation writes, and not what is translated or
      where, sits at the root of the settings beside the paths and the
      projects: `inlineTextConstants`, `plainArrayLoops` and
      `allowsArgumentAliasing` today, and
      those the plan has to come, the run length a copy is done at once
      from, a translation fixed to one place for working buffers, the
      setting that clears or fills every local, and the warnings
      silenced. Most are about speed, and each was added on its own, with
      a name of its own making. Close by: one group in the settings for
      them, the ones for speed together, with one way of naming them,
      each saying what it costs and what it gives, and settled before the
      settings are public in the cleanup round.
- [ ] **The plan still speaks in the reference sources' words.**
      `PENDING.md` was written beside the library Fortrania was made to
      translate, and says kernels, the kernel pool and the toolkit where a
      reader of this repository would say files, saved state and the
      reference sources. The rules have been reworded, and `DESIGN.md` for
      the most part. Close by: each section reworded as it is next
      touched, or moved to `DESIGN.md` when it is built.
- [ ] **Whether a routine called again while it runs is refused** is to
      be decided again. `PENDING.md`, *Working buffers: what is not
      built*, has why what was decided no longer stands and the three
      answers there are. Close by: choosing one, before the first
      release.
- [ ] **The plan says some things in more than one place:** what a
      record costs to read, under input and output, under the
      measurements and under mapped files; the page that cannot be
      touched around the arena and the setting that clears every local,
      which are here too; and the tunables of the first entry above,
      which the plan lists under its settings. Close by: each said once
      and pointed at, as its stage of `ROADMAP.md` is taken up.
- [ ] **Where the publishing pipeline runs.** `PENDING.md`, *Before
      Fortrania is published*, has what weighs in it. Close by: choosing,
      before the first release.
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
- [ ] **A package says little about itself.** No readme is packed, since
      the component's has links that only resolve in the repository.
      Close by: a readme written for each package's page, the command's
      above all, named in the pipeline's trigger list after the entry
      that takes documents away.
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
- [ ] **A paired figure moves with where the compiled code lies**, as
      `tools/benchmarks/README.md` says. Close by: each
      comparison measured in several processes of its own, the order of
      compiling varied, and the range of their ratios reported in place
      of one.

- [ ] **The arena of working buffers has nothing around it.** The runtime
      has the arena, `BufferArena`, with its regions and its copies, and
      translated code keeps its buffers there where a context says. A page that cannot be touched at each
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
- [ ] **A method too large for the just-in-time compiler to optimize
      takes a large frame, whatever the setting for working buffers.**
      Eight methods of the reference sources' test program are past the
      size at which that compiler stops optimizing, which leaves them
      slow code and frames of up to 25 KB; the library itself has none
      such. There were nine, with frames of up to 130 KB, before a
      library kept its character constants in one block. Not done
      either: a method reaches another library of its program through a
      property at each call, which is a call where the compiler's
      allowance for writing in line is used up. Close by: parting a
      method past a size into several, and the library taken once into
      a local.
