using Shouldly;

namespace Dracocephalum.Fortrania.Tests.Emit;

/// <summary>
/// Runs a translated main program that uses what a program takes from its
/// compiler's library. What it writes and the status it ends with are what
/// the reference compiler's program gave for the same source, command line
/// and environment.
/// </summary>
public class CSharpEmitterProgramTests
{
    private const string Source =
        """
              PROGRAM DEMO
              INTEGER N, IARGC, I
              CHARACTER*8 ARG
              CHARACTER*12 VALUE
              N = IARGC ( )
              PRINT '(A,I2)', 'arguments', N
              DO I = 1, N
                 CALL GETARG ( I, ARG )
                 PRINT '(A,I2,3A)', 'argument', I, ' [', ARG, ']'
              END DO
              CALL GETARG ( N + 1, ARG )
              PRINT '(3A)', 'past the last [', ARG, ']'
              CALL GETENV ( 'FORTRANIA_DEMO_VALUE', VALUE )
              PRINT '(3A)', 'set [', VALUE, ']'
              CALL GETENV ( 'FORTRANIA_DEMO_UNSET', VALUE )
              PRINT '(3A)', 'unset [', VALUE, ']'
              CALL EXIT ( 3 )
              PRINT '(A)', 'not reached'
              END
        """;

    private const string Driver =
        """
            public static string Run()
            {
                Environment.SetEnvironmentVariable("FORTRANIA_DEMO_VALUE", "hello there");
                using var output = new System.IO.StringWriter();
                using var context = new FortranContext { CommandLine = ["demo", "one", "two words"] };
                context.IO.Output = output;
                context.IO.NewLine = "\n";
                try
                {
                    context.Get<Library>().DEMO();
                    return output + "ran to its end";
                }
                catch (FortranStopException stop)
                {
                    return output + "exit " + stop.ExitCode;
                }
            }
        """;

    [Fact]
    public void Run_WhenAMainProgramUsesItsCommandLineItsEnvironmentAndExit_ShouldDoAsTheReferenceCompilersProgram()
    {
        // Arrange
        var program = TranslatedProgram.Compile(Source, Driver);

        // Act
        var result = program.Run<string>("Run");

        // Assert
        result.ShouldBe(
            """
            arguments 2
            argument 1 [one     ]
            argument 2 [two word]
            past the last [        ]
            set [hello there ]
            unset [            ]
            exit 3
            """.ReplaceLineEndings("\n"));
    }

    [Fact]
    public void Emit_WhenAUnitIsAMainProgram_ShouldWriteAMethodThatTakesNothingUnderItsName()
    {
        // Act
        var named = TranslatedProgram.Translate(Source)[0];
        var unnamed = TranslatedProgram.Translate("      INTEGER N\n      N = 1\n      END\n")[0];

        // Assert
        named.ShouldContain("    public void DEMO()\n");
        unnamed.ShouldContain("    public void MAIN()\n");
    }

    [Fact]
    public void Emit_WhenAProcedureOfTheCompilersLibraryIsCalled_ShouldCallItOnTheContext()
    {
        // Arrange
        const string Calls =
            """
                  SUBROUTINE CALLS ( NAME, VALUE, STATUS, CLOCK, PARTS )
                  CHARACTER*(*) NAME, VALUE, CLOCK ( 3 )
                  INTEGER STATUS, PARTS ( 8 )
                  CALL GETENV ( NAME, VALUE )
                  CALL SYSTEM ( NAME, STATUS )
                  CALL DATE_AND_TIME ( CLOCK ( 1 ), CLOCK ( 2 ), CLOCK ( 3 ),
                 .                     PARTS )
                  CALL EXIT
                  END
            """;

        // Act
        var file = TranslatedProgram.Translate(Calls, new Dracocephalum.Fortrania.Emit.EmitOptions("N", "C")
        {
            Convention = Dracocephalum.Fortrania.Emit.EmitConvention.F2c,
        })[0];

        // Assert
        file.ShouldContain("_context.GetEnvironment(name, name_len, value, value_len);");
        file.ShouldContain("_context.RunCommand(name, name_len, status);");
        file.ShouldContain("_context.DateAndTime(clock, clock_len, clock + 1 * clock_len, clock_len, clock + 2 * clock_len, clock_len, parts);");
        file.ShouldContain("_context.Exit();");
    }

    [Fact]
    public void Emit_WhenTheProgramDefinesAProcedureOfSuchAName_ShouldCallTheProgramsOwn()
    {
        // Arrange
        const string Own =
            """
                  SUBROUTINE EXIT ( CODE )
                  INTEGER CODE
                  CODE = CODE + 1
                  END

                  SUBROUTINE LEAVE ( CODE )
                  INTEGER CODE
                  CALL EXIT ( CODE )
                  END
            """;

        // Act
        var file = TranslatedProgram.Translate(Own)[1];

        // Assert
        file.ShouldContain("        EXIT(code);\n");
        file.ShouldNotContain("_context.Exit");
    }
}
