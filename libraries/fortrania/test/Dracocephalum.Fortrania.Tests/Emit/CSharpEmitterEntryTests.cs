using Shouldly;

namespace Dracocephalum.Fortrania.Tests.Emit;

/// <summary>
/// Runs the translated sample of units with <c>ENTRY</c> statements. Every
/// expected value is what the reference compiler's program printed for the
/// same source and arguments, but where a test says otherwise.
/// </summary>
public class CSharpEmitterEntryTests
{
    private const string Driver =
        """
            private static void Push(Library library, int item) => library.PUSH(&item);

            private static int Pop(Library library)
            {
                int item = 0;
                library.POP(&item);
                return item;
            }

            private static int Depth(Library library)
            {
                int count = 0;
                library.DEPTH(&count);
                return count;
            }

            public static int[] Stack()
            {
                using var library = new Library();
                Push(library, 4);
                Push(library, 9);
                Push(library, 16);
                var results = new int[4];
                results[0] = Depth(library);
                results[1] = Pop(library);
                int item = 0;
                int count = 0;
                library.STACK(&item, &count);
                results[2] = Depth(library);
                results[3] = Pop(library);
                return results;
            }

            public static double[] Functions()
            {
                using var library = new Library();
                double two = 2.0;
                double three = 3.0;
                return [library.AREA(&two), library.SQUARE(&three), library.SETK(&two), library.AREA(&two), library.SQUARE(&three)];
            }

            public static int[] RunningOn()
            {
                using var library = new Library();
                int twice = 3;
                int once = 3;
                library.TWICE(&twice);
                library.ONCE(&once);
                return [twice, once];
            }

            private static int[] Strays(bool taken)
            {
                using var library = new Library();
                int flag = taken ? 1 : 0;
                int first = 0;
                int other = 0;
                int third = 0;
                library.FIRST(&first, &flag);
                fixed (byte* name = "ABCD"u8)
                {
                    library.OTHER(&other, name, 4, &flag);
                }

                library.THIRD(&third, &flag);
                return [first, other, third];
            }

            public static int[] StraysNotReached() => Strays(false);

            public static int[] StraysReached() => Strays(true);

            public static string Greetings()
            {
                using var library = new Library();
                byte* text = stackalloc byte[24];
                fixed (byte* bob = "BOB"u8, alice = "ALICE"u8)
                {
                    library.GREET(text, 12, bob, 3);
                    library.FAREWL(text + 12, 12, alice, 5);
                }

                return System.Text.Encoding.ASCII.GetString(text, 24);
            }
        """;

    private static readonly Lazy<TranslatedProgram> Program = new(() => TranslatedProgram.Compile(EntrySample.Source, Driver));

    [Fact]
    public void Run_WhenEntryPointsShareSavedVariables_ShouldLetEachWorkOnWhatTheOthersStored() =>
        Program.Value.Run<int[]>("Stack").ShouldBe([3, 16, 2, 9]);

    [Fact]
    public void Run_WhenAFunctionHasEntryPoints_ShouldReturnEachOnesResultWhicheverNameItWasStoredUnder() =>
        Program.Value.Run<double[]>("Functions").ShouldBe([12.0, 9.0, 2.5, 24.0, 18.0]);

    [Fact]
    public void Run_WhenControlRunsOnIntoTheNextEntryPoint_ShouldCarryOnThroughItsStatements() =>
        Program.Value.Run<int[]>("RunningOn").ShouldBe([7, 4]);

    [Fact]
    public void Run_WhenABranchNotTakenNamesAnotherEntryPointsArgument_ShouldRunAsIfItWereNotThere() =>
        Program.Value.Run<int[]>("StraysNotReached").ShouldBe([1, 6, 3]);

    // The language does not say what happens here, and the reference
    // compiler's program follows an address nobody set. The translation
    // has a zero or a blank of its own for the argument.
    [Fact]
    public void Run_WhenABranchTakenNamesAnotherEntryPointsArgument_ShouldWorkOnAValueOfItsOwn() =>
        Program.Value.Run<int[]>("StraysReached").ShouldBe([1, 6, 8]);

    [Fact]
    public void Run_WhenACharacterFunctionHasEntryPoints_ShouldFillThePlaceEachIsHanded() =>
        Program.Value.Run<string>("Greetings").ShouldBe("HELLO BOB   BYE ALICE   ");

    [Fact]
    public void Emit_WhenAUnitHasEntryPoints_ShouldWriteAMethodForEachThatDeclaresOnlyWhatItNames()
    {
        // Arrange
        const string Source =
            """
                  SUBROUTINE KEEP ( VALUE )
                  INTEGER VALUE, LAST, I, WORK ( 8 )
                  SAVE LAST
                  RETURN

                  ENTRY PUT ( VALUE )
                  LAST = VALUE
                  RETURN

                  ENTRY GET ( VALUE )
                  DO I = 1, 8
                     WORK ( I ) = LAST
                  END DO
                  VALUE = WORK ( 8 )
                  RETURN
                  END
            """;

        // Act
        var file = TranslatedProgram.Translate(Source)[0];

        // Assert
        file.ShouldContain(
            """
                public void KEEP(int* value)
                {
                    return;
                }

                public void PUT(int* value)
                {
                    byte* _state = _state_KEEP;
                    if (_state == null)
                    {
                        _state = _load_KEEP();
                    }
                    int* last = (int*)(_state + 0);
                    *last = *value;
                    return;
                }

                public void GET(int* value)
                {
                    byte* _state = _state_KEEP;
                    if (_state == null)
                    {
                        _state = _load_KEEP();
                    }
                    int* last = (int*)(_state + 0);
                    int i = 0;
                    int* work = stackalloc int[8];
                    for (i = 1; i <= 8; i += 1)
                    {
                        work[i - 1] = *last;
                    }
                    *value = work[7];
                    return;
                }
            }
            """.ReplaceLineEndings("\n"),
            Case.Sensitive);
    }

    [Fact]
    public void Emit_WhenAnEntryPointsResultIsUsedThroughANameOfAnotherType_ShouldRefuse()
    {
        // Arrange
        const string Source =
            """
                  INTEGER FUNCTION WHOLE ( X )
                  REAL X, PART
                  WHOLE = INT ( X )
                  RETURN

                  ENTRY PART ( X )
                  WHOLE = INT ( X )
                  RETURN
                  END
            """;

        // Act & Assert
        Should.Throw<Dracocephalum.Fortrania.Emit.UnsupportedConstructException>(() => TranslatedProgram.Translate(Source))
            .Message.ShouldBe("Line 7: entry points whose results differ in type, one used through the name of another cannot be translated yet.");
    }
}
