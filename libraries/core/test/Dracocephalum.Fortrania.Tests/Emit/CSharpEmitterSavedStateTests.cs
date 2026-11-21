using Shouldly;

namespace Dracocephalum.Fortrania.Tests.Emit;

/// <summary>
/// Runs the translated sample of kept variables. Every expected value is
/// what the reference compiler's program printed for the same source and
/// arguments, in a program that like each instance here starts afresh.
/// </summary>
public class CSharpEmitterSavedStateTests
{
    private const string Driver =
        """
            private static int Count(Library library, bool reset)
            {
                int flag = reset ? 1 : 0;
                return library.COUNTR(&flag);
            }

            public static int[] Counter()
            {
                using var library = new Standalone();
                return [Count(library, false), Count(library, false), Count(library, true), Count(library, false)];
            }

            public static int[] Tables()
            {
                using var library = new Standalone();
                var results = new int[14];
                fixed (int* first = results)
                {
                    library.TABLES(first);
                    int* again = stackalloc int[12];
                    library.TABLES(again);
                    results[12] = again[6];
                    results[13] = again[10];
                }

                return results;
            }

            public static double Large()
            {
                using var library = new Standalone();
                int count = 1000;
                return library.BIGSUM(&count);
            }

            public static int[] TwoInstances()
            {
                using var first = new Standalone();
                using var second = new Standalone();
                return [Count(first, false), Count(first, false), Count(second, false), Count(first, false)];
            }

            public static string AfterDisposal()
            {
                var library = new Standalone();
                library.Dispose();
                library.Dispose();
                try
                {
                    Count(library, false);
                    return "ran";
                }
                catch (ObjectDisposedException)
                {
                    return "refused";
                }
            }
        """;

    private static readonly Lazy<TranslatedProgram> Program = new(() => TranslatedProgram.Compile(SavedStateSample.Source, Driver));

    [Fact]
    public void Run_WhenAVariableIsSavedAndInitialized_ShouldStartFromItsValueAndKeepWhatIsStored() =>
        Program.Value.Run<int[]>("Counter").ShouldBe([11, 16, 9, 22]);

    [Fact]
    public void Run_WhenDataInitializesArraysRepeatsLoopsAndSubstrings_ShouldStoreEachValueWhereTheLanguagePutsIt() =>
        Program.Value.Run<int[]>("Tables").ShouldBe([607, 77, 1221999, -2485, 100000001, 250, 11, 32102, 64, 2122, 1, 65, 12, 2]);

    [Fact]
    public void Run_WhenLocalsAreTooLargeForTheStack_ShouldKeepThemInTheInstance() =>
        Program.Value.Run<double>("Large").ShouldBe(500653.0);

    [Fact]
    public void Run_WhenThereAreTwoInstances_ShouldKeepEachOnesVariablesApart() =>
        Program.Value.Run<int[]>("TwoInstances").ShouldBe([11, 16, 11, 25]);

    [Fact]
    public void Run_WhenAnInstanceIsDisposedBeforeItsFirstCallOfAUnit_ShouldRefuseToHandOutMemory() =>
        Program.Value.Run<string>("AfterDisposal").ShouldBe("refused");
}
