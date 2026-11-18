using Dracocephalum.Fortrania.Analysis;
using Shouldly;

namespace Dracocephalum.Fortrania.Tests.Emit;

/// <summary>
/// Runs the translated sample of variables that share storage. Every
/// expected value is what the reference compiler's program printed for the
/// same source and arguments.
/// </summary>
public class CSharpEmitterEquivalenceTests
{
    private const string Driver =
        """
            public static int[] Pun()
            {
                using var library = new Library();
                double one = 1.0;
                var results = new int[2];
                fixed (int* first = results)
                {
                    library.PUN(&one, first);
                }

                return results;
            }

            public static int[] Shared()
            {
                using var library = new Library();
                int five = 5;
                int one = 1;
                return [library.SHARED(&five), library.SHARED(&one)];
            }

            public static int[] Characters()
            {
                using var library = new Library();
                var results = new int[4];
                fixed (int* first = results)
                {
                    library.CHARS(first);
                }

                return results;
            }
        """;

    private static readonly Lazy<TranslatedProgram> Program = new(() => TranslatedProgram.Compile(EquivalenceSample.Source, Driver));

    [Fact]
    public void Run_WhenANumberSharesStorageWithNumbersOfAnotherType_ShouldShowEachTheOthersBytes() =>
        Program.Value.Run<int[]>("Pun").ShouldBe([1072693248, 1]);

    [Fact]
    public void Run_WhenSavedArraysShareStorageAtAnOffset_ShouldKeepThemTogetherBetweenCalls() =>
        Program.Value.Run<int[]>("Shared").ShouldBe([10031201, 12032201]);

    [Fact]
    public void Run_WhenCharacterVariablesShareStorage_ShouldPlaceEachAtItsByte() =>
        Program.Value.Run<int[]>("Characters").ShouldBe([69, 120, 121, 70]);

    [Fact]
    public void Emit_WhenNoVariableOfASetIsKept_ShouldTakeTheSetFromTheStackAndPointEachVariableIntoIt()
    {
        // Act
        var file = TranslatedProgram.Translate(EquivalenceSample.Source)[1];

        // Assert
        file.ShouldContain(
            """
                    byte* _e1 = stackalloc byte[8];
                    double* dequiv = (double*)(_e1 + 0);
                    int* iequiv = (int*)(_e1 + 0);
                    *dequiv = *d;
            """.ReplaceLineEndings("\n"),
            Case.Sensitive);
    }

    [Fact]
    public void Emit_WhenAVariableOfASetIsKept_ShouldPlaceTheWholeSetInTheUnitsBlock()
    {
        // Act
        var file = TranslatedProgram.Translate(EquivalenceSample.Source)[2];

        // Assert
        file.ShouldContain("byte* _state = _memory.Allocate(16);");
        file.ShouldContain(
            """
                    int* a = (int*)(_state + 0);
                    int* b = (int*)(_state + 4);
                    int* c = (int*)(_state + 12);
            """.ReplaceLineEndings("\n"),
            Case.Sensitive);
    }

    [Fact]
    public void Emit_WhenTwoListsDisagreeOnWhereAVariableLies_ShouldRefuse()
    {
        // Arrange
        const string Source =
            """
                  SUBROUTINE S ( N )
                  INTEGER N, A ( 4 ), B ( 4 )
                  EQUIVALENCE ( A ( 1 ), B ( 1 ) ), ( A ( 2 ), B ( 3 ) )
                  N = A ( 1 )
                  END
            """;

        // Act & Assert
        Should.Throw<FortranAnalysisException>(() => TranslatedProgram.Translate(Source))
            .Message.ShouldContain("EQUIVALENCE puts B in two places.");
    }
}
