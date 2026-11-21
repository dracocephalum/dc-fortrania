namespace Dracocephalum.Fortrania.Tests.Emit;

/// <summary>The numeric sample, translated and compiled once for all the tests that run it.</summary>
public sealed class NumericSampleFixture
{
    // Each method makes the calls the reference program made, with the same
    // values, and returns what that program printed.
    private const string Driver =
        """
            public static double Arith(double value, int power)
            {
                return new Standalone().ARITH(&value, &power);
            }

            public static double ArithOfTwoAndAHalf() => Arith(2.5, 3);

            public static double ArithOfThreeTenths() => Arith(0.3, -2);

            public static int[] IntegerArith()
            {
                var library = new Standalone();
                int seven = 7, two = 2, minusNine = -9, four = 4;
                return [library.IARITH(&seven, &two), library.IARITH(&minusNine, &four)];
            }

            public static float RealArith()
            {
                float value = 2.7f;
                int power = 3;
                return new Standalone().RARITH(&value, &power);
            }

            public static int[] Loops(int count)
            {
                var results = new int[8];
                fixed (int* first = results)
                {
                    new Standalone().LOOPS(&count, first);
                }

                return results;
            }

            public static int[] LoopsOfTen() => Loops(10);

            public static int[] LoopsOfNone() => Loops(0);

            public static double[] Arrays()
            {
                double* a = stackalloc double[6];
                double* b = stackalloc double[4];
                for (var j = 1; j <= 2; j++)
                {
                    for (var i = 1; i <= 3; i++)
                    {
                        a[(i - 1) + (j - 1) * 3] = i + 0.5 * j;
                    }
                }

                for (var i = 0; i <= 3; i++)
                {
                    b[i] = 1 << i;
                }

                int n = 3;
                var result = new Standalone().ARRAYS(&n, a, b);
                return [result, a[0]];
            }

            public static double[] Calls()
            {
                double x = 1.5, y = 4.0;
                var result = new Standalone().CALLS(&x, &y);
                return [result, x, y];
            }

            public static int[] Branch()
            {
                var library = new Standalone();
                int minusFive = -5, zero = 0, twentySeven = 27, one = 1, yes = 1, no = 0;
                return
                [
                    library.BRANCH(&minusFive, &yes),
                    library.BRANCH(&zero, &yes),
                    library.BRANCH(&twentySeven, &yes),
                    library.BRANCH(&twentySeven, &no),
                    library.BRANCH(&one, &yes),
                ];
            }
        """;

    public NumericSampleFixture() => Program = TranslatedProgram.Compile(NumericSample.Source, Driver);

    internal TranslatedProgram Program { get; }
}
