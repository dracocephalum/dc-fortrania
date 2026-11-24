using Shouldly;

namespace Dracocephalum.Fortrania.Cli.Tests;

public class UniquePrefixTests
{
    private static readonly string[] Words = ["restore", "run", "translate", "test"];

    [Theory]
    [InlineData("run", "run")]
    [InlineData("tr", "translate")]
    [InlineData("tra", "translate")]
    [InlineData("res", "restore")]
    [InlineData("ru", "run")]
    public void Resolve_WhenTheTypedWordIsWholeOrAStartOfOneWordAlone_ShouldGiveThatWord(string typed, string expected)
    {
        // Act
        var word = UniquePrefix.Resolve(typed, Words, out var candidates);

        // Assert
        word.ShouldBe(expected);
        candidates.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("r", new[] { "restore", "run" })]
    [InlineData("t", new[] { "translate", "test" })]
    public void Resolve_WhenTheTypedWordStartsSeveral_ShouldGiveNoneAndEveryCandidate(string typed, string[] expected)
    {
        // Act
        var word = UniquePrefix.Resolve(typed, Words, out var candidates);

        // Assert
        word.ShouldBeNull();
        candidates.ShouldBe(expected);
    }

    [Theory]
    [InlineData("")]
    [InlineData("x")]
    [InlineData("translates")]
    [InlineData("Translate")]
    public void Resolve_WhenTheTypedWordStartsNone_ShouldGiveNoneAndNoCandidate(string typed)
    {
        // Act
        var word = UniquePrefix.Resolve(typed, Words, out var candidates);

        // Assert
        word.ShouldBeNull();
        candidates.ShouldBeEmpty();
    }

    [Fact]
    public void Resolve_WhenAWholeWordIsAlsoTheStartOfAnother_ShouldGiveTheWholeWord()
    {
        // Act
        var word = UniquePrefix.Resolve("run", ["run", "runtime"], out var candidates);

        // Assert
        word.ShouldBe("run");
        candidates.ShouldBeEmpty();
    }
}
