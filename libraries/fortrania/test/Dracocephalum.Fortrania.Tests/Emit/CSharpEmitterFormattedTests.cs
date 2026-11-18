using Dracocephalum.Fortrania.Emit;
using Shouldly;

namespace Dracocephalum.Fortrania.Tests.Emit;

/// <summary>
/// Runs the translated sample of text written and read by formats, and
/// sets what it wrote against what the reference compiler's program wrote.
/// </summary>
public class CSharpEmitterFormattedTests
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
                        library.FMTOUT(file, name.Length);
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
                        library.FMTIN(file, name.Length, report, log.Length);
                    }
                }

                return TakeText(path);
            }
        """;

    private static readonly Lazy<TranslatedProgram> Program = new(() => TranslatedProgram.Compile(FormattedSample.Source, Driver));

    [Fact]
    public void Run_WhenValuesAreWrittenByFormats_ShouldWriteEveryLineAsTheReferenceCompilerDoes() =>
        Lines(Program.Value.Run<string>("Output")).ShouldBe(Expected(FormattedSample.ExpectedOutput));

    [Fact]
    public void Run_WhenTextIsReadByFormats_ShouldReadGiveStatusesAndPositionAsTheReferenceCompilerDoes() =>
        Lines(Program.Value.Run<string>("Report")).ShouldBe(Expected(FormattedSample.ExpectedReport));

    [Fact]
    public void Emit_WhenAStatementTransfersTextByAFormat_ShouldPassTheFormatAndEachItemWithItsType()
    {
        // Arrange
        const string Source =
            """
                  SUBROUTINE SHOW ( UNIT, NAME, N, X, FLAG, TABLE, WORDS, F )
                  INTEGER UNIT, N, TABLE ( 3 ), IOS
                  DOUBLE PRECISION X
                  LOGICAL FLAG
                  CHARACTER*(*) NAME, WORDS ( 2 ), F
                  CHARACTER*4 TAG
                  WRITE ( UNIT, '(A,I4)', IOSTAT=IOS ) TAG // ':', N + 1
                  WRITE ( *, F ) X, FLAG, TABLE, WORDS
                  PRINT '(A)', NAME
                  READ ( UNIT, '(A,I4,F8.2,L2)' ) NAME ( 2:3 ), N, X, FLAG
                  READ ( *, '(3I4,2A)' ) TABLE, WORDS
                  END
            """;

        // Act
        var file = TranslatedProgram.Translate(Source)[0];

        // Assert
        file.ShouldContain(
            """
                    _io.WriteFormatted(*unit, "(A,I4)"u8);
                    _io.Write(Concat(_s1, 5).Add(tag, 4).Add(AddrOf(":"u8), 1).End(&_i1), _i1);
                    _io.Write(*n + 1);
                    ios = _io.EndWrite();
                    _io.WriteFormatted(6, new(f, f_len));
                    _io.Write(*x);
                    _io.Write(*flag != 0);
                    _io.Write(table, 3);
                    _io.Write(words, words_len, 2);
                    _io.Require(_io.EndWrite());
                    _io.WriteFormatted(6, "(A)"u8);
                    _io.Write(name, name_len);
                    _io.Require(_io.EndWrite());
                    _io.ReadFormatted(*unit, "(A,I4,F8.2,L2)"u8);
                    _io.Read(name + 1, 2);
                    _io.Read(n);
                    _io.Read(x);
                    _io.ReadLogical(flag);
                    _io.Require(_io.EndRead());
                    _io.ReadFormatted(5, "(3I4,2A)"u8);
                    _io.Read(table, 3);
                    _io.Read(words, words_len, 2);
                    _io.Require(_io.EndRead());
            """.ReplaceLineEndings("\n"),
            Case.Sensitive);
    }

    [Theory]
    [InlineData("      SUBROUTINE S ( X )|      WRITE ( *, * ) X|      END", "Line 2: list-directed input or output cannot be translated yet.")]
    [InlineData("      SUBROUTINE S ( X )|      PRINT *, X|      END", "Line 2: list-directed input or output cannot be translated yet.")]
    [InlineData("      SUBROUTINE S ( X )|      READ ( 5, * ) X|      END", "Line 2: list-directed input or output cannot be translated yet.")]
    [InlineData("      SUBROUTINE S ( C )|      CHARACTER*8 C|      WRITE ( C, '(A)' ) 'X'|      END", "Line 3: input or output on a character variable cannot be translated yet.")]
    [InlineData("      SUBROUTINE S ( C )|      CHARACTER*8 C|      WRITE ( 9, '(A)', REC=2 ) C|      END", "Line 3: formatted input or output by record number cannot be translated yet.")]
    public void Emit_WhenAStatementIsOfAKindNotTranslatedYet_ShouldSayWhatAndWhere(string source, string message)
    {
        // Act & Assert
        Should.Throw<UnsupportedConstructException>(() => TranslatedProgram.Translate(source.Replace('|', '\n')))
            .Message.ShouldBe(message);
    }

    private static string[] Lines(string text) => text.ReplaceLineEndings("\n").Split('\n');

    // The expected lines, each without the bar that marks its end, and
    // with the empty line the file's last line ending leaves.
    private static string[] Expected(string lines) =>
        [.. lines.ReplaceLineEndings("\n").Split('\n').Select(line => line[..^1]), string.Empty];
}
