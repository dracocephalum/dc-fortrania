using Dracocephalum.Fortrania.Tests.Emit;
using Dracocephalum.Fortrania.Translation;
using Shouldly;

namespace Dracocephalum.Fortrania.Tests.Translation;

public sealed class TranslatorSavedScalarTests : IDisposable
{
    // A driver for a library Core of the project Example.Toolkit: each test
    // gives the statements of its Run.
    private const string DriverShell =
        """
        using Dracocephalum.Fortrania.Runtime;

        namespace Translated;

        public static unsafe class Driver
        {
            public static double[] Run()
            {
                using var context = new FortranContext();
                var core = context.Get<Example.Toolkit.Core>();
                BODY
            }
        }
        """;

    private readonly string _root = Path.Combine(AppContext.BaseDirectory, "saved-" + Guid.NewGuid().ToString("N"));

    public TranslatorSavedScalarTests() => Directory.CreateDirectory(_root);

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public void Run_WhenAUnitKeepsScalarsBetweenCalls_ShouldKeepThemInLocalsAndStoreAndReadThemAroundACallThatMayComeBack()
    {
        // Arrange: a sum and a counter the unit keeps, a call that is
        // passed one of them and reaches nothing of the unit, and a call
        // that from the second time on reaches another entry of the unit,
        // which gives the factor another value.
        Put("core/acc.f", """
                  SUBROUTINE ACC ( X, N, TOTAL )
                  DOUBLE PRECISION X(*), TOTAL
                  INTEGER N
                  DOUBLE PRECISION SUM, SCALE
                  INTEGER I, CALLS
                  SAVE
                  DATA SCALE / 2.0D0 /
                  DATA CALLS / 0 /
                  CALLS = CALLS + 1
                  SUM = 0.0D0
                  DO I = 1, N
                     SUM = SUM + SCALE * X(I)
                     CALL LEAF ( SUM )
                  END DO
                  CALL BACK ( CALLS )
                  TOTAL = SUM + SCALE * 100 + CALLS
                  RETURN
                  ENTRY ACCSET ( TOTAL )
                  SCALE = TOTAL
                  END
                  SUBROUTINE LEAF ( V )
                  DOUBLE PRECISION V
                  V = V + 1.0D0
                  END
                  SUBROUTINE BACK ( K )
                  INTEGER K
                  DOUBLE PRECISION S
                  S = 3.0D0
                  IF ( K .GE. 2 ) CALL ACCSET ( S )
                  END
            """);
        const string Body =
            """
            var x = stackalloc double[] { 1, 2, 3 };
            int n = 3;
            double first = 0, second = 0, third = 0;
            core.ACC(x, &n, &first);
            core.ACC(x, &n, &second);
            core.ACC(x, &n, &third);
            return [first, second, third];
            """;

        // Act
        var result = Translate();

        // Assert
        result.Succeeded.ShouldBeTrue(string.Join('\n', result.Messages));
        var text = Text("ACC.g.cs");
        text.ShouldContain("/* Kept scalars, in locals while the method runs: stored where it ends, and around a call that may come back to this unit. */");
        text.ShouldContain("double sum_value = *sum;");
        text.ShouldContain("for (i_value = 1; i_value <= _last1; i_value += 1)");
        text.ShouldContain("sum_value = sum_value + scale_value * x[i_value - 1];");

        // A call that reaches nothing of the unit: only what it is passed.
        text.ShouldContain("            *sum = sum_value;\n            LEAF(sum);\n            sum_value = *sum;\n        }");

        // A call that may come back: everything the method has stored
        // before it, and everything read again after it.
        text.ShouldContain("        *sum = sum_value;\n        *i = i_value;\n        *calls = calls_value;\n        BACK(calls);\n        sum_value = *sum;\n        scale_value = *scale;\n        i_value = *i;\n        calls_value = *calls;\n");

        // The second call's total has the factor the other entry stored
        // during it, and the third call's sum is made with it.
        Run(Body).ShouldBe([216.0, 317.0, 324.0]);
    }

    [Fact]
    public void Run_WhenACallGoesThroughAProcedureArgument_ShouldStoreAndReadTheKeptScalarsAroundIt()
    {
        // Arrange: what is passed is another entry of the unit itself,
        // which nothing in the unit's own calls shows.
        Put("core/walk.f", """
                  SUBROUTINE WALK ( STEP, N, TOTAL )
                  EXTERNAL STEP
                  INTEGER N, I
                  DOUBLE PRECISION TOTAL, SUM
                  SAVE SUM
                  SUM = 0
                  DO I = 1, N
                     SUM = SUM + I
                     IF ( I .EQ. 2 ) CALL STEP ( TOTAL )
                  END DO
                  TOTAL = SUM
                  RETURN
                  ENTRY WALKSET ( TOTAL )
                  SUM = 100
                  END
                  SUBROUTINE TOP ( N, TOTAL )
                  INTEGER N
                  DOUBLE PRECISION TOTAL
                  EXTERNAL WALKSET
                  CALL WALK ( WALKSET, N, TOTAL )
                  END
            """);
        const string Body =
            """
            int n = 3;
            double total = 0;
            core.TOP(&n, &total);
            return [total];
            """;

        // Act
        var result = Translate();

        // Assert
        result.Succeeded.ShouldBeTrue(string.Join('\n', result.Messages));
        var text = Text("WALK.g.cs");
        text.ShouldContain("double sum_value = *sum;");
        text.ShouldContain("                *sum = sum_value;\n                step(");
        text.ShouldContain("                sum_value = *sum;\n            }");
        Run(Body).ShouldBe([103.0]);
    }

    [Fact]
    public void Run_WhenAKeptScalarCannotBeKeptSimply_ShouldLeaveItBehindItsAddress()
    {
        // Arrange: one that shares its storage with another, and one read
        // in a loop whose condition calls what may come back.
        Put("core/left.f", """
                  SUBROUTINE SHARED ( N, TOTAL )
                  INTEGER N, I, A, B
                  DOUBLE PRECISION TOTAL
                  SAVE A, B
                  EQUIVALENCE ( A, B )
                  A = 0
                  DO I = 1, N
                     A = A + I
                     B = B + 1
                  END DO
                  TOTAL = B
                  END
                  SUBROUTINE HEAD ( MORE, TOTAL )
                  LOGICAL MORE
                  EXTERNAL MORE
                  INTEGER K
                  DOUBLE PRECISION TOTAL
                  SAVE K
                  K = 0
                  DO WHILE ( MORE ( TOTAL ) )
                     K = K + 1
                     TOTAL = TOTAL + K
                  END DO
                  END
            """);
        const string Body =
            """
            int n = 3;
            double total = 0;
            core.SHARED(&n, &total);
            return [total];
            """;

        // Act
        var result = Translate();

        // Assert
        result.Succeeded.ShouldBeTrue(string.Join('\n', result.Messages));
        Text("SHARED.g.cs").ShouldNotContain("Kept scalars");
        Text("HEAD.g.cs").ShouldNotContain("Kept scalars");
        Run(Body).ShouldBe([9.0]);
    }

    private TranslationProject Project(string name, string output, string source, string files) => new()
    {
        Name = name,
        Settings =
        [
            new TranslationSettings
            {
                Folder = _root,
                Output = output,
                Sources = [new SourceSettings { Name = source, Files = [new FileSettings { Paths = [files] }] }],
            },
        ],
        Summary = Path.Combine(_root, output, ProjectSummary.GetFileName(name)),
    };

    private TranslationResult Translate() =>
        Translator.Run(Project("Example.Toolkit", "out", "Core", "core/*.f"), new TranslationOptions());

    private double[] Run(string body) => TranslatedProgram
        .FromSources(Directory.GetFiles(Path.Combine(_root, "out"), "*.cs", SearchOption.AllDirectories)
            .Select(File.ReadAllText)
            .Append(DriverShell.Replace("BODY", body, StringComparison.Ordinal)))
        .Run<double[]>("Run");

    private void Put(string path, string text)
    {
        var full = Path.Combine(_root, path);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, text);
    }

    // A file of the library's translation.
    private string Text(string name) =>
        File.ReadAllText(Path.Combine(_root, "out", Translator.GeneratedFolder, "Core", name)).ReplaceLineEndings("\n");
}
