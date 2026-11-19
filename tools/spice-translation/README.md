# spice-translation

Translates the SPICE Toolkit from its FORTRAN 77 sources with Fortrania, and
rewrites what the translation needs after it is written.

| | |
|---|---|
| Category | `tools/` |
| Kind | tool |
| Solution | `Dracocephalum.Spice.Translation.slnx` |
| Projects | `src/Dracocephalum.Spice.Translation`, `test/Dracocephalum.Spice.Translation.Tests` |

## What it does

It holds the toolkit's translation settings, `spice.fortrania.json`, and runs
Fortrania over them: the toolkit's main and support libraries become one
project, its test utilities and test program another. After the translation
the settings run the tool's own `rewrite`, which puts the header NAIF's
rules ask for into every translated file and makes `ZZPLATFM` report the platform it
runs on instead of the one its sources were written for.

## Run it

    dotnet build Dracocephalum.Spice.Translation.slnx --nologo -v:q
    dotnet test  Dracocephalum.Spice.Translation.slnx

Translate, from the repository root:

    dotnet run --project tools/spice-translation/src/Dracocephalum.Spice.Translation -- translate

`--force` writes every file again. The translation goes to `cache/translated/`;
build `Dracocephalum.Spice.TSpice` there and run it to run the toolkit's own
test suite.

## Configuration

The sources are read from `cache/N0067/` at the repository root, which git
ignores. When the folder is not there, the translation downloads NAIF's test
package of N0067 into it, checked against the hash in the settings. To use a
copy already on the machine, link the folder to it before the first run; on
Windows a junction needs no administrator:

    mklink /J cache\N0067 <folder of the expanded tspice package>

## Notes

- The sources are never committed, and neither is the translation yet: it
  is to have a component of its own, under the toolkit's terms, as
  [`docs/rules/translated-code.md`](../../docs/rules/translated-code.md) asks.
- `rewrite` is run by a translation, which names the files it wrote in
  `FORTRANIA_WRITTEN`; it changes only those.
