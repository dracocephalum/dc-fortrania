using Shouldly;

namespace Dracocephalum.Fortrania.Tests.Emit;

/// <summary>
/// Translates a unit with a comment in every place a comment can stand, and
/// checks that each is carried to where it stands in the C#, and that the
/// code still does what it did.
/// </summary>
public class CSharpEmitterCommentTests
{
    private const string Source =
        """
        C$Procedure SHOW ( Show the comments )
              SUBROUTINE SHOW ( N, M )
        C
        C$ Abstract
        C
        C     Lined   up    text   stays   lined   up.
        C

              IMPLICIT NONE
        *     The arguments.
              INTEGER N, M
              INTEGER I


        C     Executable code starts here.
              M = 0
              DO I = 1, N
        c        Inside the loop.
                 M = M + I
        C        Before the END DO.
              END DO
              IF ( M .GT. 10 ) THEN
        C        In the first arm.
                 M = 10
        C        About the second arm.
              ELSE IF ( M .GT. 5 ) THEN
                 M = 5
              ELSE
        !        In the third arm.
                 M = M +
        C        Between the lines of one statement.
             .       1
        C        Before the END IF.
              END IF
        C     Before the END.
              END

        C
        C     An appendix after the END.

        """;

    private const string Driver =
        """
            public static int[] Show()
            {
                var library = new Standalone();
                var results = new int[3];
                int[] counts = [4, 2, 9];
                for (var index = 0; index < 3; index++)
                {
                    int n = counts[index], m;
                    library.SHOW(&n, &m);
                    results[index] = m;
                }

                return results;
            }
        """;

    private static readonly Lazy<string> File = new(() => TranslatedProgram.Translate(Source)[0]);

    [Fact]
    public void Emit_WhenCommentsComeBeforeTheUnitsHeading_ShouldWriteThemBeforeTheMethod() =>
        File.Value.ShouldContain(
            """
                // $Procedure SHOW ( Show the comments )
                public void SHOW(int* n, int* m)
                {
            """.ReplaceLineEndings("\n"));

    [Fact]
    public void Emit_WhenCommentsComeWithDeclarations_ShouldWriteThemAtTheTopOfTheMethodAsTheyStand()
    {
        // Act
        var file = File.Value;

        // Assert: a space after the comment characters, the indentation the
        // lines of a block share taken off and the rest as it stands, a run
        // of blank lines as one, and all of it before what is declared.
        file.ShouldContain(
            """
                {
                    //
                    // $ Abstract
                    //
                    //      Lined   up    text   stays   lined   up.
                    //

                    // The arguments.

            """.ReplaceLineEndings("\n"));
        file.IndexOf("The arguments.", StringComparison.Ordinal)
            .ShouldBeLessThan(file.IndexOf("int i = 0;", StringComparison.Ordinal));
    }

    [Fact]
    public void Emit_WhenCommentsComeBeforeTheFirstExecutableStatement_ShouldWriteThemAfterTheDeclarationsAndABlankLine() =>
        File.Value.ShouldContain("        int i = 0;\n\n        // Executable code starts here.\n        *m = 0;\n");

    [Fact]
    public void Emit_WhenCommentsComeAmongExecutableStatements_ShouldWriteThemBeforeTheCodeAtItsIndentation()
    {
        // Act
        var file = File.Value;

        // Assert
        file.ShouldContain("        {\n            // Inside the loop.\n            *m = ");
        file.ShouldContain("        {\n            // In the first arm.\n            *m = 10;\n");
        file.ShouldContain("        else if (*m > 5)\n        {\n            // About the second arm.\n");
        file.ShouldContain("            // In the third arm.\n            // Between the lines of one statement.\n            *m = *m + 1;\n");
    }

    [Fact]
    public void Emit_WhenCommentsFollowCode_ShouldWriteABlankLineBeforeThem()
    {
        // Act
        var file = File.Value;

        // Assert
        file.ShouldContain("            *m = *m + i;\n\n            // Before the END DO.\n        }\n");
        file.ShouldContain("            *m = *m + 1;\n\n            // Before the END IF.\n        }\n");
        file.ShouldContain("        }\n\n        // Before the END.\n    }\n");
    }

    [Fact]
    public void Emit_WhenCommentsComeAfterTheLastEnd_ShouldWriteThemAfterTheLastMethod() =>
        File.Value.ShouldEndWith("    }\n\n    //\n    // An appendix after the END.\n}\n");

    [Fact]
    public void Emit_WhenTheLinesOfABlockAreIndentedUnevenly_ShouldKeepWhatIsLinedUpAndTrimTheirEnds()
    {
        // Arrange: the spaces that end the second comment are spelled out,
        // where an editor would not take them off.
        const string Source =
            "      SUBROUTINE S\n" +
            "C        Two columns:\n" +
            "C           first      second" + "   \n" +
            "C\n" +
            "C        end.\n" +
            "      END\n";

        // Act
        var file = TranslatedProgram.Translate(Source)[0];

        // Assert
        file.ShouldContain("        // Two columns:\n        //    first      second\n        //\n        // end.\n");
    }

    [Fact]
    public void Run_WhenTheSourceHasComments_ShouldDoWhatItDid() =>
        TranslatedProgram.Compile(Source, Driver).Run<int[]>("Show").ShouldBe([5, 4, 10]);
}
