using Shouldly;

namespace Dracocephalum.Fortrania.Tests.Emit;

/// <summary>
/// Runs the translated concatenation sample. Every expected value is what
/// the reference compiler's program printed for the same source and
/// arguments.
/// </summary>
public class CSharpEmitterConcatenationTests
{
    // Each method makes the calls the reference program made, with the same
    // values, and returns what that program printed.
    private const string Driver =
        """
            private static string Read(byte* text, int length) => System.Text.Encoding.ASCII.GetString(text, length);

            private static void Write(byte* text, int length, string value)
            {
                for (var index = 0; index < length; index++)
                {
                    text[index] = (byte)(index < value.Length ? value[index] : ' ');
                }
            }

            public static string Join()
            {
                byte* first = stackalloc byte[2];
                byte* second = stackalloc byte[5];
                byte* result = stackalloc byte[60];
                Write(first, 2, "ab");
                Write(second, 5, "CDEFG");
                Write(result, 60, string.Empty);
                new Standalone().CJOIN(first, 2, second, 5, result, 60);
                return Read(result, 60);
            }

            public static int[] Arguments()
            {
                var library = new Standalone();
                int three = 3, five = 5;
                return [library.CJARG(&three), library.CJARG(&five)];
            }

            public static string Functions()
            {
                byte* name = stackalloc byte[3];
                byte* result = stackalloc byte[36];
                Write(name, 3, "Ada");
                Write(result, 36, new string('#', 36));
                new Standalone().CFUNC(name, 3, result, 36);
                return Read(result, 36);
            }

            public static string Fold()
            {
                byte* result = stackalloc byte[32];
                int count;
                Write(result, 32, new string('#', 32));
                new Standalone().CFOLD(result, 32, &count);
                return Read(result, 32) + count;
            }

            public static string FunctionAlone()
            {
                byte* name = stackalloc byte[5];
                byte* result = stackalloc byte[7];
                Write(name, 5, "mixEd");
                var returned = new Standalone().UPPER(result, 7, name, 5);
                return Read(returned, 7) + (returned == result);
            }
        """;

    private static readonly Lazy<TranslatedProgram> Program = new(() => TranslatedProgram.Compile(ConcatenationSample.Source, Driver));

    [Fact]
    public void Run_WhenValuesAreJoinedInAnAssignment_ShouldJoinThemEvenIntoOneOfThemselves() =>
        Program.Value.Run<string>("Join").ShouldBe("abCDEF|b!CDEFG.     cdefabZ             abcdef    kabCDE    ");

    [Fact]
    public void Run_WhenValuesAreJoinedInAnArgumentAComparisonOrAnIntrinsic_ShouldJoinThemInAPlaceOfTheirOwn() =>
        Program.Value.Run<int[]>("Arguments").ShouldBe([5309093, 5311113]);

    [Fact]
    public void Run_WhenCharacterFunctionsAreReferenced_ShouldGiveResultsOfTheLengthTheCallerDeclares() =>
        Program.Value.Run<string>("Functions").ShouldBe("ADA     /ABADA   yesno       ! YES  ");

    [Fact]
    public void Run_WhenConstantsAreJoined_ShouldGiveWhatJoiningThemWhenRunGives() =>
        Program.Value.Run<string>("Fold").ShouldBe("abcd|pre-xy      |middleabend   7061");

    [Fact]
    public void Emit_WhenConstantsAreJoined_ShouldJoinThemWhenTranslating()
    {
        // Act
        var file = TranslatedProgram.Translate(ConcatenationSample.Source).Single(text => text.Contains("void CFOLD(", StringComparison.Ordinal));

        // Assert: a concatenation of constants is one literal, however it
        // is grouped and whatever constants it names; constants side by
        // side among other values are one piece; and only the one
        // concatenation with a variable in it is joined when run.
        file.ShouldContain("AddrOf(\"abcdef\"u8), 6);");
        file.ShouldContain("AddrOf(\"pre-xy\"u8), 6);");
        file.ShouldContain("AddrOf(\"abcpre\"u8), 6");
        file.ShouldContain(".Add(AddrOf(\"|middle\"u8), 7)");
        file.ShouldContain(".Add(AddrOf(\"end\"u8), 3).Pad();");
        file.ShouldContain("1000 * 7");
        file.Split("Concat(").Length.ShouldBe(2);
        file.ShouldNotContain(".End(");
    }

    [Fact]
    public void Run_WhenACharacterFunctionIsCalledFromOutside_ShouldFillThePlaceGivenAndReturnItsAddress() =>
        Program.Value.Run<string>("FunctionAlone").ShouldBe("MIXED  True");
}
