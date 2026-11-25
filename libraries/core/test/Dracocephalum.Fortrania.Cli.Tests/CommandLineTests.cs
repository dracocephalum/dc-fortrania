using Shouldly;

namespace Dracocephalum.Fortrania.Cli.Tests;

public sealed class CommandLineTests : IDisposable
{
    private const string Source =
        """
              SUBROUTINE TWICE ( N )
              INTEGER N
              N = 2 * N
              END
        """;

    private const string Settings =
        """
        {
          "output": "out/Example.Toolkit",
          "sources": [ { "name": "Core", "files": [ "src/*.f" ] } ]
        }
        """;

    private readonly string _root = Path.Combine(AppContext.BaseDirectory, "command-" + Guid.NewGuid().ToString("N"));
    private readonly StringWriter _output = new();
    private readonly StringWriter _error = new();

    public CommandLineTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        _output.Dispose();
        _error.Dispose();
        Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public void Run_WhenToldToTranslateWithTheOneSettingsFileInTheFolder_ShouldTranslateAndSaySo()
    {
        // Arrange
        Put("src/twice.f", Source);
        Put("example.fortrania.json", Settings);

        // Act
        var code = Run("translate", "csharp", "--name", "Example.Toolkit");

        // Assert
        code.ShouldBe(CommandLine.Done, _error.ToString());
        _output.ToString().ShouldStartWith("Translated: 1 program units,");
        File.ReadAllText(Path.Combine(_root, "out/Example.Toolkit/Generated/Core/TWICE.g.cs")).ShouldContain("public void TWICE(int* n)");
    }

    [Fact]
    public void Run_WhenNamedASettingsFileElsewhere_ShouldTakeItsPathsFromWhereItLies()
    {
        // Arrange: run from a folder of its own, the settings beside the
        // sources they name.
        Put("project/src/twice.f", Source);
        Put("project/settings.json", Settings);
        Directory.CreateDirectory(Path.Combine(_root, "elsewhere"));

        // Act
        var code = RunIn(Path.Combine(_root, "elsewhere"), "translate", "csharp", "--name", "Example.Toolkit", "../project/settings.json");

        // Assert
        code.ShouldBe(CommandLine.Done, _error.ToString());
        File.Exists(Path.Combine(_root, "project/out/Example.Toolkit/Generated/Core/TWICE.g.cs")).ShouldBeTrue();
    }

    [Fact]
    public void Run_WhenNothingChangedSinceTheLastTranslation_ShouldWriteNothingUnlessForced()
    {
        // Arrange
        Put("src/twice.f", Source);
        Put("fortrania.json", Settings);
        Run("translate", "csharp", "--name", "Example.Toolkit").ShouldBe(CommandLine.Done);

        // Act
        Run("translate", "csharp", "--name", "Example.Toolkit").ShouldBe(CommandLine.Done);
        var again = _output.ToString();
        Run("translate", "csharp", "--name", "Example.Toolkit", "--force").ShouldBe(CommandLine.Done);
        var forced = _output.ToString()[again.Length..];

        // Assert
        again.ShouldEndWith($"0 files written, 0 removed.{Environment.NewLine}");
        forced.ShouldNotContain(" 0 files written");
    }

    [Fact]
    public void Run_WhenTheSourceCannotBeTranslated_ShouldSayWhereAndFail()
    {
        // Arrange
        Put("src/twice.f", "      SUBROUTINE TWICE ( N\n      END\n");
        Put("fortrania.json", Settings);

        // Act
        var code = Run("translate", "csharp", "--name", "Example.Toolkit");

        // Assert
        code.ShouldBe(CommandLine.Failed);
        _error.ToString().ShouldContain(Path.Combine("src", "twice.f") + ": error: Line 1:");
        _output.ToString().ShouldStartWith("Not translated:");
    }

    [Fact]
    public void Run_WhenTheFolderHasNoSettingsFile_ShouldSayToNameOneAndFail()
    {
        // Act
        var code = Run("translate", "csharp", "--name", "Example.Toolkit");

        // Assert
        code.ShouldBe(CommandLine.Failed);
        _error.ToString().ShouldContain("name the settings file.");
    }

    [Fact]
    public void Run_WhenTheFolderHasSeveralSettingsFiles_ShouldTranslateThemAsOneProject()
    {
        // Arrange
        Put("src/twice.f", Source);
        Put("other/thrice.f", "      SUBROUTINE THRICE ( N )\n      INTEGER N\n      CALL TWICE ( N )\n      END\n");
        Put("a.fortrania.json", Settings);
        Put("b.fortrania.json", """{ "output": "out/Example.Other", "sources": [ { "name": "More", "files": [ "other/*.f" ] } ] }""");

        // Act
        var code = Run("translate", "csharp", "--name", "Example.Toolkit");

        // Assert
        code.ShouldBe(CommandLine.Done, _error.ToString());
        _output.ToString().ShouldStartWith("Translated: 2 program units,");
        File.ReadAllText(Path.Combine(_root, "out/Example.Other/Generated/More/THRICE.g.cs")).ShouldContain("TWICE(");
    }

    [Fact]
    public void Run_WhenTheSettingsAreNotSettings_ShouldSayWhichFileAndFail()
    {
        // Arrange
        Put("fortrania.json", """{ "sources": [], "colour": "red" }""");

        // Act
        var code = Run("translate", "csharp", "--name", "Example.Toolkit");

        // Assert
        code.ShouldBe(CommandLine.Failed);
        _error.ToString().ShouldStartWith(Path.Combine(_root, "fortrania.json") + ": error:");
    }

    [Theory]
    [InlineData]
    [InlineData("vb")]
    [InlineData("translate", "csharp", "--quick")]
    [InlineData("translate", "csharp", "--project", "Example.Toolkit")]
    [InlineData("translate", "csharp", "--f")]
    public void Run_WhenTheCommandLineIsNotOneItTakes_ShouldShowHowToUseItAndSaySo(params string[] arguments)
    {
        // Act
        var code = Run(arguments);

        // Assert
        code.ShouldBe(CommandLine.Misused);
        _error.ToString().ShouldContain("Usage: fortrania translate csharp");
    }

    [Fact]
    public void Run_WhenNamedSeveralSettingsFiles_ShouldTranslateThemAsOneProject()
    {
        // Arrange: a library and a program that calls it, each with its
        // settings in its own folder, the program's beside a project file
        // of its own, which is left as it is.
        Put("lib/twice.f", Source);
        Put("lib/lib.fortrania.json", """{ "output": ".", "sources": [ { "name": "Core", "files": [ "*.f" ] } ] }""");
        Put("app/main.f", "      PROGRAM APP\n      INTEGER N\n      N = 4\n      CALL TWICE ( N )\n      END\n");
        Put("app/Example.App.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\" />\n");
        Put("app/app.fortrania.json", """{ "output": ".", "sources": [ { "name": "AppMain", "files": [ "*.f" ] } ] }""");

        // Act
        var code = Run("translate", "csharp", "--name", "Example.App", "lib", "app/app.fortrania.json");

        // Assert: the program reaches the library's procedure.
        code.ShouldBe(CommandLine.Done, _error.ToString());
        _output.ToString().ShouldStartWith("Translated: 2 program units,");
        File.ReadAllText(Path.Combine(_root, "app/Generated/AppMain/APP.g.cs")).ShouldContain("TWICE(");
        _error.ToString().ShouldBeEmpty();
        File.ReadAllText(Path.Combine(_root, "app/Example.App.csproj")).ShouldBe("<Project Sdk=\"Microsoft.NET.Sdk\" />\n");
    }

    [Fact]
    public void Run_WhenASettingsFileNamedIsNotThere_ShouldSayWhichAndTranslateNothing()
    {
        // Arrange
        Put("src/twice.f", Source);
        Put("fortrania.json", Settings);

        // Act
        var code = Run("translate", "csharp", "--name", "Example.Toolkit", "fortrania.json", "missing.fortrania.json");

        // Assert
        code.ShouldBe(CommandLine.Failed);
        _error.ToString().ShouldContain($"there is no settings file {Path.Combine(_root, "missing.fortrania.json")}.");
        Directory.Exists(Path.Combine(_root, "out")).ShouldBeFalse();
    }

    [Theory]
    [InlineData("t", "cs", "--name", "Example.Toolkit")]
    [InlineData("tr", "c", "--name", "Example.Toolkit", "--of")]
    [InlineData("translate", "csh", "--name", "Example.Toolkit", "--fo")]
    public void Run_WhenWordsAndOptionsAreShortenedToAStartNoOtherShares_ShouldTakeThemForTheWholeWords(params string[] arguments)
    {
        // Arrange
        Put("src/twice.f", Source);
        Put("fortrania.json", Settings);

        // Act
        var code = Run(arguments);

        // Assert
        code.ShouldBe(CommandLine.Done, _error.ToString());
        _output.ToString().ShouldStartWith("Translated: 1 program units,");
    }

    [Theory]
    [InlineData("--v")]
    [InlineData("--vers")]
    public void Run_WhenAnOptionOfItsOwnIsShortened_ShouldTakeItForTheWholeOption(string typed)
    {
        // Act
        var code = Run(typed);

        // Assert
        code.ShouldBe(CommandLine.Done);
        _output.ToString().ShouldMatch(@"^\d+\.\d+\.\d+");
    }

    [Theory]
    [InlineData("translate")]
    [InlineData("translate", "--force")]
    public void Run_WhenTranslateIsGivenNoTarget_ShouldSayWhatItCanTranslateTo(params string[] arguments)
    {
        // Act
        var code = Run(arguments);

        // Assert
        code.ShouldBe(CommandLine.Misused);
        _error.ToString().ShouldContain("translate is to be followed by what to translate to: csharp.");
    }

    [Fact]
    public void Run_WhenAWordIsNoneOfThoseItTakes_ShouldSaySo()
    {
        // Act
        var code = Run("translate", "vb");

        // Assert
        code.ShouldBe(CommandLine.Misused);
        _error.ToString().ShouldContain("vb is not a target of translate.");
    }

    [Theory]
    [InlineData("help")]
    [InlineData("he")]
    [InlineData("-h")]
    [InlineData("--h")]
    public void Run_WhenAskedForHelpInAnyOfItsSpellings_ShouldShowHowToUseItAndSucceed(string typed)
    {
        // Act
        var code = Run(typed);

        // Assert
        code.ShouldBe(CommandLine.Done);
        _output.ToString().ShouldContain("Usage: fortrania translate csharp");
    }

    [Fact]
    public void Run_WhenRunBelowTheSettingsFile_ShouldNotLookAboveAndSaySo()
    {
        // Arrange
        Put("fortrania.json", Settings);
        Directory.CreateDirectory(Path.Combine(_root, "deep"));

        // Act
        var code = RunIn(Path.Combine(_root, "deep"), "translate", "csharp", "--name", "Example.Toolkit");

        // Assert
        code.ShouldBe(CommandLine.Failed);
        _error.ToString().ShouldContain("there is no fortrania.json or *.fortrania.json in");
    }

    [Fact]
    public void Run_WhenAFolderIsNamedForTheSettings_ShouldTakeThoseInIt()
    {
        // Arrange: the settings beside the sources, named by their folder
        // from elsewhere, as a build names its project's.
        Put("project/src/twice.f", Source);
        Put("project/my.fortrania.json", Settings);
        Directory.CreateDirectory(Path.Combine(_root, "elsewhere"));

        // Act
        var code = RunIn(Path.Combine(_root, "elsewhere"), "translate", "csharp", "--name", "Example.Toolkit", "../project");

        // Assert
        code.ShouldBe(CommandLine.Done, _error.ToString());
        File.Exists(Path.Combine(_root, "project/out/Example.Toolkit/Generated/Core/TWICE.g.cs")).ShouldBeTrue();
    }

    [Fact]
    public void Run_WhenOfflineAndTheSourcesAreGone_ShouldCompileWhatWasTranslatedAndWriteNothing()
    {
        // Arrange
        Put("src/twice.f", Source);
        Put("fortrania.json", Settings);
        Run("translate", "csharp", "--name", "Example.Toolkit").ShouldBe(CommandLine.Done);
        File.Delete(Path.Combine(_root, "src/twice.f"));
        var before = _output.ToString().Length;

        // Act
        var offline = Run("translate", "csharp", "--name", "Example.Toolkit", "--offline");
        var online = Run("translate", "csharp", "--name", "Example.Toolkit");

        // Assert
        offline.ShouldBe(CommandLine.Done, _error.ToString());
        _output.ToString()[before..].ShouldStartWith("Translated: 0 program units, 0 files written");
        online.ShouldBe(CommandLine.Failed);
        _error.ToString().ShouldContain("there is no such file");
    }

    [Fact]
    public void Run_WhenOfflineAndNeitherSourcesNorATranslationAreThere_ShouldSayToRestoreAndFail()
    {
        // Arrange
        Put("fortrania.json", Settings);

        // Act
        var code = Run("translate", "csharp", "--name", "Example.Toolkit", "--offline");

        // Assert
        code.ShouldBe(CommandLine.Failed);
        _error.ToString().ShouldContain("restore them first");
    }

    [Theory]
    [InlineData("restore", "--name", "Example.Toolkit")]
    [InlineData("restore", "--name", "Example.Toolkit", "--if-needed")]
    [InlineData("res", "--name", "Example.Toolkit", "fortrania.json", "--if")]
    public void Run_WhenToldToRestoreWhatNeedsNoDownload_ShouldFetchNothingAndSucceed(params string[] arguments)
    {
        // Arrange
        Put("src/twice.f", Source);
        Put("fortrania.json", Settings);

        // Act
        var code = Run(arguments);

        // Assert
        code.ShouldBe(CommandLine.Done, _error.ToString());
        _output.ToString().ShouldStartWith("Restored: 0 folders of sources fetched.");
        Directory.Exists(Path.Combine(_root, "out")).ShouldBeFalse();
    }

    [Fact]
    public void Run_WhenToldToKeepAnInventory_ShouldWriteWhatWasReadMatchedAndMade()
    {
        // Arrange
        Put("src/twice.f", Source);
        Put("fortrania.json", Settings);

        // Act
        var code = Run("translate", "csharp", "--name", "Example.Toolkit", "--inventory", "obj/fortrania.inventory");

        // Assert
        code.ShouldBe(CommandLine.Done, _error.ToString());
        File.ReadAllLines(Path.Combine(_root, "obj/fortrania.inventory")).ShouldBe(
            [
                "read " + Path.Combine(_root, "fortrania.json"),
                "read " + Path.GetFullPath(Path.Combine(_root, "src/twice.f")),
                "pattern " + Path.GetFullPath(Path.Combine(_root, "src/*.f")),
                "file " + Path.GetFullPath(Path.Combine(_root, "out/Example.Toolkit/Generated/Core/TWICE.g.cs")),
                "file " + Path.GetFullPath(Path.Combine(_root, "out/Example.Toolkit/Generated/Core/Core.g.cs")),
                "file " + Path.Combine(_root, "Example.Toolkit.fortrania-summary.json"),
            ]);
    }

    [Fact]
    public void Run_WhenATranslationWithAnInventoryFails_ShouldRemoveTheLastInventory()
    {
        // Arrange: an inventory of a translation that was made, then its
        // sources gone.
        Put("src/twice.f", Source);
        Put("fortrania.json", Settings);
        Run("translate", "csharp", "--name", "Example.Toolkit", "--inventory", "fortrania.inventory").ShouldBe(CommandLine.Done);
        File.Delete(Path.Combine(_root, "src/twice.f"));

        // Act
        var code = Run("translate", "csharp", "--name", "Example.Toolkit", "--inventory", "fortrania.inventory");

        // Assert
        code.ShouldBe(CommandLine.Failed);
        File.Exists(Path.Combine(_root, "fortrania.inventory")).ShouldBeFalse();
    }

    [Fact]
    public void Run_WhenTheInventoryIsNamedTwice_ShouldSaySo()
    {
        // Act
        var code = Run("translate", "csharp", "--name", "Example.Toolkit", "--inventory", "a", "--inventory", "b");

        // Assert
        code.ShouldBe(CommandLine.Misused);
        _error.ToString().ShouldContain("--inventory is given twice; it takes one.");
    }

    [Fact]
    public void Run_WhenToldToScaffold_ShouldWriteAProjectFileOfTheProjectsNameBesideItsSummaryForTheFrameworkGiven()
    {
        // Arrange
        Put("src/twice.f", Source);
        Put("fortrania.json", Settings);

        // Act
        var code = Run("translate", "csharp", "--name", "Example.Toolkit", "--scaffold", "--framework", "net11.0");

        // Assert
        code.ShouldBe(CommandLine.Done, _error.ToString());
        File.ReadAllText(Path.Combine(_root, "Example.Toolkit.csproj")).ShouldContain("<TargetFramework>net11.0</TargetFramework>");
    }

    [Fact]
    public void Run_WhenNotToldToScaffold_ShouldWriteNoProjectFile()
    {
        // Arrange
        Put("src/twice.f", Source);
        Put("fortrania.json", Settings);

        // Act
        var code = Run("translate", "csharp", "--name", "Example.Toolkit");

        // Assert
        code.ShouldBe(CommandLine.Done, _error.ToString());
        Directory.GetFiles(_root, "*.csproj", SearchOption.AllDirectories).ShouldBeEmpty();
    }

    [Theory]
    [InlineData("--entry", "MAIN")]
    [InlineData("--framework", "net11.0")]
    public void Run_WhenAScaffoldsOptionIsGivenWithoutScaffold_ShouldSaySo(string option, string value)
    {
        // Act
        var code = Run("translate", "csharp", "--name", "Example.Toolkit", option, value);

        // Assert
        code.ShouldBe(CommandLine.Misused);
        _error.ToString().ShouldContain($"{option} says what a scaffold writes, and is given without --scaffold.");
    }

    [Fact]
    public void Run_WhenANamespaceIsGiven_ShouldTranslateToIt()
    {
        // Arrange
        Put("src/twice.f", Source);
        Put("fortrania.json", Settings);

        // Act
        var code = Run("translate", "csharp", "--name", "Example.Toolkit", "--namespace", "Example.Chosen");

        // Assert
        code.ShouldBe(CommandLine.Done, _error.ToString());
        File.ReadAllText(Path.Combine(_root, "out/Example.Toolkit/Generated/Core/Core.g.cs")).ShouldContain("namespace Example.Chosen;");
    }

    [Theory]
    [InlineData("translate", "csharp")]
    [InlineData("restore")]
    public void Run_WhenNoNameIsGiven_ShouldSayItIsToBeAndTranslateNothing(params string[] arguments)
    {
        // Arrange
        Put("src/twice.f", Source);
        Put("fortrania.json", Settings);

        // Act
        var code = Run(arguments);

        // Assert
        code.ShouldBe(CommandLine.Misused);
        _error.ToString().ShouldContain("--name is to be given: the name of the project, its assembly's.");
        Directory.Exists(Path.Combine(_root, "out")).ShouldBeFalse();
    }

    [Fact]
    public void Run_WhenASummaryFileIsNamed_ShouldKeepTheSummaryThere()
    {
        // Arrange
        Put("src/twice.f", Source);
        Put("fortrania.json", Settings);

        // Act
        var code = Run("translate", "csharp", "--name", "Example.Toolkit", "--summary", "kept/toolkit.json");

        // Assert
        code.ShouldBe(CommandLine.Done, _error.ToString());
        File.ReadAllText(Path.Combine(_root, "kept/toolkit.json")).ShouldContain("\"project\": \"Example.Toolkit\"");
        File.Exists(Path.Combine(_root, "Example.Toolkit.fortrania-summary.json")).ShouldBeFalse();
    }

    [Fact]
    public void Run_WhenAReferenceIsASummaryFile_ShouldTranslateAgainstIt()
    {
        // Arrange: a library translated on its own, then a program that
        // calls into it, against its summary.
        Put("lib/twice.f", Source);
        Put("lib/fortrania.json", """{ "output": ".", "sources": [ { "name": "Core", "files": [ "*.f" ] } ] }""");
        Put("app/main.f", "      PROGRAM APP\n      INTEGER N\n      N = 4\n      CALL TWICE ( N )\n      END\n");
        Put("app/fortrania.json", """{ "output": ".", "sources": [ { "name": "AppMain", "files": [ "*.f" ] } ] }""");
        Run("translate", "csharp", "--name", "Example.Toolkit", "lib").ShouldBe(CommandLine.Done, _error.ToString());

        // Act
        var code = Run("translate", "csharp", "--name", "Example.App", "app", "--reference", "lib/Example.Toolkit.fortrania-summary.json");

        // Assert
        code.ShouldBe(CommandLine.Done, _error.ToString());
        _error.ToString().ShouldBeEmpty();
        File.ReadAllText(Path.Combine(_root, "app/Generated/AppMain/APP.g.cs")).ShouldContain("TWICE(");
    }

    [Fact]
    public void Run_WhenAReferenceIsNotAnAssembly_ShouldSayWhichAndFail()
    {
        // Arrange
        Put("src/twice.f", Source);
        Put("fortrania.json", Settings);
        Put("lib/not-an-assembly.dll", "text");

        // Act
        var code = Run("translate", "csharp", "--name", "Example.Toolkit", "--reference", "lib/not-an-assembly.dll");

        // Assert
        code.ShouldBe(CommandLine.Failed);
        _error.ToString().ShouldContain(Path.Combine(_root, "lib", "not-an-assembly.dll") + ": error:");
    }

    [Fact]
    public void Run_WhenAReferenceHasNoPath_ShouldShowHowToUseItAndSaySo()
    {
        // Act
        var code = Run("translate", "csharp", "--name", "Example.Toolkit", "--reference");

        // Assert
        code.ShouldBe(CommandLine.Misused);
        _error.ToString().ShouldContain("--reference is to be followed by the path of an assembly or of a summary file.");
    }

    [Fact]
    public void Run_WhenRestoreIsGivenAnOptionOfTranslate_ShouldSaySo()
    {
        // Act
        var code = Run("restore", "--force");

        // Assert
        code.ShouldBe(CommandLine.Misused);
        _error.ToString().ShouldContain("--force is not an option of restore.");
    }

    [Fact]
    public void Run_WhenAskedForHelp_ShouldShowHowToUseItAndSucceed()
    {
        // Act
        var code = Run("--help");

        // Assert
        code.ShouldBe(CommandLine.Done);
        _output.ToString().ShouldContain("Usage: fortrania translate csharp");
    }

    [Fact]
    public void Run_WhenAskedForItsVersion_ShouldGiveTheVersionItWasBuiltAs()
    {
        // Act
        var code = Run("--version");

        // Assert
        code.ShouldBe(CommandLine.Done);
        _output.ToString().ShouldMatch(@"^\d+\.\d+\.\d+");
    }

    private int Run(params string[] arguments) => RunIn(_root, arguments);

    private int RunIn(string workingDirectory, params string[] arguments) => CommandLine.Run(arguments, _output, _error, workingDirectory);

    private void Put(string path, string text)
    {
        var full = Path.Combine(_root, path);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, text);
    }
}
