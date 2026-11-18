using Dracocephalum.Fortrania.Emit;
using Shouldly;

namespace Dracocephalum.Fortrania.Tests.Emit;

public class EmitConventionTests
{
    // A logical function, a function that references it, and a subroutine
    // whose name holds an underscore and that calls the function for what
    // it does besides returning.
    private const string Source =
        """
              LOGICAL FUNCTION ISPOS ( N )
              INTEGER N
              ISPOS = N .GT. 0
              RETURN
              END
              INTEGER FUNCTION COUNT ( A, B )
              INTEGER A, B
              LOGICAL ISPOS
              COUNT = 0
              IF ( ISPOS ( A ) ) COUNT = COUNT + 1
              IF ( ISPOS ( B ) .AND. .NOT. ISPOS ( -B ) ) COUNT = COUNT + 10
              END
              SUBROUTINE TALLY_2 ( A, TOTAL )
              INTEGER A, TOTAL, COUNT
              CALL ISPOS ( A )
              TOTAL = COUNT ( A, A ) + COUNT ( -A, A )
              END
        """;

    [Theory]
    [InlineData("VADD", "VADD")]
    [InlineData("ZZ_X1", "ZZ_X1")]
    public void ProcedureName_WhenConventionIsFortranias_ShouldKeepTheFortranName(string name, string expected) =>
        EmitConvention.Fortrania.ProcedureName(name).ShouldBe(expected);

    [Theory]
    [InlineData("VADD", "vadd_")]
    [InlineData("SPKEZ", "spkez_")]
    [InlineData("NICEPR_1", "nicepr_1__")]
    public void ProcedureName_WhenConventionIsThatOfF2c_ShouldLowerItAndAddUnderscores(string name, string expected) =>
        EmitConvention.F2c.ProcedureName(name).ShouldBe(expected);

    [Fact]
    public void ReturnsLogicalAsInteger_ShouldBeFalseForFortraniaAndTrueForF2c()
    {
        EmitConvention.Fortrania.ReturnsLogicalAsInteger.ShouldBeFalse();
        EmitConvention.F2c.ReturnsLogicalAsInteger.ShouldBeTrue();
    }

    [Fact]
    public void Emit_WhenConventionIsFortranias_ShouldNameMethodsAsFortranDoesAndReturnBool()
    {
        // Arrange
        var program = TranslatedProgram.Compile(
            Source,
            """
                public static int Run()
                {
                    var library = new Library();
                    int three = 3, total = 0;
                    bool positive = library.ISPOS(&three);
                    library.TALLY_2(&three, &total);
                    return positive ? total : -1;
                }
            """);

        // Act & Assert
        program.Run<int>("Run").ShouldBe(21);
    }

    [Fact]
    public void Emit_WhenConventionIsThatOfF2c_ShouldNameMethodsItsWayAndReturnAnInteger()
    {
        // Arrange
        var program = TranslatedProgram.Compile(
            Source,
            """
                public static int Run()
                {
                    var library = new Library();
                    int three = 3, total = 0;
                    int positive = library.ispos_(&three);
                    library.tally_2__(&three, &total);
                    return positive * total;
                }
            """,
            new EmitOptions(TranslatedProgram.Namespace, TranslatedProgram.ClassName) { Convention = EmitConvention.F2c });

        // Act & Assert
        program.Run<int>("Run").ShouldBe(21);
    }

    [Fact]
    public void TextLengthsComeLast_ShouldBeFalseForFortraniaAndTrueForF2c()
    {
        EmitConvention.Fortrania.TextLengthsComeLast.ShouldBeFalse();
        EmitConvention.F2c.TextLengthsComeLast.ShouldBeTrue();
    }

    [Theory]
    [InlineData(false, "library.JOIN(first, 2, &count, second, 3, result, 6);")]
    [InlineData(true, "library.join_(first, &count, second, result, 2, 3, 6);")]
    public void Emit_WhenArgumentsAreCharacter_ShouldPutTheirLengthsWhereTheConventionSays(bool isF2c, string call)
    {
        // Arrange: JOIN passes its arguments on, in another order, so that
        // both taking the lengths and handing them over are tried.
        var options = new EmitOptions(TranslatedProgram.Namespace, TranslatedProgram.ClassName)
        {
            Convention = isF2c ? EmitConvention.F2c : EmitConvention.Fortrania,
        };
        var program = TranslatedProgram.Compile(
            """
                  SUBROUTINE PLACE ( OUT, N, A, B )
                  CHARACTER*(*) OUT, A, B
                  INTEGER N
                  OUT = A
                  OUT ( N + 1: ) = B
                  N = LEN ( A ) * 100 + LEN ( B ) * 10 + LEN ( OUT )
                  END
                  SUBROUTINE JOIN ( A, N, B, OUT )
                  CHARACTER*(*) A, B, OUT
                  INTEGER N
                  CALL PLACE ( OUT, N, A, B )
                  END
            """,
            $$"""
                public static string Run()
                {
                    var library = new Library();
                    byte* first = stackalloc byte[2] { (byte)'a', (byte)'b' };
                    byte* second = stackalloc byte[3] { (byte)'X', (byte)'Y', (byte)'Z' };
                    byte* result = stackalloc byte[6];
                    int count = 2;
                    {{call}}
                    return System.Text.Encoding.ASCII.GetString(result, 6) + count;
                }
            """,
            options);

        // Act & Assert
        program.Run<string>("Run").ShouldBe("abXYZ 236");
    }

    [Fact]
    public void Emit_WhenConventionIsOnesOwn_ShouldFollowIt()
    {
        // Arrange
        var program = TranslatedProgram.Compile(
            Source,
            """
                public static int Run()
                {
                    var library = new Library();
                    int three = 3, total = 0;
                    library.F77_TALLY_2(&three, &total);
                    return library.F77_ISPOS(&total) ? total : -1;
                }
            """,
            new EmitOptions(TranslatedProgram.Namespace, TranslatedProgram.ClassName) { Convention = new Prefixed() });

        // Act & Assert
        program.Run<int>("Run").ShouldBe(21);
    }

    [Fact]
    public void Emit_WhenWarningsAreToBeSuppressed_ShouldSwitchThemOffInEveryFileAfterTheEmittersOwn()
    {
        // Arrange
        var options = new EmitOptions("N", "C") { SuppressedWarnings = ["CA1707", "SA1300", "CS0219"] };

        // Act
        var files = TranslatedProgram.Translate(Source, options);

        // Assert
        files.Count.ShouldBe(3);
        files.ShouldAllBe(file => file.Contains("\n#pragma warning disable CS0162, CS0219, CA1707, SA1300\n"));
    }

    // A function of one unit, referenced from another, with a call into
    // the runtime in each: a power that is not a square.
    private const string Powers =
        """
              INTEGER FUNCTION F ( N )
              INTEGER N
              F = N ** 3
              END
              INTEGER FUNCTION POW ( N )
              INTEGER N, F
              POW = F ( N ) + N ** 4
              END
        """;

    private const string PowersDriver =
        """
            public static int Run()
            {
                int three = 3;
                return new Library().{0}(&three);
            }
        """;

    private static EmitOptions Options(string? alias = null, EmitConvention? convention = null) =>
        new(TranslatedProgram.Namespace, TranslatedProgram.ClassName)
        {
            RuntimeAlias = alias,
            Convention = convention ?? EmitConvention.Fortrania,
        };

    [Fact]
    public void Emit_WhenNoShortNameIsGiven_ShouldCallTheRuntimeBare()
    {
        // Act
        var files = TranslatedProgram.Translate(Powers);

        // Assert
        files[0].ShouldContain("\nusing static Dracocephalum.Fortrania.Runtime.Fortran;\n");
        files[0].ShouldContain("f = Pow(*n, 3);", Case.Sensitive);
        files[1].ShouldContain("pow = F(n) + Pow(*n, 4);", Case.Sensitive);
        TranslatedProgram.Compile(Powers, PowersDriver.Replace("{0}", "POW")).Run<int>("Run").ShouldBe(108);
    }

    [Fact]
    public void Emit_WhenAShortNameIsGiven_ShouldCallTheRuntimeByIt()
    {
        // Act
        var files = TranslatedProgram.Translate(Powers, Options("Rt"));

        // Assert
        files[0].ShouldContain("\nusing Rt = Dracocephalum.Fortrania.Runtime.Fortran;\n");
        files[0].ShouldContain("f = Rt.Pow(*n, 3);", Case.Sensitive);
        TranslatedProgram.Compile(Powers, PowersDriver.Replace("{0}", "POW"), Options("Rt")).Run<int>("Run").ShouldBe(108);
    }

    [Fact]
    public void Emit_WhenAProcedureIsEmittedUnderTheShortName_ShouldWriteTheRuntimesFullNameInEveryUnit()
    {
        // Act: the short name asked for is that of the function F.
        var files = TranslatedProgram.Translate(Powers, Options("F"));

        // Assert
        files.ShouldAllBe(file => !file.Contains("using F ="));
        files[0].ShouldContain("f = Dracocephalum.Fortrania.Runtime.Fortran.Pow(*n, 3);", Case.Sensitive);
        files[1].ShouldContain("Dracocephalum.Fortrania.Runtime.Fortran.Pow(*n, 4)", Case.Sensitive);
        TranslatedProgram.Compile(Powers, PowersDriver.Replace("{0}", "POW"), Options("F")).Run<int>("Run").ShouldBe(108);
    }

    [Fact]
    public void Emit_WhenAProcedureIsEmittedUnderTheNameOfARuntimeMethod_ShouldWriteThatMethodsFullName()
    {
        // Arrange: under this convention the function POW is the method Pow,
        // which is what the runtime calls its own.
        var options = Options(convention: new Capitalized());

        // Act
        var files = TranslatedProgram.Translate(Powers, options);

        // Assert
        files[0].ShouldContain("f = Dracocephalum.Fortrania.Runtime.Fortran.Pow(*n, 3);", Case.Sensitive);
        files[1].ShouldContain("pow = F(n) + Dracocephalum.Fortrania.Runtime.Fortran.Pow(*n, 4);", Case.Sensitive);
        TranslatedProgram.Compile(Powers, PowersDriver.Replace("{0}", "Pow"), options).Run<int>("Run").ShouldBe(108);
    }

    [Fact]
    public void Emit_WhenTheConventionKeepsTheProcedureClearOfTheShortName_ShouldUseTheShortName()
    {
        // Act: under this convention the function F is the method f_.
        var files = TranslatedProgram.Translate(Powers, Options("F", EmitConvention.F2c));

        // Assert
        files[0].ShouldContain("f = F.Pow(*n, 3);", Case.Sensitive);
    }

    [Fact]
    public void Emit_WhenNoWarningsAreNamed_ShouldSwitchOffTheEmittersOwnAlone() =>
        TranslatedProgram.Translate(Source)[0].ShouldContain("\n#pragma warning disable CS0162, CS0219\n");

    [Fact]
    public void Emit_WhenAFunctionEndsOnAReturn_ShouldNotReturnItsResultASecondTime()
    {
        // Act: ISPOS ends on a RETURN and COUNT runs off its end.
        var files = TranslatedProgram.Translate(Source);

        // Assert
        files[0].Split("return ispos != 0;").Length.ShouldBe(2);
        files[1].Split("return count;").Length.ShouldBe(2);
    }

    private sealed class Capitalized : EmitConvention
    {
        public override string ProcedureName(string name) => name[..1] + name[1..].ToLowerInvariant();
    }

    private sealed class Prefixed : EmitConvention
    {
        public override string ProcedureName(string name) => "F77_" + name;
    }
}
