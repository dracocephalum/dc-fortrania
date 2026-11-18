using Shouldly;

namespace Dracocephalum.Fortrania.Tests.Emit;

/// <summary>
/// Runs the translated sample of list-directed text, and sets what it
/// wrote against what the reference compiler's program wrote.
/// </summary>
public class CSharpEmitterListDirectedTests
{
    private const string Driver =
        """
            private static string NewName() =>
                System.IO.Path.Combine(AppContext.BaseDirectory, Guid.NewGuid().ToString("N") + ".txt");

            private static string TakeText(string path)
            {
                var text = System.IO.File.ReadAllText(path);
                System.IO.File.Delete(path);
                return text;
            }

            public static string Output()
            {
                var path = NewName();
                var name = System.Text.Encoding.UTF8.GetBytes(path);
                using (var library = new Library())
                {
                    fixed (byte* file = name)
                    {
                        library.LDOUT(file, name.Length);
                    }
                }

                return TakeText(path);
            }

            public static string Report()
            {
                var path = NewName();
                var name = System.Text.Encoding.UTF8.GetBytes(NewName());
                var log = System.Text.Encoding.UTF8.GetBytes(path);
                using (var library = new Library())
                {
                    fixed (byte* file = name, report = log)
                    {
                        library.LDIN(file, name.Length, report, log.Length);
                    }
                }

                return TakeText(path);
            }
        """;

    private static readonly Lazy<TranslatedProgram> Program = new(() => TranslatedProgram.Compile(ListDirectedSample.Source, Driver));

    [Fact]
    public void Run_WhenValuesAreWrittenListDirected_ShouldLayEveryLineOutAsTheReferenceCompilerDoes() =>
        Lines(Program.Value.Run<string>("Output")).ShouldBe(Expected(ListDirectedSample.ExpectedOutput));

    [Fact]
    public void Run_WhenValuesAreReadListDirected_ShouldTakeThemApartAndGiveStatusesAsTheReferenceCompilerDoes() =>
        Lines(Program.Value.Run<string>("Report")).ShouldBe(Expected(ListDirectedSample.ExpectedReport));

    [Fact]
    public void Emit_WhenAStatementIsListDirected_ShouldBeginItWithItsUnitAloneAndPassEachItemWithItsType()
    {
        // Arrange
        const string Source =
            """
                  SUBROUTINE SHOW ( UNIT, NAME, N, X, TABLE )
                  INTEGER UNIT, N, TABLE ( 3 ), IOS
                  DOUBLE PRECISION X
                  CHARACTER*(*) NAME
                  WRITE ( UNIT, *, IOSTAT=IOS ) NAME, N + 1, X
                  PRINT *, TABLE
                  READ ( UNIT, * ) NAME, N, X
                  READ ( *, * ) TABLE
                  END
            """;

        // Act
        var file = TranslatedProgram.Translate(Source)[0];

        // Assert
        file.ShouldContain(
            """
                    _io.WriteList(*unit);
                    _io.Write(name, name_len);
                    _io.Write(*n + 1);
                    _io.Write(*x);
                    ios = _io.EndWrite();
                    _io.WriteList(6);
                    _io.Write(table, 3);
                    _io.Require(_io.EndWrite());
                    _io.ReadList(*unit);
                    _io.Read(name, name_len);
                    _io.Read(n);
                    _io.Read(x);
                    _io.Require(_io.EndRead());
                    _io.ReadList(5);
                    _io.Read(table, 3);
                    _io.Require(_io.EndRead());
            """.ReplaceLineEndings("\n"),
            Case.Sensitive);
    }

    private static string[] Lines(string text) => text.ReplaceLineEndings("\n").Split('\n');

    // The expected lines, each without the bar that marks its end, and
    // with the empty line the file's last line ending leaves.
    private static string[] Expected(string lines) =>
        [.. lines.ReplaceLineEndings("\n").Split('\n').Select(line => line[..^1]), string.Empty];
}
