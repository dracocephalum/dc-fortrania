using Dracocephalum.Fortrania.Emit;
using Shouldly;

namespace Dracocephalum.Fortrania.Tests.Emit;

/// <summary>
/// Runs the translated sample of files and unformatted records. Every
/// expected value is what the reference compiler's program printed for the
/// same source.
/// </summary>
public class CSharpEmitterInputOutputTests
{
    private const string Driver =
        """
            private static string NewName() =>
                System.IO.Path.Combine(AppContext.BaseDirectory, Guid.NewGuid().ToString("N") + ".bin");

            public static object[] Records()
            {
                using var library = new Standalone();
                var name = System.Text.Encoding.UTF8.GetBytes(NewName());
                var results = new int[24];
                byte* word = stackalloc byte[8];
                double* values = stackalloc double[2];
                fixed (byte* file = name)
                fixed (int* first = results)
                {
                    library.RECS(file, name.Length, first, word, 8, values);
                }

                return [results, System.Text.Encoding.ASCII.GetString(word, 8), values[0], values[1]];
            }

            public static int Fails()
            {
                using var library = new Standalone();
                var name = System.Text.Encoding.UTF8.GetBytes(NewName());
                fixed (byte* file = name)
                {
                    try
                    {
                        library.FAILS(file, name.Length);
                        return 0;
                    }
                    catch (FortranIOException exception)
                    {
                        return exception.Status;
                    }
                }
            }
        """;

    private static readonly Lazy<TranslatedProgram> Program = new(() => TranslatedProgram.Compile(RecordSample.Source, Driver));

    [Fact]
    public void Run_WhenRecordsAreWrittenAndReadByNumber_ShouldTransferEveryItemAndGiveEachStatementItsStatus()
    {
        // Act
        var results = Program.Value.Run<object[]>("Records");

        // Assert
        results[0].ShouldBe(new[] { 0, -1, 0, 0, 8, 0, 10, 9, 8, 7, 0, 0, 5002, 5016, 5015, 111, 11, 11, 11, 0, 0, 2, -1, 0 });
        results[1].ShouldBe("ABCDEFGH");
        results[2].ShouldBe(1.5);
        results[3].ShouldBe(-0.75);
    }

    [Fact]
    public void Run_WhenAStatementWithoutIostatFails_ShouldThrowWithTheStatusItWouldHaveGiven() =>
        Program.Value.Run<int>("Fails").ShouldBe(2);

    [Fact]
    public void Emit_WhenAStatementTransfersRecords_ShouldBeginItPassEachItemAndEndIt()
    {
        // Act
        var file = TranslatedProgram.Translate(RecordSample.Source)[0];

        // Assert
        file.ShouldContain(
            """
                    _io.WriteDirect(unit, 4);
                    for (i = 4; i >= 1; i -= 1)
                    {
                        _io.Put(&ia[i - 1], 4);
                    }
                    _io.Put(Arg(&_d1, d[0] + d[1]), 8);
                    _io.Require(_io.EndWrite());
                    _io.ReadDirect(unit, 2);
                    _io.Get(word, 8);
                    _io.Get(vals, 16);
                    _io.Get(&@out[4], 4);
                    ios = _io.EndRead();
            """.ReplaceLineEndings("\n"),
            Case.Sensitive);
    }

    [Fact]
    public void Emit_WhenAStatementConnectsOrInquires_ShouldPassEachSpecifierByName()
    {
        // Act
        var file = TranslatedProgram.Translate(RecordSample.Source)[0];

        // Assert
        file.ShouldContain("ios = _io.Open(unit, file: new(name, name_len), status: \"NEW\"u8, access: \"DIRECT\"u8, recl: reclen);");
        file.ShouldContain("ios = _io.Close(unit, status: \"DELETE\"u8);");
        file.ShouldContain("_io.Require(_io.InquireUnit(unit, opened: &op, number: &n, named: &nmd, name: found, nameLength: 255));");
        file.ShouldContain("ios = _io.InquireFile(new(name, name_len), exist: &ex, opened: &op, number: &n);");
        file.ShouldContain("ios = _io.Rewind(unit);");
    }

    [Fact]
    public void Emit_WhenAWholeArrayOfAdjustableSizeIsTransferred_ShouldWorkOutItsBytesWhenTheProgramRuns()
    {
        // Arrange
        const string Source =
            """
                  SUBROUTINE DUMP ( UNIT, N, M, TABLE, NAMES )
                  INTEGER UNIT, N, M
                  DOUBLE PRECISION TABLE ( 2, 0:N )
                  CHARACTER*(*) NAMES ( M )
                  WRITE ( UNIT, REC=1 ) TABLE, NAMES
                  END
            """;

        // Act
        var file = TranslatedProgram.Translate(Source)[0];

        // Assert
        file.ShouldContain("_io.Put(table, 16 * (*n - 0 + 1));");
        file.ShouldContain("_io.Put(names, names_len * *m);");
    }

    [Theory]
    [InlineData("      SUBROUTINE S ( X )|      READ ( 9 ) X|      END", "Line 2: unformatted input or output without a record number cannot be translated yet.")]
    [InlineData("      SUBROUTINE S ( X )|      ENDFILE ( 9 )|      END", "Line 2: ENDFILE cannot be translated yet.")]
    [InlineData("      SUBROUTINE S ( X )|      OPEN ( 9, BLANK='ZERO' )|      END", "Line 2: OPEN with BLANK cannot be translated yet.")]
    [InlineData("      SUBROUTINE S ( X )|      INTEGER N|      INQUIRE ( 9, RECL=N )|      END", "Line 3: INQUIRE with RECL cannot be translated yet.")]
    [InlineData("      SUBROUTINE S ( X, T )|      DOUBLE PRECISION T ( * )|      WRITE ( 9, REC=1 ) T|      END", "Line 3: an array of assumed size in an input or output list cannot be translated yet.")]
    public void Emit_WhenAStatementIsOfAKindNotTranslatedYet_ShouldSayWhatAndWhere(string source, string message)
    {
        // Act & Assert
        Should.Throw<UnsupportedConstructException>(() => TranslatedProgram.Translate(source.Replace('|', '\n')))
            .Message.ShouldBe(message);
    }
}
