using Dracocephalum.Fortrania.Emit;
using Shouldly;

namespace Dracocephalum.Fortrania.Tests.Emit;

/// <summary>
/// Runs the translated sample of statement functions. Every expected value
/// is what the reference compiler's program printed for the same source
/// and arguments.
/// </summary>
public class CSharpEmitterStatementFunctionTests
{
    private const string Driver =
        """
            public static int[] Run()
            {
                using var library = new Library();
                var results = new int[15];
                int n = 3;
                int k = 1000;
                fixed (int* first = results)
                {
                    results[12] = library.SFUNCS(&n, &k, first);
                    int* again = stackalloc int[12];
                    library.SFUNCS(&n, &k, again);
                    results[13] = again[4];
                    results[14] = again[6];
                }

                return results;
            }

            public static int[] Entries()
            {
                using var library = new Library();
                int four = 4;
                int first = 0;
                int second = 0;
                library.SFA(&four, &first);
                library.SFB(&four, &second);
                return [first, second];
            }
        """;

    private static readonly Lazy<TranslatedProgram> Program = new(() => TranslatedProgram.Compile(StatementFunctionSample.Source, Driver));

    [Fact]
    public void Run_WhenStatementFunctionsUseArgumentsVariablesEachOtherAndProcedures_ShouldGiveWhatTheirExpressionsDo() =>
        Program.Value.Run<int[]>("Run").ShouldBe([27, 64, 1, 35, 1105, 63, 1110, 18, 135, 7, 1, 153, 512, 1106, 1111]);

    [Fact]
    public void Run_WhenEntryPointsReferToAStatementFunction_ShouldHaveItInEachMethodThatDoes() =>
        Program.Value.Run<int[]>("Entries").ShouldBe([17, 43]);

    [Fact]
    public void Emit_WhenAStatementFunctionNamesVariablesOfItsUnit_ShouldPassThemSoThatNothingIsCaptured()
    {
        // Arrange
        const string Source =
            """
                  INTEGER FUNCTION TWICE ( M )
                  INTEGER M
                  TWICE = M * 2
                  END

                  SUBROUTINE USES ( N, R )
                  INTEGER N, R, I, LOCAL, PURE, MIXED, CALLS, TWICE
                  PURE ( I ) = I * I
                  MIXED ( I ) = PURE ( I ) + N + LOCAL
                  CALLS ( I ) = TWICE ( I ) + TWICE ( I + 1 )
                  LOCAL = 5
                  R = MIXED ( 2 ) + CALLS ( R )
                  END
            """;

        // Act
        var file = TranslatedProgram.Translate(Source)[1];

        // Assert
        file.ShouldContain(
            """
                public void USES(int* n, int* r)
                {
                    int local = 0;
                    static int pure(int i) => i * i;
                    static int mixed(int i, int* n, int local) => pure(i) + *n + local;
                    int calls(int i)
                    {
                        int _i1 = 0;
                        return TWICE(&i) + TWICE(Arg(&_i1, i + 1));
                    }
                    local = 5;
                    *r = mixed(2, n, local) + calls(*r);
                }
            """.ReplaceLineEndings("\n"),
            Case.Sensitive);
    }

    [Fact]
    public void Emit_WhenAStatementFunctionIsNotReferredTo_ShouldWriteNothingForIt()
    {
        // Arrange
        const string Source =
            """
                  SUBROUTINE UNUSED ( N )
                  INTEGER N, I, SQ
                  SQ ( I ) = I * I
                  N = 1
                  END
            """;

        // Act
        var file = TranslatedProgram.Translate(Source)[0];

        // Assert
        file.ShouldNotContain("sq");
    }

    [Theory]
    [InlineData("      SUBROUTINE S ( C )|      CHARACTER*4 C, F, D|      F ( D ) = D|      C = F ( C )|      END", "Line 3: a statement function of type CHARACTER cannot be translated yet.")]
    [InlineData("      SUBROUTINE S ( N, D )|      INTEGER N, F|      CHARACTER*(*) D|      F ( D ) = ICHAR ( D )|      N = F ( 'A' )|      END", "Line 4: a statement function with an argument of assumed length cannot be translated yet.")]
    [InlineData("      SUBROUTINE S ( N )|      INTEGER N, F, G|      LOGICAL L|      EXTERNAL G|      F ( L ) = G ( L )|      N = F ( .TRUE. )|      END", "Line 5: a logical argument of a statement function passed on to a procedure cannot be translated yet.")]
    [InlineData(
        "      SUBROUTINE S ( N )|      INTEGER N, K, F, G|      F ( N ) = N + K|      G ( K ) = F ( K )|      N = G ( N )|      END",
        "Line 4: a statement function with an argument named as a variable that a statement function it refers to uses cannot be translated yet.")]
    public void Emit_WhenAStatementFunctionIsOfAKindNotTranslatedYet_ShouldSayWhatAndWhere(string source, string message)
    {
        // Act & Assert
        Should.Throw<UnsupportedConstructException>(() => TranslatedProgram.Translate(source.Replace('|', '\n')))
            .Message.ShouldBe(message);
    }
}
