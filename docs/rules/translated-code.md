# Translated code

Applies when creating or building a component that holds code translated from
the SPICE toolkit, changing what the translator emits, or handling the
original FORTRAN sources.

## The original sources stay out of the repository

NAIF, which maintains and publishes the toolkit for NASA, does not permit
redistribution of the unmodified toolkit.
Its FORTRAN sources, its C sources, its documentation and its test kernels are
never committed here, in whole or in part, and neither is a patch that
reproduces them. The translator reads them from `cache/` at the repository
root, which git ignores: filled from NAIF's own server, checked against
a hash, when it is not there, or linked to a copy on the machine. No path to
a copy on any machine appears in a tracked file; NAIF's address does.

## What the translator emits

- Every generated file opens with a header saying that it is a machine
  translation of the named routine, which version of the toolkit it came
  from, that it is not a product of the toolkit's authors and that they do
  not support it. NAIF's rules ask for modified code to be marked.
- The original routine header and comments are carried into the output, the
  disclaimer included.
- The output is deterministic: the same sources and the same translator
  produce the same bytes, so a change in the translator shows up as a diff
  in exactly the routines it affects.
- Generated files are never edited by hand. A wrong translation is fixed in
  the translator. What a translation needs beyond translating is done by a
  tool the settings run after it, in `tools/spice-translation`: the change is
  made the same way every time, its logic is committed and reviewed like any
  code, and each file it changes says so in its header. A diff of translated
  code is then the translator's change or the tool's, and never a person's.

## It is committed, as an intermediate product

The translator's output is committed, and it is not the product: the package
compiled from it is. `DESIGN.md`, *Translated code is committed, and it is an
intermediate product*, has the reasoning.

Regenerating is one translator run over the whole library, committed on its
own with the translator version in the message, never mixed with a
hand-written change.

## Licence

Translated code is not covered by the root `LICENSE`; `DESIGN.md`, *Two
licences, split by component*, says why and where the line runs. What a
component has to do about it:

- A component that holds translated code carries its own `LICENSE.md`,
  stating that the code is a machine translation, naming NAIF's rules,
  and saying that the root licence does not apply. Its package metadata
  points at that file and not at an SPDX identifier.
- It holds nothing a person wrote. Hand-written code that builds on it lives
  in a component of its own.
- The root `NOTICE` states the split for the reader who starts there.

## Building it

A component that holds translated code relaxes the build, in its own
`.csproj` and nowhere else:

    <PropertyGroup>
      <TreatWarningsAsErrors>false</TreatWarningsAsErrors>
      <EnforceCodeStyleInBuild>false</EnforceCodeStyleInBuild>
      <RunAnalyzers>false</RunAnalyzers>
    </PropertyGroup>

That is temporary by intent: the translator's output is not yet clean, and
several hundred thousand lines of warnings would hide the ones that matter.
`TODO.md` carries the entry to turn it back on. Never achieve the same thing
with a `Directory.Build.props` inside the component — a nested copy severs it
from the root configuration altogether.

Hand-written code does not share a component with translated code, so that
neither the relaxation nor the licence covers code a person wrote.

## Verifying it

The reference toolkit ships its own regression tests. A translation is correct
when those tests, translated by the same translator, pass against the
translated library; that is the acceptance test for the translator, and no
routine is hand-checked in its place.
