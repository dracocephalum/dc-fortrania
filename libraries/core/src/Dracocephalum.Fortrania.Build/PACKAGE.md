# Dracocephalum.Fortrania.Build

Translates a project's FORTRAN 77 to C# as the project builds, and
compiles what it wrote. Nothing is installed: the package carries the
translator.

## What it is

Refer to it and to the runtime the translated code calls:

    <ItemGroup>
      <PackageReference Include="Dracocephalum.Fortrania.Build" PrivateAssets="all" />
      <PackageReference Include="Dracocephalum.Fortrania.Runtime" />
    </ItemGroup>

    <PropertyGroup>
      <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
    </PropertyGroup>

and put a settings file beside the project file, `fortrania.json` or
`*.fortrania.json`:

    {
      "output": ".",
      "sources": [
        { "name": "Core", "files": [ { "paths": [ "fortran/*.f" ] } ] }
      ]
    }

A build then translates what the file names into `Generated/` and
compiles it with the project. The project's name is the namespace and
each source a class, `Example.Library.Core` for a source named `Core` in
a project `Example.Library`. A later build translates again only where a
source, a settings file or the translator changed.

What the translator reports, an error or a warning, is the build's, shown
against the FORTRAN file and line. The translated code works on
addresses, as FORTRAN does, which is why the project allows unsafe code.

## What it does not do yet

It translates the FORTRAN 77 that the sources it was written for use.
`GO TO` and statement labels, `COMMON` and `COMPLEX` are among what it
does not translate yet; a program unit that uses any of them fails the
build with the construct and the line. A warning is shown by the build
that translates, and not by a later one that finds nothing changed.

## What it writes

What this tool writes is made from what it was given, and carries the terms
of that and not this package's.

## Terms

Apache-2.0, the text of which is at <https://www.apache.org/licenses/LICENSE-2.0>.

## Where to ask

Questions, faults and requests go to <https://github.com/dracocephalum/dc-fortrania/issues>.
The properties the package takes are described at <https://github.com/dracocephalum/dc-fortrania>.
