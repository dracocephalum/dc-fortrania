using Shouldly;

namespace Dracocephalum.Spice.Translation.Tests;

public class PlatformRewriteTests
{
    // Made up for the test, in the shape the translator gives a table that
    // is filled once: one store of a constant to each place.
    private const string Table =
        """
        public void TABLE(byte* key, int key_len)
        {
            if (first)
            {
                Assign(names, 32, AddrOf("SYSTEM"u8), 6);
                Assign(values, 32, AddrOf("PC"u8), 2);
                Assign(values + 1 * 32, 32, AddrOf("LINUX"u8), 5);
                Assign(values + 2 * 32, 32, AddrOf("GFORTRAN/64BIT"u8), 14);
                Assign(values + 3 * 32, 32, AddrOf("LTL-IEEE"u8), 8);
                Assign(values + 4 * 32, 32, AddrOf("LF"u8), 2);
                Assign(values + 5 * 32, 32, AddrOf("BIG-IEEE LTL-IEEE"u8), 17);
            }
        }
        """;

    [Fact]
    public void Rewrite_WhenTheTableHoldsTheFactsOfThePlatformTranslated_ShouldChooseEachWhenItRuns()
    {
        // Act
        var rewritten = PlatformRewrite.Rewrite(Table.ReplaceLineEndings("\n"));

        // Assert
        rewritten.ShouldContain(
            """
                    if (_io.NewLine == "\r\n")
                    {
                        Assign(values + 4 * 32, 32, AddrOf("CR-LF"u8), 5);
                    }
                    else if (_io.NewLine == "\r")
                    {
                        Assign(values + 4 * 32, 32, AddrOf("CR"u8), 2);
                    }
                    else
                    {
                        Assign(values + 4 * 32, 32, AddrOf("LF"u8), 2);
                    }
            """.ReplaceLineEndings("\n"),
            Case.Sensitive);
        rewritten.ShouldContain("if (BitConverter.IsLittleEndian)\n        {\n            Assign(values + 3 * 32, 32, AddrOf(\"LTL-IEEE\"u8), 8);");
        rewritten.ShouldContain("Assign(values + 3 * 32, 32, AddrOf(\"BIG-IEEE\"u8), 8);");
        rewritten.ShouldContain("if (OperatingSystem.IsWindows())\n        {\n            Assign(values + 1 * 32, 32, AddrOf(\"MICROSOFT WINDOWS\"u8), 17);");
        rewritten.ShouldContain("AddrOf(\"FORTRANIA/64BIT\"u8), 15);");
        rewritten.ShouldContain("AddrOf(\"INTEL MAC\"u8), 9);");
        rewritten.ShouldNotContain("GFORTRAN");
    }

    [Fact]
    public void Rewrite_WhenAValueStandsForNoFactOfThePlatform_ShouldLeaveItAlone()
    {
        // Act
        var rewritten = PlatformRewrite.Rewrite(Table.ReplaceLineEndings("\n"));

        // Assert
        rewritten.ShouldContain("Assign(names, 32, AddrOf(\"SYSTEM\"u8), 6);");
        rewritten.ShouldContain("Assign(values + 5 * 32, 32, AddrOf(\"BIG-IEEE LTL-IEEE\"u8), 17);");
    }

    [Theory]
    [InlineData("Assign(values + 4 * 32, 32, AddrOf(\"LF\"u8), 2);", "")]
    [InlineData("Assign(values + 4 * 32, 32, AddrOf(\"LF\"u8), 2);", "Assign(values + 4 * 32, 32, AddrOf(\"LF\"u8), 2);\n        Assign(values + 4 * 32, 32, AddrOf(\"LF\"u8), 2);")]
    public void Rewrite_WhenAFactIsNotStoredOnce_ShouldRefuseAndSaySo(string store, string replacement)
    {
        // Act & Assert
        Should.Throw<RewriteException>(() => PlatformRewrite.Rewrite(Table.ReplaceLineEndings("\n").Replace(store, replacement, StringComparison.Ordinal)))
            .Message.ShouldContain("'LF'");
    }
}
