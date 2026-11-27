using System.Diagnostics;
using Shouldly;

namespace Dracocephalum.Fortrania.Build.Tests;

/// <summary>
/// The package's targets in a build: a library made of two settings files
/// and a program, each project with settings files of its own, the
/// library's translation kept in its folder and the program's in its
/// intermediate folder, built with <c>dotnet build</c> as someone who
/// references the package builds them.
/// </summary>
/// <remarks>
/// The package is laid out as it is packed, the command beside the targets,
/// and the projects import the props and the targets where a package's are
/// imported: after the SDK's props, and after its targets. The folder the
/// projects lie in holds empty files of the names MSBuild looks upward for,
/// so that nothing of this repository's own build reaches them. The SDK is
/// what runs the tests, so it is no live dependency of theirs.
/// </remarks>
public sealed class BuildTargetsTests : IDisposable
{
    // A build that has not ended by then is not going to.
    private static readonly TimeSpan BuildTimeout = TimeSpan.FromMinutes(5);

    // What a build's output says, at normal verbosity, where it runs the
    // compiler: the compiler's command line.
    private const string CompilerRuns = "/noconfig";

    // What a translation and its build leave in the intermediate folder.
    private static readonly string[] OursInObj = ["*.g.cs", "fortrania.inventory", "fortrania.command", ".fortrania-manifest.json"];

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
        Put("Lib/lib.fortrania.json", """{ "output": ".", "sources": [ { "name": "Core", "files": [ { "paths": [ "fortran/*.f" ] } ] } ] }""");
        Put("Lib/fortran/lib.f", Library);
        Put("Lib/more.fortrania.json", """{ "output": "more", "sources": [ { "name": "Halves", "files": [ { "paths": [ "more/*.f" ] } ] } ] }""");
        Put("Lib/more/half.f", Half);

        Put("App/Example.App.csproj", ProjectFile("""<PropertyGroup><OutputType>Exe</OutputType></PropertyGroup><ItemGroup><ProjectReference Include="../Lib/Example.Lib.csproj" /></ItemGroup>"""));
        Put("App/app.fortrania.json", """{ "output": "$(IntermediateOutput)/fortrania", "sources": [ { "name": "AppMain", "files": [ { "paths": [ "fortran/*.f" ] } ] } ] }""");
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
    public async Task Build_WhenALibraryOfTwoSettingsFilesAndAProgramHaveSettingsOfTheirOwn_ShouldTranslateCompileAndRunThem()
    {
        // Act
        var (code, output) = await BuildAsync();

        // Assert: the library's two settings files one translation, kept in
        // its folder with its summary beside its project file; the
        // program's in its intermediate folder, reaching the library
        // through the summary in its assembly.
        code.ShouldBe(0, output);
        File.Exists(Path.Combine(_root, "Lib/Generated/Core/TWICE.g.cs")).ShouldBeTrue();
        File.Exists(Path.Combine(_root, "Lib/more/Generated/Halves/HALF.g.cs")).ShouldBeTrue();
        File.ReadAllText(Path.Combine(_root, "Lib/Example.Lib.fortrania-summary.json")).ShouldContain("\"project\": \"Example.Lib\"");
        File.Exists(Path.Combine(_root, "App/Example.App.fortrania-summary.json")).ShouldBeTrue();
        Directory.GetFiles(Path.Combine(_root, "App/obj"), "TOOL.g.cs", SearchOption.AllDirectories).ShouldHaveSingleItem();
        output.ShouldNotContain(": warning", Case.Sensitive);
        (await RunAsync("dotnet", Path.Combine(_root, "App/bin/Debug/net10.0/Example.App.dll"))).Code.ShouldBe(9 + 2 + 2);
    }

    [Fact]
    public async Task Build_WhenToldNeverOnAFreshCheckout_ShouldTranslateNothingAndCompileWhatIsCommitted()
    {
        // Arrange: the library translated once, then its build and its
        // sources gone, as a checkout with its translation committed has it.
        (await BuildAsync()).Code.ShouldBe(0);
        foreach (var folder in new[] { "Lib/obj", "Lib/bin", "Lib/fortran", "Lib/more/half.f" })
        {
            var path = Path.Combine(_root, folder);
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
            else
            {
                File.Delete(path);
            }
        }

        // Act
        var (code, output) = await BuildAsync("-p:FortraniaTranslate=never");

        // Assert
        code.ShouldBe(0, output);
        output.ShouldNotContain("Translated:");
        (await RunAsync("dotnet", Path.Combine(_root, "App/bin/Debug/net10.0/Example.App.dll"))).Code.ShouldBe(9 + 2 + 2);
    }

    [Fact]
    public async Task Build_WhenASettingsFileIsRemovedFromTheItems_ShouldTranslateWithoutIt()
    {
        // Arrange: the library's second settings file taken out, and the
        // program no longer calling what it brought.
        Put("Lib/Example.Lib.csproj", ProjectFile("""<ItemGroup><Fortrania Remove="more.fortrania.json" /></ItemGroup>"""));
        Put("App/fortran/main.f", Program.Replace(" + HALF ( 4.0D0 )", string.Empty, StringComparison.Ordinal));

        // Act
        var (code, output) = await BuildAsync();

        // Assert
        code.ShouldBe(0, output);
        Directory.Exists(Path.Combine(_root, "Lib/more/Generated")).ShouldBeFalse();
        (await RunAsync("dotnet", Path.Combine(_root, "App/bin/Debug/net10.0/Example.App.dll"))).Code.ShouldBe(9 + 2);
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
        var afterAdded = await BuildAsync();

        // Assert: neither translated nor compiled again, the sources being
        // compiled in the same order as by the build that translated them,
        // which wrote some for the first time.
        unchanged.Code.ShouldBe(0, unchanged.Output);
        unchanged.Output.ShouldNotContain("Translated:");
        unchanged.Output.ShouldNotContain(CompilerRuns);
        afterAdded.Output.ShouldNotContain(CompilerRuns);

        // The library only, both of its settings files in one run: its
        // summary is the same, so the program that calls into it is not
        // translated again.
        touched.Output.Split("Translated:").Length.ShouldBe(2, touched.Output);
        touched.Output.ShouldContain("Translated: 3 program units, 0 files written");
        added.Code.ShouldBe(0, added.Output);
        added.Output.ShouldContain("Translated: 3 program units, 2 files written");
    }

    [Fact]
    public async Task Clean_WhenATranslationLiesInTheIntermediateFolder_ShouldLeaveNothingOfItAndTheNextBuildTranslateAgain()
    {
        // Arrange: the program's translation is in its intermediate folder.
        (await BuildAsync()).Code.ShouldBe(0);

        // Act
        var cleaned = await RunAsync("dotnet", "clean", Path.Combine(_root, "App/Example.App.csproj"), "-nodeReuse:false");
        var left = OursInObj.SelectMany(pattern => Directory.GetFiles(Path.Combine(_root, "App/obj"), pattern, SearchOption.AllDirectories)).ToList();
        var (code, output) = await BuildAsync();

        // Assert
        cleaned.Code.ShouldBe(0, cleaned.Output);
        left.ShouldBeEmpty();
        code.ShouldBe(0, output);
        output.ShouldContain("Translated: 2 program units, 3 files written");
        (await RunAsync("dotnet", Path.Combine(_root, "App/bin/Debug/net10.0/Example.App.dll"))).Code.ShouldBe(9 + 2 + 2);
    }

    [Fact]
    public async Task Build_WhenALibraryReferencesAProjectOfPlugins_ShouldBuildItFirstAndTranslateAgainWhenItChanges()
    {
        // Arrange: a project of one plugin, which marks every file, and the
        // library referring to it as plugins are referred to.
        Put("Plugin/Example.Plugin.csproj", """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <Nullable>enable</Nullable>
                <EnableDynamicLoading>true</EnableDynamicLoading>
              </PropertyGroup>
              <ItemGroup>
                <Reference Include="../package/tools/net10.0/Dracocephalum.Fortrania.dll" Private="false" />
              </ItemGroup>
            </Project>
            """);
        Put("Plugin/Marker.cs", Marker("first"));
        Put("Lib/Example.Lib.csproj", ProjectFile("""<ItemGroup><ProjectReference Include="../Plugin/Example.Plugin.csproj" ReferenceOutputAssembly="false" OutputItemType="FortraniaPlugin" /></ItemGroup>"""));

        // Act
        var (code, output) = await BuildAsync();
        Put("Plugin/Marker.cs", Marker("second"));
        var changed = await BuildAsync();

        // Assert: the plugin took part, and the library does not refer to
        // its assembly.
        code.ShouldBe(0, output);
        output.ShouldNotContain(": warning", Case.Sensitive);
        changed.Code.ShouldBe(0, changed.Output);
        File.ReadAllText(Path.Combine(_root, "Lib/Generated/Core/TWICE.g.cs")).ShouldEndWith("// Marked second.\n");
        File.Exists(Path.Combine(_root, "Lib/bin/Debug/net10.0/Example.Plugin.dll")).ShouldBeFalse();
        (await RunAsync("dotnet", Path.Combine(_root, "App/bin/Debug/net10.0/Example.App.dll"))).Code.ShouldBe(9 + 2 + 2);
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
        output.ShouldContain("The project has no settings to translate");
    }

    // A plugin that ends every file with a line of the word given.
    private static string Marker(string word) =>
        $$"""
        namespace Example.Plugin;

        public sealed class Marker : Dracocephalum.Fortrania.Plugins.ITranslationPlugin
        {
            public void BeforeWrite(Dracocephalum.Fortrania.Plugins.TranslationContext context)
            {
                foreach (var file in context.GeneratedFiles)
                {
                    file.Text += "// Marked {{word}}.\n";
                }
            }
        }
        """;

    private static string ProjectFile(string body) =>
        $"""
        <Project>
          <Import Project="Sdk.props" Sdk="Microsoft.NET.Sdk" />
          <Import Project="../package/build/Dracocephalum.Fortrania.Build.props" />
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
    private Task<(int Code, string Output)> BuildAsync(params string[] properties) =>
        RunAsync("dotnet", ["build", Path.Combine(_root, "App/Example.App.csproj"), "-nodeReuse:false", "-v:n", .. properties]);

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
    // props and targets under build; the runtime beside them, for the
    // projects.
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
        foreach (var name in new[] { "Dracocephalum.Fortrania.Build.props", "Dracocephalum.Fortrania.Build.targets" })
        {
            File.Copy(Path.Combine(AppContext.BaseDirectory, "package/build", name), Path.Combine(_root, "package/build", name));
        }
    }

    private void Put(string path, string text)
    {
        var full = Path.Combine(_root, path);
        Directory.CreateDirectory(Path.GetDirectoryName(full) ?? _root);
        File.WriteAllText(full, text);
    }
}
