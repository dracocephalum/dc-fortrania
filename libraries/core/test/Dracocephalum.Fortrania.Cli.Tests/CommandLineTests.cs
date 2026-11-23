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
        var code = Run("cs");

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
        var code = RunIn(Path.Combine(_root, "elsewhere"), "cs", "../project/settings.json");

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
        Run("cs").ShouldBe(CommandLine.Done);

        // Act
        Run("cs").ShouldBe(CommandLine.Done);
        var again = _output.ToString();
        Run("cs", "--force").ShouldBe(CommandLine.Done);
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
        var code = Run("cs");

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
        var code = Run("cs");

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
        var code = Run("cs");

        // Assert
        code.ShouldBe(CommandLine.Failed);
        _error.ToString().ShouldStartWith(Path.Combine(_root, "fortrania.json") + ": error:");
    }

    [Theory]
    [InlineData]
    [InlineData("vb")]
    [InlineData("cs", "--quick")]
    [InlineData("cs", "a.json", "b.json")]
    public void Run_WhenTheCommandLineIsNotOneItTakes_ShouldShowHowToUseItAndSaySo(params string[] arguments)
    {
        // Act
        var code = Run(arguments);

        // Assert
        code.ShouldBe(CommandLine.Misused);
        _error.ToString().ShouldContain("Usage: fortrania cs");
    }

    [Fact]
    public void Run_WhenAskedForHelp_ShouldShowHowToUseItAndSucceed()
    {
        // Act
        var code = Run("--help");

        // Assert
        code.ShouldBe(CommandLine.Done);
        _output.ToString().ShouldContain("Usage: fortrania cs");
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
