using Shouldly;

namespace Dracocephalum.Fortrania.Tests.Emit;

/// <summary>
/// Runs the translated sample of internal files, <c>BACKSPACE</c> and
/// <c>ISHFT</c>, and sets what it wrote against what the reference
/// compiler's program wrote.
/// </summary>
public class CSharpEmitterInternalFileTests
{
    private const string Driver =
        """
            private static string NewName() =>
                System.IO.Path.Combine(AppContext.BaseDirectory, Guid.NewGuid().ToString("N") + ".txt");

            public static string Report()
            {
                var path = NewName();
                var name = System.Text.Encoding.UTF8.GetBytes(NewName());
                var log = System.Text.Encoding.UTF8.GetBytes(path);
                using (var library = new Standalone())
                {
                    fixed (byte* file = name, report = log)
                    {
                        library.INTF(file, name.Length, report, log.Length);
                    }
                }

                var text = System.IO.File.ReadAllText(path);
                System.IO.File.Delete(path);
                return text;
            }
        """;

    [Fact]
    public void Run_WhenCharacterVariablesAreFilesAndAFileIsBackspaced_ShouldDoAsTheReferenceCompilersProgram()
    {
        // Arrange
        var program = TranslatedProgram.Compile(InternalFileSample.Source, Driver);

        // Act
        var report = program.Run<string>("Report").ReplaceLineEndings("\n").Split('\n');

        // Assert
        report.ShouldBe([.. InternalFileSample.ExpectedReport.ReplaceLineEndings("\n").Split('\n').Select(line => line[..^1]), string.Empty]);
    }

    [Fact]
    public void Emit_WhenTheUnitIsACharacterValue_ShouldPassItsAddressTheLengthOfARecordAndTheNumberOfRecords()
    {
        // Arrange
        const string Source =
            """
                  SUBROUTINE SHOW ( N, LINE, LINES, M )
                  INTEGER N, M
                  CHARACTER*(*) LINE, LINES ( M )
                  CHARACTER*10 BOX ( 3 )
                  WRITE ( LINE, '(I4)' ) N
                  WRITE ( LINE ( 2:5 ), * ) N
                  READ ( LINES, '(I4)' ) N
                  READ ( LINES ( 2 ), * ) N
                  WRITE ( BOX, '(I4)' ) N
                  BACKSPACE ( N )
                  N = ISHFT ( N, -M )
                  END
            """;

        // Act
        var file = TranslatedProgram.Translate(Source)[0];

        // Assert
        file.ShouldContain("_io.WriteFormatted(line, line_len, 1, \"(I4)\"u8);");
        file.ShouldContain("_io.WriteList(line + 1, 4, 1);");
        file.ShouldContain("_io.ReadFormatted(lines, lines_len, *m, \"(I4)\"u8);");
        file.ShouldContain("_io.ReadList(lines + 1 * lines_len, lines_len, 1);");
        file.ShouldContain("_io.WriteFormatted(box, 10, 3, \"(I4)\"u8);");
        file.ShouldContain("_io.Require(_io.Backspace(*n));");
        file.ShouldContain("*n = Ishft(*n, -*m);");
    }
}
