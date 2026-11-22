using Shouldly;

namespace Dracocephalum.Fortrania.Tests.Emit;

/// <summary>
/// Runs two translated libraries as one program. Every expected value is
/// what the reference compiler's program printed for the two linked
/// together.
/// </summary>
public class CSharpEmitterLibrariesTests
{
    private const string Driver =
        """
            private static int[] Run(string made)
            {
                var name = System.Text.Encoding.UTF8.GetBytes(
                    System.IO.Path.Combine(AppContext.BaseDirectory, Guid.NewGuid().ToString("N") + ".bin"));
                var results = new int[5];
                using var context = new FortranContext();
                App app;
                switch (made)
                {
                    case "app first":
                        app = new App(context);
                        _ = new Core(context);
                        break;
                    case "core first":
                        _ = new Core(context);
                        app = new App(context);
                        break;
                    default:
                        app = context.Get<App>();
                        break;
                }

                fixed (byte* file = name)
                fixed (int* first = results)
                {
                    app.ARUN(file, name.Length, first);
                }

                return results;
            }

            public static int[] CoreFirst() => Run("core first");

            public static int[] AppFirst() => Run("app first");

            public static int[] NoneMade() => Run("none");

            public static bool[] Asked()
            {
                using var context = new FortranContext();
                var made = new Core(context);
                var app = context.Get<App>();
                return
                [
                    ReferenceEquals(context.Get<Core>(), made),
                    ReferenceEquals(context.Get<App>(), app),
                    ReferenceEquals(app.Context, context),
                    ReferenceEquals(made.Context, context),
                ];
            }

            public static int[] TwoPrograms()
            {
                using var one = new FortranContext();
                using var other = new FortranContext();
                var first = new Core(one);
                var second = new Core(other);
                int five = 5, seven = 7, a = 0, b = 0;
                first.CPUT(&five);
                second.CPUT(&seven);
                first.CGET(&a);
                second.CGET(&b);
                return [a, b];
            }
        """;

    private static readonly Lazy<TranslatedProgram> Program = new(() =>
        TranslatedProgram.CompileLibraries(Driver, ("Core", LibrariesSample.Core), ("App", LibrariesSample.App)));

    [Fact]
    public void Run_WhenOneLibraryCallsAnother_ShouldShareItsStateItsUnitsAndPassProceduresEitherWay() =>
        Program.Value.Run<int[]>("CoreFirst").ShouldBe([12, 1234, 13, 9, 7]);

    [Fact]
    public void Run_WhenTheLibrariesAreMadeInTheOtherOrder_ShouldFindEachOtherAllTheSame() =>
        Program.Value.Run<int[]>("AppFirst").ShouldBe([12, 1234, 13, 9, 7]);

    [Fact]
    public void Run_WhenNoLibraryWasMadeByHand_ShouldHaveTheContextMakeEachWhenItIsFirstAskedFor() =>
        Program.Value.Run<int[]>("NoneMade").ShouldBe([12, 1234, 13, 9, 7]);

    [Fact]
    public void Run_WhenALibraryIsAskedOfItsContext_ShouldGiveTheOneThereIsAndEachShouldKnowItsContext() =>
        Program.Value.Run<bool[]>("Asked").ShouldBe([true, true, true, true]);

    [Fact]
    public void Run_WhenThereAreTwoPrograms_ShouldKeepTheLibrariesOfEachApart() =>
        Program.Value.Run<int[]>("TwoPrograms").ShouldBe([5, 7]);

    [Fact]
    public void Emit_WhenAProcedureBelongsToAnotherLibrary_ShouldCallItOnThatLibrarysInstance()
    {
        // Act
        var files = TranslatedProgram.TranslateLibraries(("Core", LibrariesSample.Core), ("App", LibrariesSample.App));
        var caller = files.Single(file => file.Contains("public void ARUN("));
        var appClass = files.Single(file => file.Contains("public App(FortranContext context)"));

        // Assert
        caller.ShouldContain("_Core.CPUT(Arg(&_i1, 5));");
        caller.ShouldContain("_Core.COPEN(name, name_len, Arg(&_i1, 31));");
        caller.ShouldContain("@out[4] = _Core.CBACK(Arg(&_i1, 2));");
        caller.ShouldContain("private static int _ARUN_passes_ATRIPL(FortranContext context, int* a1)");
        caller.ShouldContain("context.Working.Enter();");
        caller.ShouldContain("return context.Get<App>().ATRIPL(a1);");
        caller.ShouldContain("context.Working.Leave();");
        caller.ShouldContain("private static int _ARUN_passes_CTWICE(FortranContext context, int* a1)");
        caller.ShouldContain("context.Working.Enter();");
        caller.ShouldContain("return context.Get<global::Translated.Core>().CTWICE(a1);");
        caller.ShouldContain("context.Working.Leave();");
        appClass.ShouldContain("private global::Translated.Core? _library_Core;");
        appClass.ShouldContain("private global::Translated.Core _Core => _library_Core ??= _context.Get<global::Translated.Core>();");
    }

    [Fact]
    public void Emit_WhenALibraryRefersToNoOther_ShouldWriteNothingForOthers()
    {
        // Act
        var files = TranslatedProgram.TranslateLibraries(("Core", "      SUBROUTINE ALONE ( N )\n      INTEGER N\n      N = 1\n      END\n"));

        // Assert
        files.ShouldAllBe(file => !file.Contains("_library"));
    }
}
