using Dracocephalum.Fortrania.Emit;
using Shouldly;

namespace Dracocephalum.Fortrania.Tests.Emit;

/// <summary>
/// Runs the translated sample of procedures passed as arguments. Every
/// expected value is what the reference compiler's program printed for the
/// same source, but where a test passes a method of its own.
/// </summary>
public class CSharpEmitterProcedureArgumentTests
{
    private const string Driver =
        """
            public static double[] Reals()
            {
                using var library = new Standalone();
                double* reals = stackalloc double[4];
                int* integers = stackalloc int[3];
                library.DRIVER(reals, integers);
                return [reals[0], reals[1], reals[2], reals[3]];
            }

            public static int[] Integers()
            {
                using var library = new Standalone();
                double* reals = stackalloc double[4];
                int* integers = stackalloc int[3];
                library.DRIVER(reals, integers);
                return [integers[0], integers[1], integers[2]];
            }

            private static double Half(FortranContext context, double* x) => *x / 2;

            public static double[] FromCSharp()
            {
                using var library = new Standalone();
                double two = 2.0;
                return [library.APPLY(&Half, &two), library.RELAY((delegate*<FortranContext, double*, double>)&Half, &two)];
            }
        """;

    private static readonly Lazy<TranslatedProgram> Program = new(() => TranslatedProgram.Compile(ProcedureArgumentSample.Source, Driver));

    [Fact]
    public void Run_WhenFunctionsArePassed_ShouldCallTheOnePassedWhetherAtOnceOrHandedOnFirst() =>
        Program.Value.Run<double[]>("Reals").ShouldBe([13.0, 35.0, 25.0, 8.0]);

    [Fact]
    public void Run_WhenASubroutineALogicalFunctionAndACharacterFunctionArePassed_ShouldCallEachAsItsKindIsCalled() =>
        Program.Value.Run<int[]>("Integers").ShouldBe([8, 11, 0]);

    // The reference compiler has no part in this one: the function passed
    // is a static method of the test's own, of the shape the parameter asks.
    [Fact]
    public void Run_WhenAMethodOfTheCallersOwnIsPassed_ShouldCallIt() =>
        Program.Value.Run<double[]>("FromCSharp").ShouldBe([2.5, 2.5]);

    [Fact]
    public void Emit_WhenAUnitCallsAProcedureItIsPassed_ShouldTypeThePointerAndPassTheInstanceFirst()
    {
        // Act
        var files = TranslatedProgram.Translate(ProcedureArgumentSample.Source);

        // Assert
        files[5].ShouldContain(
            """
                public double APPLY(delegate*<FortranContext, double*, double> f, double* x)
                {
                    double apply = 0;
                    double _d1 = 0;
                    apply = f(_context, x) + f(_context, Arg(&_d1, *x + 1.0));
                    return apply;
                }
            """.ReplaceLineEndings("\n"),
            Case.Sensitive);
        files[8].ShouldContain("public int COUNTS(delegate*<FortranContext, int*, bool> p, delegate*<FortranContext, byte*, int, int*, byte*> t, int* n)");
    }

    [Fact]
    public void Emit_WhenAUnitOnlyPassesAProcedureOn_ShouldLeaveThePointerUntypedAndCastItWhereItGoes()
    {
        // Act
        var file = TranslatedProgram.Translate(ProcedureArgumentSample.Source)[6];

        // Assert
        file.ShouldContain(
            """
                public double RELAY(void* f, double* x)
                {
                    double relay = 0;
                    relay = APPLY((delegate*<FortranContext, double*, double>)f, x);
                    return relay;
                }
            """.ReplaceLineEndings("\n"),
            Case.Sensitive);
    }

    [Fact]
    public void Emit_WhenTwoCallsTakeAProcedureDifferently_ShouldLeaveThePointerUntypedAndCastItAtEachCall()
    {
        // Act
        var file = TranslatedProgram.Translate(ProcedureArgumentSample.Source)[9];

        // Assert
        file.ShouldContain("public double TWOWAY(void* f, double* x, int* n)");
        file.ShouldContain("twoway = ((delegate*<FortranContext, double*, double>)f)(_context, x);");
        file.ShouldContain("twoway = ((delegate*<FortranContext, int*, double>)f)(_context, n);");
    }

    [Fact]
    public void Emit_WhenAUnitPassesAProcedureOfTheProgram_ShouldWriteOneForwarderForItAndPassItsAddress()
    {
        // Act
        var file = TranslatedProgram.Translate(ProcedureArgumentSample.Source)[10];

        // Assert
        file.ShouldContain(
            """
                private static double _DRIVER_passes_SQUARE(FortranContext context, double* a1)
                {
                    context.Arena.Push();
                    try
                    {
                        return context.Get<Library>().SQUARE(a1);
                    }
                    finally
                    {
                        context.Arena.Pop();
                    }
                }

            """.ReplaceLineEndings("\n"));
        file.ShouldContain("    private static byte* _DRIVER_passes_TAG(FortranContext context, byte* result, int result_len, int* a1)\n");
        file.ShouldContain("            return context.Get<Library>().TAG(result, result_len, a1);\n");
        file.Split("private static double _DRIVER_passes_SQUARE").Length.ShouldBe(2);
        file.ShouldContain("@out[0] = APPLY(&_DRIVER_passes_SQUARE, Arg(&_d1, 2.0));");
        file.ShouldContain("@out[2] = RELAY((void*)(delegate*<FortranContext, double*, double>)&_DRIVER_passes_SQUARE, Arg(&_d1, 3.0));");
    }

    [Fact]
    public void Emit_WhenTextLengthsComeLast_ShouldPlaceThemSoInPointersAndForwarders()
    {
        // Arrange
        const string Source =
            """
                  SUBROUTINE SHOW ( NAME, N )
                  CHARACTER*(*) NAME
                  INTEGER N
                  N = LEN ( NAME )
                  END

                  SUBROUTINE EACH ( S, N )
                  EXTERNAL S
                  INTEGER N
                  CALL S ( 'ABC', N )
                  END

                  SUBROUTINE BOTH ( N )
                  EXTERNAL SHOW
                  INTEGER N
                  CALL EACH ( SHOW, N )
                  END
            """;

        // Act
        var files = TranslatedProgram.Translate(Source, new EmitOptions("N", "C") { Convention = EmitConvention.F2c });

        // Assert
        files[1].ShouldContain("(delegate*<FortranContext, byte*, int*, int, void> s, int* n)");
        files[2].ShouldContain("private static void _BOTH_passes_SHOW(FortranContext context, byte* a1, int* a2, int a1_len)");
        files[2].ShouldContain("context.Arena.Push();");
        files[2].ShouldContain("context.Get<C>().show_(a1, a2, a1_len);");
        files[2].ShouldContain("context.Arena.Pop();");
    }
}
