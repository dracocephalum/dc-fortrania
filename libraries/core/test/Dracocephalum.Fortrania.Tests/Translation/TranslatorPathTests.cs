using Dracocephalum.Fortrania.Translation;
using Shouldly;

namespace Dracocephalum.Fortrania.Tests.Translation;

public sealed class TranslatorPathTests : IDisposable
{
    // What an ignored file holds, which is not FORTRAN: read, it would be an
    // error.
    private const string NotFortran = "This is not FORTRAN.\n";

    private readonly string _root = Path.Combine(AppContext.BaseDirectory, "paths-" + Guid.NewGuid().ToString("N"));

    public TranslatorPathTests() => Directory.CreateDirectory(_root);

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public void Run_WhenAPathHasAnyFolders_ShouldFindItsFilesAtEveryDepth()
    {
        // Arrange
        Put("lib/a.f", Unit("A"));
        Put("lib/x/b.f", Unit("B"));
        Put("lib/x/y/c.f", Unit("C"));
        Put("lib/x/y/notes.txt", NotFortran);

        // Act
        var result = Translate(new FileSettings { Paths = ["lib/**/*.f"] });

        // Assert
        result.Succeeded.ShouldBeTrue(string.Join('\n', result.Messages));
        Generated().ShouldBe(["A.g.cs", "B.g.cs", "C.g.cs", "Core.g.cs"], ignoreOrder: true);
        result.Patterns.ShouldBe([Full("lib/**/*.f")]);
    }

    [Fact]
    public void Run_WhenAFolderOfAPathHasAWildcard_ShouldFindItsFilesInEachFolderThatMatches()
    {
        // Arrange
        Put("lib/one/a.f", Unit("A"));
        Put("lib/two/a.f", Unit("B"));
        Put("lib/other/a.f", NotFortran);
        Put("lib/one/deeper/a.f", NotFortran);

        // Act
        var result = Translate(new FileSettings { Paths = ["lib/?w?/a.f", "lib/o*e/a.f"] });

        // Assert
        result.Succeeded.ShouldBeTrue(string.Join('\n', result.Messages));
        Generated().ShouldBe(["A.g.cs", "B.g.cs", "Core.g.cs"], ignoreOrder: true);
    }

    [Fact]
    public void Run_WhenAPathEndsInAnyFolders_ShouldFindEveryFileBeneathIt()
    {
        // Arrange
        Put("lib/a.f", Unit("A"));
        Put("lib/x/y/b.for", Unit("B"));

        // Act
        var result = Translate(new FileSettings { Paths = ["lib/**"] });

        // Assert
        result.Succeeded.ShouldBeTrue(string.Join('\n', result.Messages));
        Generated().ShouldBe(["A.g.cs", "B.g.cs", "Core.g.cs"], ignoreOrder: true);
    }

    [Fact]
    public void Run_WhenIgnoresMatchFilesThePathsFind_ShouldLeaveThemUnreadAndSaySo()
    {
        // Arrange: a glob that ignores case, one said to be a glob, and a
        // regular expression, each against the path from the root, lib.
        Put("lib/a.f", Unit("A"));
        Put("lib/x/TEST_one.f", NotFortran);
        Put("lib/x/y/b.f", NotFortran);
        Put("lib/x/z/c.f", Unit("C"));
        Put("lib/draft.f", NotFortran);

        // Act
        var result = Translate(new FileSettings
        {
            Paths = ["lib/**/*.f"],
            Ignores = ["**/test_*.f", "glob:draft.f", "regex:^x/y/"],
        });

        // Assert
        result.Succeeded.ShouldBeTrue(string.Join('\n', result.Messages));
        Generated().ShouldBe(["A.g.cs", "C.g.cs", "Core.g.cs"], ignoreOrder: true);
        result.Ignored.ShouldBe([Full("lib/draft.f"), Full("lib/x/TEST_one.f"), Full("lib/x/y/b.f")], ignoreOrder: true);
        result.Read.ShouldNotContain(Full("lib/draft.f"));
    }

    [Fact]
    public void Run_WhenAGlobIgnoreHasNoFolder_ShouldMatchOnlyFilesAtTheRoot()
    {
        // Arrange: a glob matches the whole path from the root.
        Put("lib/test_a.f", NotFortran);
        Put("lib/x/test_b.f", Unit("B"));

        // Act
        var result = Translate(new FileSettings { Paths = ["lib/**/*.f"], Ignores = ["test_*.f"] });

        // Assert
        result.Succeeded.ShouldBeTrue(string.Join('\n', result.Messages));
        Generated().ShouldBe(["B.g.cs", "Core.g.cs"], ignoreOrder: true);
    }

    [Fact]
    public void Run_WhenEveryFileAPathFindsIsIgnored_ShouldNotSayItNamesNoFile()
    {
        // Arrange
        Put("lib/a.f", Unit("A"));
        Put("old/b.f", NotFortran);

        // Act
        var result = Translate(new FileSettings { Paths = ["lib/*.f", "old/*.f"], Ignores = ["b.f"] });

        // Assert
        result.Succeeded.ShouldBeTrue(string.Join('\n', result.Messages));
        result.Ignored.ShouldBe([Full("old/b.f")]);
    }

    [Theory]
    [InlineData("regex:(", "'(' is not a regular expression that can be run without backtracking")]
    [InlineData("regex:(a)\\1", "'(a)\\1' is not a regular expression that can be run without backtracking")]
    public void Run_WhenAnIgnoreIsNotARegularExpressionThatCanBeRun_ShouldRefuseBeforeReadingAnything(string ignore, string message)
    {
        // Arrange
        Put("lib/a.f", NotFortran);

        // Act
        var result = Translate(new FileSettings { Paths = ["lib/*.f"], Ignores = [ignore] });

        // Assert
        result.Succeeded.ShouldBeFalse();
        result.Messages.ShouldHaveSingleItem().Text.ShouldStartWith($"An ignore of the source Core of the project Example.Toolkit cannot be read: {message}");
    }

    private static string Unit(string name) =>
        $"""
              SUBROUTINE {name} ( N )
              INTEGER N
              N = 1
              END
        """;

    private TranslationResult Translate(FileSettings files) => Translator.Run(
        new TranslationProject
        {
            Name = "Example.Toolkit",
            Settings = [new TranslationSettings { Folder = _root, Output = "out", Sources = [new SourceSettings { Name = "Core", Files = [files] }] }],
            Summary = Path.Combine(_root, "out", ProjectSummary.GetFileName("Example.Toolkit")),
        },
        new TranslationOptions());

    private void Put(string path, string text)
    {
        var full = Path.Combine(_root, path);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, text);
    }

    private string Full(string path) => Path.GetFullPath(Path.Combine(_root, path));

    private string[] Generated() => [.. Directory.GetFiles(Path.Combine(_root, "out", Translator.GeneratedFolder, "Core")).Select(Path.GetFileName).OfType<string>()];
}
