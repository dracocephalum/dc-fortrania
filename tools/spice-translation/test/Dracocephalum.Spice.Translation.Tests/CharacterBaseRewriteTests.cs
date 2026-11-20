using Shouldly;

namespace Dracocephalum.Spice.Translation.Tests;

public class CharacterBaseRewriteTests
{
    // Made up for the test, in the shape the translator gives a function
    // that returns one constant.
    private const string Routine =
        """
        public int CHBASE()
        {
            int chbase = 0;

            // Values such as 128 are given for several machines.
            chbase = 128;

            return chbase;
        }
        """;

    [Fact]
    public void Rewrite_WhenTheRoutineStoresTheBaseTranslated_ShouldStoreTheRuntimesInItsPlace()
    {
        // Act
        var rewritten = CharacterBaseRewrite.Rewrite(Routine.ReplaceLineEndings("\n"));

        // Assert
        rewritten.ShouldBe(Routine.ReplaceLineEndings("\n").Replace("chbase = 128;", "chbase = 256;", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("chbase = 256;", "0 times")]
    [InlineData("chbase = 128;\n    chbase = 128;", "2 times")]
    public void Rewrite_WhenTheBaseIsNotStoredOnce_ShouldRefuseAndSaySo(string stores, string count)
    {
        // Act & Assert
        Should.Throw<RewriteException>(() => CharacterBaseRewrite.Rewrite(Routine.ReplaceLineEndings("\n").Replace("chbase = 128;", stores, StringComparison.Ordinal)))
            .Message.ShouldContain(count);
    }
}
