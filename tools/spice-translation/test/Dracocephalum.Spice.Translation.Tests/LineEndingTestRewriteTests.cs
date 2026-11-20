using Shouldly;

namespace Dracocephalum.Spice.Translation.Tests;

public class LineEndingTestRewriteTests
{
    // Made up for the test, in the shape the translator gives a list of
    // expected refusals: every flag cleared, then the refused ones set and
    // given their message.
    private const string Family =
        """
        public void FAMILY()
        {
            int* myfnef = stackalloc int[12];
            byte* myfnem = stackalloc byte[384];
            for (i = 1; i <= 12; i++)
            {
                myfnef[i - 1] = 0;
                Assign(myfnem + (i - 1) * 32, 32, AddrOf(" "u8), 1);
            }

            // The refused kernels.
            myfnef[0] = 1;
            myfnef[2] = 1;
            myfnef[3] = 1;
            myfnef[5] = 1;
            myfnef[6] = 1;
            myfnef[8] = 1;
            Assign(myfnem, 32, AddrOf("SPICE(INCOMPATIBLEEOL)"u8), 22);
            Assign(myfnem + 2 * 32, 32, AddrOf("SPICE(INCOMPATIBLEEOL)"u8), 22);
            Assign(myfnem + 3 * 32, 32, AddrOf("SPICE(INCOMPATIBLEEOL)"u8), 22);
            Assign(myfnem + 5 * 32, 32, AddrOf("SPICE(INCOMPATIBLEEOL)"u8), 22);
            Assign(myfnem + 6 * 32, 32, AddrOf("SPICE(INCOMPATIBLEEOL)"u8), 22);
            Assign(myfnem + 8 * 32, 32, AddrOf("SPICE(INCOMPATIBLEEOL)"u8), 22);
            CHECK(myfnef, myfnem);
        }
        """;

    [Fact]
    public void Rewrite_WhenTheFamilyExpectsTheRefusalsOfLinux_ShouldChooseThemFromTheLineEndingWhenItRuns()
    {
        // Act
        var rewritten = LineEndingTestRewrite.Rewrite(Family.ReplaceLineEndings("\n"));

        // Assert
        rewritten.ShouldBe(
            """
            public void FAMILY()
            {
                int* myfnef = stackalloc int[12];
                byte* myfnem = stackalloc byte[384];
                for (i = 1; i <= 12; i++)
                {
                    myfnef[i - 1] = 0;
                    Assign(myfnem + (i - 1) * 32, 32, AddrOf(" "u8), 1);
                }

                // The refused kernels.
                {
                    // Refused: each kernel whose lines do not end the way this program writes them.
                    var _native = _io.NewLine == "\r\n" ? 2 : _io.NewLine == "\r" ? 0 : 1;
                    for (var _kernel = 0; _kernel < 9; _kernel++)
                    {
                        if (_kernel % 3 != _native)
                        {
                            myfnef[_kernel] = 1;
                            Assign(myfnem + _kernel * 32, 32, AddrOf("SPICE(INCOMPATIBLEEOL)"u8), 22);
                        }
                    }
                }
                CHECK(myfnef, myfnem);
            }
            """.ReplaceLineEndings("\n"));
    }

    [Theory]
    [InlineData("    myfnef[2] = 1;\n", "", "[0, 3, 5, 6, 8]")]
    [InlineData("    myfnef[2] = 1;\n", "    myfnef[1] = 1;\n", "[0, 1, 3, 5, 6, 8]")]
    [InlineData("    Assign(myfnem + 8 * 32, 32, AddrOf(\"SPICE(INCOMPATIBLEEOL)\"u8), 22);\n", "", "[0, 2, 3, 5, 6]")]
    public void Rewrite_WhenTheFamilyExpectsOtherRefusals_ShouldRefuseAndSaySo(string store, string replacement, string found)
    {
        // Arrange
        var changed = Family.ReplaceLineEndings("\n").Replace(store, replacement, StringComparison.Ordinal);

        // Act & Assert
        Should.Throw<RewriteException>(() => LineEndingTestRewrite.Rewrite(changed))
            .Message.ShouldContain(found);
    }
}
