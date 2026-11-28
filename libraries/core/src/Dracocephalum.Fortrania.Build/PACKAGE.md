# Dracocephalum.Fortrania.Build

Translates a project's FORTRAN 77 to C# as the project builds, and
compiles what it wrote. Nothing is installed: the package carries the
translator.

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
against the FORTRAN file and line.

The translated code works on addresses, as FORTRAN does, which is why the
project allows unsafe code.

Documentation, the properties the package takes, and where to report a
fault: <https://github.com/dracocephalum/dc-fortrania>.
