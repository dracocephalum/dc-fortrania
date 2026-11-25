using System.Diagnostics;
using Shouldly;

namespace Dracocephalum.Fortrania.Build.Tests;

/// <summary>
/// The package's targets in a build: a library of two translations and a
/// program, each project with settings files of its own, one of the
/// library's translations kept in its folder and the others in intermediate
/// folders, built with <c>dotnet build</c> as someone who references the
/// package builds them.
/// </summary>
/// <remarks>
/// The package is laid out as it is packed, the command beside the targets,
/// and the projects import the targets where a package's are imported:
/// after the SDK's. The folder the projects lie in holds empty files of the
/// names MSBuild looks upward for, so that nothing of this repository's own
/// build reaches them. The SDK is what runs the tests, so it is no live
/// dependency of theirs.
/// </remarks>
public sealed class BuildTargetsTests : IDisposable
{
    // A build that has not ended by then is not going to.
    private static readonly TimeSpan BuildTimeout = TimeSpan.FromMinutes(5);

    private const string Library =
        """
              SUBROUTINE APPLY ( F, X, Y )
              DOUBLE PRECISION F, X, Y
              EXTERNAL F
              Y = F ( X )
              END

              DOUBLE PRECISION FUNCTION TWICE ( X )
              DOUBLE PRECISION X
              TWICE = 2 * X
              END
        """;

    private const string Program =
        """
              PROGRAM TOOL
              DOUBLE PRECISION Y, SQUARE, TWICE, HALF
              EXTERNAL SQUARE
              CALL APPLY ( SQUARE, 3.0D0, Y )
              CALL EXIT ( INT ( Y + TWICE ( 1.0D0 ) + HALF ( 4.0D0 ) ) )
              END

              DOUBLE PRECISION FUNCTION SQUARE ( X )
              DOUBLE PRECISION X
              SQUARE = X * X
              END
        """;

    // The library's second translation, written to its intermediate folder
    // and compiled into the same assembly.
    private const string Half =
        """
              DOUBLE PRECISION FUNCTION HALF ( X )
              DOUBLE PRECISION X
              HALF = X / 2
              END
        """;

    private const string Main =
        """
        using Dracocephalum.Fortrania.Runtime;

        using var context = new FortranContext(FortranContextOptions.Standalone);
        try
        {
            context.Get<Example.App.AppMain>().TOOL();
            return 0;
        }
        catch (FortranStopException stop)
        {
            return stop.ExitCode;
        }

        """;

    private readonly string _root = Path.Combine(AppContext.BaseDirectory, "build-" + Guid.NewGuid().ToString("N"));

    public BuildTargetsTests()
    {
        LayOutPackage();
        foreach (var name in new[] { "Directory.Build.props", "Directory.Build.targets", "Directory.Packages.props" })
        {
            Put(name, "<Project />");
        }

        Put("Lib/Example.Lib.csproj", ProjectFile(string.Empty));
        Put("Lib/lib.fortrania.json", """{ "name": "Example.Lib", "output": ".", "sources": [ { "name": "Core", "files": [ "fortran/*.f" ] } ] }""");
        Put("Lib/fortran/lib.f", Library);
        Put("Lib/more.fortrania.json", """{ "name": "Example.More", "output": "$(IntermediateOutput)/more", "sources": [ { "name": "Halves", "files": [ "more/*.f" ] } ] }""");
        Put("Lib/more/half.f", Half);

        Put("App/Example.App.csproj", ProjectFile("""<PropertyGroup><OutputType>Exe</OutputType></PropertyGroup><ItemGroup><ProjectReference Include="../Lib/Example.Lib.csproj" /></ItemGroup>"""));
        Put("App/app.fortrania.json", """{ "name": "Example.App", "output": "$(IntermediateOutput)/fortrania", "sources": [ { "name": "AppMain", "files": [ "fortran/*.f" ] } ] }""");
        Put("App/fortran/main.f", Program);
        Put("App/Program.cs", Main);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public async Task Build_WhenALibraryOfTwoTranslationsAndAProgramHaveSettingsOfTheirOwn_ShouldTranslateCompileAndRunThem()
    {
        // Act
        var (code, output) = await BuildAsync();

        // Assert: one of the library's translations kept in its folder, the
        // other and the program's in intermediate ones, and the program
        // reaching both of the library's through the summaries in its
        // assembly.
        code.ShouldBe(0, output);
        File.Exists(Path.Combine(_root, "Lib/Generated/Core/TWICE.g.cs")).ShouldBeTrue();
        Directory.GetFiles(Path.Combine(_root, "Lib/obj"), "HALF.g.cs", SearchOption.AllDirectories).ShouldHaveSingleItem();
        Directory.GetFiles(Path.Combine(_root, "App/obj"), "TOOL.g.cs", SearchOption.AllDirectories).ShouldHaveSingleItem();
        output.ShouldNotContain(": warning", Case.Sensitive);
        (await RunAsync("dotnet", Path.Combine(_root, "App/bin/Debug/net10.0/Example.App.dll"))).Code.ShouldBe(9 + 2 + 2);
    }

    [Fact]
    public async Task Build_WhenNothingChanged_ShouldNotTranslateAgainUntilASourceDoes()
    {
        // Arrange
        (await BuildAsync()).Code.ShouldBe(0);

        // Act
        var unchanged = await BuildAsync();
        File.SetLastWriteTimeUtc(Path.Combine(_root, "Lib/fortran/lib.f"), DateTime.UtcNow);
        var touched = await BuildAsync();
        Put("App/fortran/extra.f", "      SUBROUTINE EXTRA\n      END\n");
        var added = await BuildAsync();

        // Assert
        unchanged.Code.ShouldBe(0, unchanged.Output);
        unchanged.Output.ShouldNotContain("Translated:");

        // The library only, both of its translations in one run: their
        // summaries are the same, so the program that calls into them is
        // not translated again.
        touched.Output.Split("Translated:").Length.ShouldBe(2, touched.Output);
        touched.Output.ShouldContain("Translated: 3 program units, 0 files written");
        added.Code.ShouldBe(0, added.Output);
        added.Output.ShouldContain("Translated: 3 program units, 2 files written");
    }

    [Fact]
    public async Task Build_WhenTheProjectsFolderHasNoSettings_ShouldSaySoAndFail()
    {
        // Arrange
        File.Delete(Path.Combine(_root, "Lib/lib.fortrania.json"));
        File.Delete(Path.Combine(_root, "Lib/more.fortrania.json"));

        // Act
        var (code, output) = await BuildAsync();

        // Assert
        code.ShouldNotBe(0);
        output.ShouldContain("The project's folder has no fortrania.json or *.fortrania.json");
    }

    private static string ProjectFile(string body) =>
        $"""
        <Project>
          <Import Project="Sdk.props" Sdk="Microsoft.NET.Sdk" />
          <PropertyGroup>
            <TargetFramework>net10.0</TargetFramework>
            <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
            <Nullable>enable</Nullable>
          </PropertyGroup>
          <ItemGroup>
            <Reference Include="../package/lib/Dracocephalum.Fortrania.Runtime.dll" />
          </ItemGroup>
          {body}
          <Import Project="Sdk.targets" Sdk="Microsoft.NET.Sdk" />
          <Import Project="../package/build/Dracocephalum.Fortrania.Build.targets" />
        </Project>
        """;

    // Builds the program, and with it the library, as a person would; its
    // outcome and what it said. No build node is left to be reused, so that
    // nothing of a build is left running but the compiler's server, as
    // after any build.
    private Task<(int Code, string Output)> BuildAsync() =>
        RunAsync("dotnet", "build", Path.Combine(_root, "App/Example.App.csproj"), "-nodeReuse:false", "-v:n");

    private static async Task<(int Code, string Output)> RunAsync(string program, params string[] arguments)
    {
        var start = new ProcessStartInfo(program) { RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        // What the test's own build left in the environment would steer
        // the build started here to another SDK's pieces.
        foreach (var name in start.Environment.Keys.Where(key => key.StartsWith("MSBuild", StringComparison.OrdinalIgnoreCase)).ToList())
        {
            start.Environment.Remove(name);
        }

        using var process = Process.Start(start) ?? throw new InvalidOperationException($"{program} did not start.");
        var output = process.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
        var error = process.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(BuildTimeout);
        await process.WaitForExitAsync(timeout.Token);
        return (process.ExitCode, await output + await error);
    }

    // The package as it is packed: the command under tools, and the
    // targets under build; the runtime beside them, for the projects.
    private void LayOutPackage()
    {
        var tools = Path.Combine(_root, "package/tools/net10.0");
        var lib = Path.Combine(_root, "package/lib");
        Directory.CreateDirectory(tools);
        Directory.CreateDirectory(lib);
        foreach (var name in new[] { "Dracocephalum.Fortrania.Cli.dll", "Dracocephalum.Fortrania.Cli.runtimeconfig.json", "Dracocephalum.Fortrania.dll" })
        {
            File.Copy(Path.Combine(AppContext.BaseDirectory, name), Path.Combine(tools, name));
        }

        File.Copy(Path.Combine(AppContext.BaseDirectory, "Dracocephalum.Fortrania.Runtime.dll"), Path.Combine(lib, "Dracocephalum.Fortrania.Runtime.dll"));
        Directory.CreateDirectory(Path.Combine(_root, "package/build"));
        File.Copy(
            Path.Combine(AppContext.BaseDirectory, "package/build/Dracocephalum.Fortrania.Build.targets"),
            Path.Combine(_root, "package/build/Dracocephalum.Fortrania.Build.targets"));
    }

    private void Put(string path, string text)
    {
        var full = Path.Combine(_root, path);
        Directory.CreateDirectory(Path.GetDirectoryName(full) ?? _root);
        File.WriteAllText(full, text);
    }
}
