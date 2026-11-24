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
          "output": "out",
          "runtime": { "package": "1.2.3" },
          "projects": [ { "name": "Example.Toolkit", "sources": [ { "name": "Core", "files": [ "src/*.f" ] } ] } ]
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
        var code = Run("translate", "csharp");

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
        var code = RunIn(Path.Combine(_root, "elsewhere"), "translate", "csharp", "../project/settings.json");

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
        Run("translate", "csharp").ShouldBe(CommandLine.Done);

        // Act
        Run("translate", "csharp").ShouldBe(CommandLine.Done);
        var again = _output.ToString();
        Run("translate", "csharp", "--force").ShouldBe(CommandLine.Done);
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
        var code = Run("translate", "csharp");

        // Assert
        code.ShouldBe(CommandLine.Failed);
        _error.ToString().ShouldContain(Path.Combine("src", "twice.f") + ": error: Line 1:");
        _output.ToString().ShouldStartWith("Not translated:");
    }

    [Theory]
    [InlineData]
    [InlineData("a.fortrania.json", "b.fortrania.json")]
    public void Run_WhenTheFolderHasNoneOrSeveralSettingsFiles_ShouldSayToNameOneAndFail(params string[] files)
    {
        // Arrange
        foreach (var file in files)
        {
            Put(file, Settings);
        }

        // Act
        var code = Run("translate", "csharp");

        // Assert
        code.ShouldBe(CommandLine.Failed);
        _error.ToString().ShouldContain("name the");
    }

    [Fact]
    public void Run_WhenTheSettingsAreNotSettings_ShouldSayWhichFileAndFail()
    {
        // Arrange
        Put("fortrania.json", """{ "projects": [], "colour": "red" }""");

        // Act
        var code = Run("translate", "csharp");

        // Assert
        code.ShouldBe(CommandLine.Failed);
        _error.ToString().ShouldStartWith(Path.Combine(_root, "fortrania.json") + ": error:");
    }

    [Theory]
    [InlineData]
    [InlineData("vb")]
    [InlineData("translate", "csharp", "--quick")]
    [InlineData("translate", "csharp", "a.json", "b.json")]
    public void Run_WhenTheCommandLineIsNotOneItTakes_ShouldShowHowToUseItAndSaySo(params string[] arguments)
    {
        // Act
        var code = Run(arguments);

        // Assert
        code.ShouldBe(CommandLine.Misused);
        _error.ToString().ShouldContain("Usage: fortrania translate csharp");
    }

    [Fact]
    public void Run_WhenToldOneProject_ShouldTranslateItAloneAndReadTheOtherFromItsSummary()
    {
        // Arrange: a library and a program that calls it, translated
        // together once; then the library's sources go.
        Put("lib/twice.f", Source);
        Put("app/main.f", "      PROGRAM APP\n      INTEGER N\n      N = 4\n      CALL TWICE ( N )\n      END\n");
        Put(
            "fortrania.json",
            """
            {
              "output": "out",
              "runtime": { "package": "1.2.3" },
              "projects": [
                { "name": "Example.Toolkit", "sources": [ { "name": "Core", "files": [ "lib/*.f" ] } ] },
                { "name": "Example.App", "sources": [ { "name": "AppMain", "files": [ "app/*.f" ] } ] }
              ]
            }
            """);
        Run("translate", "csharp").ShouldBe(CommandLine.Done, _error.ToString());
        File.Delete(Path.Combine(_root, "lib/twice.f"));
        var before = _output.ToString().Length;

        // Act
        var code = Run("translate", "csharp", "--project", "Example.App", "--force");

        // Assert
        code.ShouldBe(CommandLine.Done, _error.ToString());
        _output.ToString()[before..].ShouldStartWith("Translated: 1 program units,");
    }

    [Theory]
    [InlineData("translate", "csharp", "--project")]
    [InlineData("translate", "csharp", "--project", "--force")]
    public void Run_WhenToldOfAProjectWithoutItsName_ShouldShowHowToUseItAndSaySo(params string[] arguments)
    {
        // Act
        var code = Run(arguments);

        // Assert
        code.ShouldBe(CommandLine.Misused);
        _error.ToString().ShouldContain("--project is to be followed by the name");
    }

    [Fact]
    public void Run_WhenToldAProjectTheSettingsLack_ShouldSaySoAndFail()
    {
        // Arrange
        Put("src/twice.f", Source);
        Put("fortrania.json", Settings);

        // Act
        var code = Run("translate", "csharp", "--project", "Example.Other");

        // Assert
        code.ShouldBe(CommandLine.Failed);
        _error.ToString().ShouldContain("the project Example.Other to translate");
    }

    [Theory]
    [InlineData("t", "cs")]
    [InlineData("tr", "c", "--f")]
    [InlineData("translate", "csh", "--fo")]
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
        var code = RunIn(Path.Combine(_root, "deep"), "translate", "csharp");

        // Assert
        code.ShouldBe(CommandLine.Failed);
        _error.ToString().ShouldContain("there is no fortrania.json or *.fortrania.json in");
    }

    [Fact]
    public void Run_WhenAFolderIsNamedForTheSettings_ShouldTakeTheOneInIt()
    {
        // Arrange: the settings beside the sources, named by their folder
        // from elsewhere, as a build names its project's.
        Put("project/src/twice.f", Source);
        Put("project/my.fortrania.json", Settings);
        Directory.CreateDirectory(Path.Combine(_root, "elsewhere"));

        // Act
        var code = RunIn(Path.Combine(_root, "elsewhere"), "translate", "csharp", "../project");

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
        Run("translate", "csharp").ShouldBe(CommandLine.Done);
        File.Delete(Path.Combine(_root, "src/twice.f"));
        var before = _output.ToString().Length;

        // Act
        var offline = Run("translate", "csharp", "--offline");
        var online = Run("translate", "csharp");

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
        var code = Run("translate", "csharp", "--offline");

        // Assert
        code.ShouldBe(CommandLine.Failed);
        _error.ToString().ShouldContain("restore them first");
    }

    [Theory]
    [InlineData("restore")]
    [InlineData("restore", "--if-needed")]
    [InlineData("res", "--project", "Example.Toolkit", "--if")]
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
    public void Run_WhenToldToRecord_ShouldWriteWhatWasReadMatchedAndMade()
    {
        // Arrange
        Put("src/twice.f", Source);
        Put("fortrania.json", Settings);

        // Act
        var code = Run("translate", "csharp", "--record", "obj/fortrania.record");

        // Assert
        code.ShouldBe(CommandLine.Done, _error.ToString());
        File.ReadAllLines(Path.Combine(_root, "obj/fortrania.record")).ShouldBe(
            [
                "read " + Path.Combine(_root, "fortrania.json"),
                "read " + Path.GetFullPath(Path.Combine(_root, "src/twice.f")),
                "pattern " + Path.GetFullPath(Path.Combine(_root, "src/*.f")),
                "file " + Path.GetFullPath(Path.Combine(_root, "out/Example.Toolkit/Generated/Core/TWICE.g.cs")),
                "file " + Path.GetFullPath(Path.Combine(_root, "out/Example.Toolkit/Generated/Core/Core.g.cs")),
                "file " + Path.GetFullPath(Path.Combine(_root, "out/Example.Toolkit/Generated/.fortrania-summary.json")),
            ]);
    }

    [Fact]
    public void Run_WhenATranslationToBeRecordedFails_ShouldRemoveTheLastRecord()
    {
        // Arrange: a record of a translation that was done, then its
        // sources gone.
        Put("src/twice.f", Source);
        Put("fortrania.json", Settings);
        Run("translate", "csharp", "--record", "fortrania.record").ShouldBe(CommandLine.Done);
        File.Delete(Path.Combine(_root, "src/twice.f"));

        // Act
        var code = Run("translate", "csharp", "--record", "fortrania.record");

        // Assert
        code.ShouldBe(CommandLine.Failed);
        File.Exists(Path.Combine(_root, "fortrania.record")).ShouldBeFalse();
    }

    [Fact]
    public void Run_WhenTheRecordIsNamedTwice_ShouldSaySo()
    {
        // Act
        var code = Run("translate", "csharp", "--record", "a", "--record", "b");

        // Assert
        code.ShouldBe(CommandLine.Misused);
        _error.ToString().ShouldContain("--record is given twice; it takes one.");
    }

    [Fact]
    public void Run_WhenAReferenceIsNotAnAssembly_ShouldSayWhichAndFail()
    {
        // Arrange
        Put("src/twice.f", Source);
        Put("fortrania.json", Settings);
        Put("lib/not-an-assembly.dll", "text");

        // Act
        var code = Run("translate", "csharp", "--reference", "lib/not-an-assembly.dll");

        // Assert
        code.ShouldBe(CommandLine.Failed);
        _error.ToString().ShouldContain(Path.Combine(_root, "lib", "not-an-assembly.dll") + ": error:");
    }

    [Fact]
    public void Run_WhenAReferenceHasNoPath_ShouldShowHowToUseItAndSaySo()
    {
        // Act
        var code = Run("translate", "csharp", "--reference");

        // Assert
        code.ShouldBe(CommandLine.Misused);
        _error.ToString().ShouldContain("--reference is to be followed by the path of an assembly.");
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
