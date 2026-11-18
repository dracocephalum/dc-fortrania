namespace Dracocephalum.Fortrania.Tests.Emit;

/// <summary>The character sample, translated and compiled once for all the tests that run it.</summary>
public sealed class CharacterSampleFixture
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

            public static string Assign()
            {
                byte* brief = stackalloc byte[2];
                byte* longer = stackalloc byte[6];
                byte* result = stackalloc byte[30];
                Write(brief, 2, "ab");
                Write(longer, 6, "LONGER");
                Write(result, 30, new string('#', 30));
                new Library().CASSGN(brief, 2, longer, 6, result, 30);
                return Read(result, 30);
            }

            private static int Compare(string left, string right)
            {
                byte* a = stackalloc byte[left.Length];
                byte* b = stackalloc byte[right.Length];
                Write(a, left.Length, left);
                Write(b, right.Length, right);
                return new Library().CCMP(a, left.Length, b, right.Length);
            }

            public static int[] Compare() =>
            [
                Compare("ABC", "ABC  "),
                Compare("ABC", "ABD"),
                Compare("b", "ABC"),
                Compare("AB", "AB\t"),
                Compare("AB!", "AB"),
            ];

            public static int[] Intrinsics()
            {
                byte* text = stackalloc byte[11];
                Write(text, 11, "HELLO WORLD");
                int code = 65;
                var results = new int[10];
                fixed (int* first = results)
                {
                    new Library().CINTR(text, 11, &code, first);
                }

                return results;
            }

            public static string Calls()
            {
                byte* result = stackalloc byte[27];
                Write(result, 27, string.Empty);
                int number = 0;
                new Library().CCALL(result, 27, &number);
                return Read(result, 27) + "|" + number;
            }
        """;

    public CharacterSampleFixture() => Program = TranslatedProgram.Compile(CharacterSample.Source, Driver);

    internal TranslatedProgram Program { get; }
}
