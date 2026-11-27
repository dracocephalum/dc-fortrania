using Dracocephalum.Fortrania.Tests.Emit;
using Dracocephalum.Fortrania.Translation;
using Shouldly;

namespace Dracocephalum.Fortrania.Tests.Translation;

public sealed class TranslatorScalarArgumentTests : IDisposable
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

    private readonly string _root = Path.Combine(AppContext.BaseDirectory, "scalars-" + Guid.NewGuid().ToString("N"));

    public TranslatorScalarArgumentTests() => Directory.CreateDirectory(_root);

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public void Run_WhenAScalarArgumentIsUsedInALoop_ShouldKeepItInALocalAndStoreItBackWhereTheMethodEnds()
    {
        // Arrange: a sum that reads one argument and writes another on every
        // turn, and ends early by one path.
        Put("core/sum.f", """
                  SUBROUTINE SUMUP ( X, N, TOTAL )
                  DOUBLE PRECISION X, TOTAL
                  INTEGER N, I
                  TOTAL = 0
                  IF ( N .LE. 0 ) THEN
                     TOTAL = -1
                     RETURN
                  END IF
                  DO I = 1, N
                     TOTAL = TOTAL + X * I
                  END DO
                  END
            """);
        const string Body =
            """
            double x = 1.5, total = 7, none = 7;
            int n = 4, zero = 0;
            core.SUMUP(&x, &n, &total);
            core.SUMUP(&x, &zero, &none);
            return [total, none, x, n];
            """;

        // Act
        var result = Translate();
        var plain = Translate(allowsArgumentAliasing: true, output: "plain");

        // Assert
        result.Succeeded.ShouldBeTrue(string.Join('\n', result.Messages));
        var text = Text("out", "SUMUP.g.cs");
        text.ShouldContain("double x_value = *x;");
        text.ShouldContain("double total_value = *total;");
        text.ShouldContain("total_value = total_value + x_value * i;");
        text.ShouldContain("            *total = total_value;\n            return;");
        text.ShouldEndWith("        *total = total_value;\n    }\n}\n");
        result.AliasedPlaces.ShouldBeEmpty();

        Text("plain", "SUMUP.g.cs").ShouldNotContain("_value");
        Run("out", Body).ShouldBe([15.0, -1.0, 1.5, 4.0]);
        Run("plain", Body).ShouldBe([15.0, -1.0, 1.5, 4.0]);
    }

    [Fact]
    public void Run_WhenAStatementPassesTheArgumentOn_ShouldStoreTheLocalBeforeItAndReadItAgainAfter()
    {
        // Arrange: an argument read in a loop that also passes it to a
        // procedure that changes it, and one the loop stores to and passes on.
        Put("core/work.f", """
                  SUBROUTINE BUMP ( N )
                  INTEGER N
                  N = N + 1
                  END

                  SUBROUTINE WORK ( N, TOTAL )
                  INTEGER N, TOTAL, I
                  TOTAL = 0
                  DO I = 1, 3
                     TOTAL = TOTAL + N
                     CALL BUMP ( N )
                     CALL BUMP ( TOTAL )
                  END DO
                  TOTAL = TOTAL + N
                  END
            """);
        const string Body =
            """
            int n = 1, total = 0;
            core.WORK(&n, &total);
            return [total, n];
            """;

        // Act
        var result = Translate();

        // Assert: 1 + 2 + 3, one more after each, and 4 at the end.
        result.Succeeded.ShouldBeTrue(string.Join('\n', result.Messages));
        var text = Text("out", "WORK.g.cs");
        text.ShouldContain("BUMP(n);\n            n_value = *n;");
        text.ShouldContain("*total = total_value;\n            BUMP(total);\n            total_value = *total;");
        text.ShouldNotContain("*n = n_value;");
        Run("out", Body).ShouldBe([13.0, 4.0]);
    }

    [Fact]
    public void Run_WhenACallPassesOneVariableTwice_ShouldLeaveThoseArgumentsBehindTheirAddressesAndListTheCall()
    {
        // Arrange: a procedure that is right only behind its addresses when
        // both arguments are one variable, called so and called properly;
        // and one never called so.
        Put("core/add.f", """
                  SUBROUTINE ADDTO ( X, Y )
                  DOUBLE PRECISION X, Y
                  INTEGER I
                  DO I = 1, 2
                     Y = Y + X
                  END DO
                  END

                  SUBROUTINE TWICE ( X, Y )
                  DOUBLE PRECISION X, Y
                  INTEGER I
                  DO I = 1, 2
                     Y = Y + X
                  END DO
                  END

                  SUBROUTINE BOTH ( V, A )
                  DOUBLE PRECISION V, A ( 3 )
                  INTEGER I
                  I = 2
                  CALL ADDTO ( V, V )
                  CALL TWICE ( A ( 1 ), A ( 2 ) )
                  CALL TWICE ( A ( I ), A ( I + 1 ) )
                  END
            """);
        const string Body =
            """
            double v = 1;
            double* a = stackalloc double[3] { 1, 10, 100 };
            core.BOTH(&v, a);
            return [v, a[0], a[1], a[2]];
            """;

        // Act
        var result = Translate();

        // Assert: V doubled twice. Two elements written differently are two.
        result.Succeeded.ShouldBeTrue(string.Join('\n', result.Messages));
        Text("out", "ADDTO.g.cs").ShouldNotContain("_value");
        Text("out", "TWICE.g.cs").ShouldContain("double x_value = *x;");
        var place = result.AliasedPlaces.ShouldHaveSingleItem();
        (place.Unit, place.Variable, place.Procedure, place.FirstArgument, place.SecondArgument, place.Line).ShouldBe(("BOTH", "V", "ADDTO", 1, 2, 21));
        result.Messages.ShouldBeEmpty();
        Run("out", Body).ShouldBe([4.0, 1.0, 12.0, 124.0]);
    }

    [Fact]
    public void Run_WhenACallPassesOneElementTwiceOrAnArrayWithItsElement_ShouldLeaveThoseArgumentsBehindTheirAddresses()
    {
        // Arrange
        Put("core/add.f", """
                  SUBROUTINE ADDTO ( X, Y )
                  DOUBLE PRECISION X, Y
                  INTEGER I
                  DO I = 1, 2
                     Y = Y + X
                  END DO
                  END

                  SUBROUTINE SCALE ( A, F )
                  DOUBLE PRECISION A ( 3 ), F
                  INTEGER I
                  DO I = 1, 3
                     A ( I ) = A ( I ) * F
                  END DO
                  END

                  SUBROUTINE BOTH ( A )
                  DOUBLE PRECISION A ( 3 )
                  INTEGER I
                  I = 1
                  CALL ADDTO ( A ( I ), A ( I ) )
                  CALL SCALE ( A, A ( 2 ) )
                  END
            """);
        const string Body =
            """
            double* a = stackalloc double[3] { 1, 2, 3 };
            core.BOTH(a);
            return [a[0], a[1], a[2]];
            """;

        // Act
        var result = Translate();

        // Assert: the factor changes as the second element is scaled.
        result.Succeeded.ShouldBeTrue(string.Join('\n', result.Messages));
        Text("out", "ADDTO.g.cs").ShouldNotContain("_value");
        Text("out", "SCALE.g.cs").ShouldNotContain("_value");
        result.AliasedPlaces.Select(place => (place.Procedure, place.Variable, place.Line)).ShouldBe([("ADDTO", "A", 21), ("SCALE", "A", 22)]);
        Run("out", Body).ShouldBe([8.0, 4.0, 12.0]);
    }

    [Fact]
    public void Run_WhenAnArgumentCannotBeKeptSimply_ShouldLeaveItBehindItsAddress()
    {
        // Arrange: an argument a statement function names; one whose
        // address a condition passes on; and one a statement reads while
        // it passes its address on.
        Put("core/left.f", """
                  INTEGER FUNCTION NEXT ( N )
                  INTEGER N
                  N = N + 1
                  NEXT = N
                  END

                  SUBROUTINE BYFUNC ( X, R )
                  DOUBLE PRECISION X, R, SQ, T
                  INTEGER I
                  SQ ( T ) = T * X
                  R = 0
                  DO I = 1, 3
                     R = R + SQ ( 2.0D0 )
                  END DO
                  END

                  SUBROUTINE BYCOND ( N, R )
                  INTEGER N, NEXT
                  DOUBLE PRECISION R
                  R = 0
                  DO WHILE ( NEXT ( N ) .LT. 4 )
                     R = R + N
                  END DO
                  END

                  SUBROUTINE BYBOTH ( N, R )
                  INTEGER N, NEXT, I
                  DOUBLE PRECISION R
                  R = 0
                  DO I = 1, 2
                     R = R + N + NEXT ( N )
                  END DO
                  END
            """);
        const string Body =
            """
            double x = 3, r1 = 0, r2 = 0, r3 = 0;
            int n = 1, m = 1;
            core.BYFUNC(&x, &r1);
            core.BYCOND(&n, &r2);
            core.BYBOTH(&m, &r3);
            return [r1, r2, n, r3, m];
            """;

        // Act
        var result = Translate();

        // Assert
        result.Succeeded.ShouldBeTrue(string.Join('\n', result.Messages));
        Text("out", "BYFUNC.g.cs").ShouldNotContain("x_value");
        Text("out", "BYFUNC.g.cs").ShouldContain("double r_value = *r;");
        Text("out", "BYCOND.g.cs").ShouldNotContain("n_value");
        Text("out", "BYBOTH.g.cs").ShouldNotContain("n_value");
        Run("out", Body).ShouldBe([18.0, 5.0, 4.0, 8.0, 3.0]);
    }

    [Fact]
    public void Run_WhenACallThroughAnArgumentPassesOneVariableTwice_ShouldLeaveTheArgumentsOfEveryProcedurePassedBehindTheirAddresses()
    {
        // Arrange: ADDTO is passed to APPLY, which calls what it is passed
        // with one variable twice.
        Put("core/apply.f", """
                  SUBROUTINE ADDTO ( X, Y )
                  DOUBLE PRECISION X, Y
                  INTEGER I
                  DO I = 1, 2
                     Y = Y + X
                  END DO
                  END

                  SUBROUTINE APPLY ( F, V )
                  EXTERNAL F
                  DOUBLE PRECISION V
                  CALL F ( V, V )
                  END

                  SUBROUTINE BOTH ( V )
                  DOUBLE PRECISION V
                  EXTERNAL ADDTO
                  CALL APPLY ( ADDTO, V )
                  END
            """);
        const string Body =
            """
            double v = 1;
            core.BOTH(&v);
            return [v];
            """;

        // Act
        var result = Translate();

        // Assert
        result.Succeeded.ShouldBeTrue(string.Join('\n', result.Messages));
        Text("out", "ADDTO.g.cs").ShouldNotContain("_value");
        result.AliasedPlaces.ShouldHaveSingleItem().Procedure.ShouldBe("F");
        Run("out", Body).ShouldBe([4.0]);
    }

    [Fact]
    public void Run_WhenACallFromAnotherProjectPassesOneVariableTwice_ShouldWarnAndChangeNothingInTheProjectCalled()
    {
        // Arrange
        Put("core/add.f", """
                  SUBROUTINE ADDTO ( X, Y )
                  DOUBLE PRECISION X, Y
                  INTEGER I
                  DO I = 1, 2
                     Y = Y + X
                  END DO
                  END
            """);
        Put("app/app.f", """
                  SUBROUTINE BOTH ( V )
                  DOUBLE PRECISION V
                  CALL ADDTO ( V, V )
                  END
            """);
        var library = Project("Example.Toolkit", "out", "Core", "core/*.f");
        var caller = Project("Example.App", "app-out", "Caller", "app/*.f");

        // Act
        var result = Translator.Run([library, caller], new TranslationOptions());

        // Assert
        result.Succeeded.ShouldBeTrue(string.Join('\n', result.Messages));
        Text("out", "ADDTO.g.cs").ShouldContain("double x_value = *x;");
        result.AliasedPlaces.ShouldHaveSingleItem().Project.ShouldBe("Example.App");
        var warning = result.Messages.ShouldHaveSingleItem();
        warning.IsError.ShouldBeFalse();
        warning.Text.ShouldBe("BOTH passes V as arguments 1 and 2 of ADDTO, which may give one of them a value. FORTRAN 77 forbids it, and ADDTO is translated in another project, Example.Toolkit, to rely on that; pass a copy, or translate that project with \"allowsArgumentAliasing\": true.");
    }

    private TranslationProject Project(string name, string output, string source, string files, bool allowsArgumentAliasing = false) => new()
    {
        Name = name,
        Settings =
        [
            new TranslationSettings
            {
                Folder = _root,
                Output = output,
                AllowsArgumentAliasing = allowsArgumentAliasing,
                Sources = [new SourceSettings { Name = source, Files = [new FileSettings { Paths = [files] }] }],
            },
        ],
        Summary = Path.Combine(_root, output, ProjectSummary.GetFileName(name)),
    };

    private TranslationResult Translate(bool allowsArgumentAliasing = false, string output = "out") =>
        Translator.Run(Project("Example.Toolkit", output, "Core", "core/*.f", allowsArgumentAliasing), new TranslationOptions());

    private double[] Run(string output, string body) => TranslatedProgram
        .FromSources(Directory.GetFiles(Path.Combine(_root, output), "*.cs", SearchOption.AllDirectories)
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
    private string Text(string output, string name) =>
        File.ReadAllText(Path.Combine(_root, output, Translator.GeneratedFolder, "Core", name)).ReplaceLineEndings("\n");
}
