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

    private sealed class Prefixed : EmitConvention
    {
        public override string ProcedureName(string name) => "F77_" + name;
    }
}
