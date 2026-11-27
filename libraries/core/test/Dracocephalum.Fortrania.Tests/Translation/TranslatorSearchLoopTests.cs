using Dracocephalum.Fortrania.Tests.Emit;
using Dracocephalum.Fortrania.Translation;
using Shouldly;

namespace Dracocephalum.Fortrania.Tests.Translation;

public sealed class TranslatorSearchLoopTests : IDisposable
{
    private const string Driver =
        """
        using System.Collections.Generic;
        using Dracocephalum.Fortrania.Runtime;

        namespace Translated;

        public static unsafe class Driver
        {
            public static int[] Run()
            {
                using var context = new FortranContext();
                var core = context.Get<Example.Toolkit.Core>();
                var array = stackalloc int[40];
                for (var index = 0; index < 40; index++)
                {
                    array[index] = 100 + (index % 30);
                }

                var results = new List<int>();
                foreach (var (value, count) in new[] { (100, 40), (129, 40), (105, 40), (999, 40), (139, 40), (103, 4), (104, 4), (100, 0), (100, -3), (115, 16), (116, 16) })
                {
                    int sought = value, n = count;
                    results.Add(core.WHERE(&sought, &n, array));
                    results.Add(core.KNOWN(&sought, array));
                }

                return [.. results];
            }
        }
        """;

    private readonly string _root = Path.Combine(AppContext.BaseDirectory, "search-" + Guid.NewGuid().ToString("N"));

    public TranslatorSearchLoopTests() => Directory.CreateDirectory(_root);

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Run_WhenALoopOnlyLooksForAWholeNumber_ShouldHaveTheRuntimeLookThroughALongRunAndGiveWhatTheLoopGives(bool keepsLoops)
    {
        // Arrange: a search of a length known only as the program runs,
        // which keeps its loop for a short run, and one of a length known
        // here. Each gives the place found, or what the loop's variable
        // is left as where nothing is found, taken a hundred times.
        Put("""
                  INTEGER FUNCTION WHERE ( VALUE, N, ARRAY )
                  INTEGER VALUE, N, ARRAY ( * ), I
                  DO I = 1, N
                     IF ( ARRAY ( I ) .EQ. VALUE ) THEN
                        WHERE = I
                        RETURN
                     END IF
                  END DO
                  WHERE = 100 * I
                  END
                  INTEGER FUNCTION KNOWN ( VALUE, ARRAY )
                  INTEGER VALUE, ARRAY ( 40 ), I
                  DO I = 1, 40
                     IF ( VALUE .EQ. ARRAY ( I ) ) THEN
                        KNOWN = I
                        RETURN
                     END IF
                  END DO
                  KNOWN = 100 * I
                  END
            """);

        // Act
        var result = Translate(keepsLoops);

        // Assert
        result.Succeeded.ShouldBeTrue(string.Join('\n', result.Messages));
        var where = Text("WHERE.g.cs");
        var known = Text("KNOWN.g.cs");
        where.ShouldContain("for (i = 1; i <= _last1; i += 1)");
        if (keepsLoops)
        {
            where.ShouldNotContain("FindElement");
            known.ShouldNotContain("FindElement");
        }
        else
        {
            where.ShouldContain("/* A long run is looked through by the runtime and a short one by the loop; \"plainArrayLoops\": true in the translation's settings writes the loop alone. */");
            where.ShouldContain("if (_last1 >= LongRun)");
            where.ShouldContain("i = FindElement(array, _last1, value_value);");
            where.ShouldContain("if (i <= _last1)");
            known.ShouldContain("/* A loop that looks for a value, done by the runtime; \"plainArrayLoops\": true in the translation's settings writes the loop. */");
            known.ShouldContain("i = FindElement(array, 40, *value);");
            known.ShouldNotContain("for (");
        }

        // Found first, found at the thirtieth place, found in between,
        // and twice not found; a short run with and without a find; no run at all; and a run of the length at
        // which the runtime is asked, with a find at its end and none.
        TranslatedProgram
            .FromSources(Directory.GetFiles(Path.Combine(_root, "out"), "*.cs", SearchOption.AllDirectories).Select(File.ReadAllText).Append(Driver))
            .Run<int[]>("Run")
            .ShouldBe([1, 1, 30, 30, 6, 6, 4100, 4100, 4100, 4100, 4, 4, 500, 5, 100, 1, 100, 1, 16, 16, 1700, 17]);
    }

    [Fact]
    public void Run_WhenALoopLooksForARealValueOrDoesMoreThanLook_ShouldLeaveTheLoop()
    {
        // Arrange: a real value, which the framework's search and the
        // loop's test take differently where it is no number; a find that
        // does not leave the procedure, so that the loop goes on to the
        // next; and a second statement in the loop.
        Put("""
                  INTEGER FUNCTION REALS ( VALUE, N, ARRAY )
                  INTEGER N, I
                  DOUBLE PRECISION VALUE, ARRAY ( * )
                  REALS = 0
                  DO I = 1, N
                     IF ( ARRAY ( I ) .EQ. VALUE ) THEN
                        REALS = I
                        RETURN
                     END IF
                  END DO
                  END
                  INTEGER FUNCTION LAST ( VALUE, N, ARRAY )
                  INTEGER VALUE, N, ARRAY ( * ), I
                  LAST = 0
                  DO I = 1, N
                     IF ( ARRAY ( I ) .EQ. VALUE ) THEN
                        LAST = I
                     END IF
                  END DO
                  END
                  INTEGER FUNCTION TALLY ( VALUE, N, ARRAY )
                  INTEGER VALUE, N, ARRAY ( * ), I
                  TALLY = 0
                  DO I = 1, N
                     TALLY = TALLY + 1
                     IF ( ARRAY ( I ) .EQ. VALUE ) THEN
                        RETURN
                     END IF
                  END DO
                  END
            """);

        // Act
        var result = Translate(keepsLoops: false);

        // Assert
        result.Succeeded.ShouldBeTrue(string.Join('\n', result.Messages));
        Text("REALS.g.cs").ShouldNotContain("FindElement");
        Text("LAST.g.cs").ShouldNotContain("FindElement");
        Text("TALLY.g.cs").ShouldNotContain("FindElement");
    }

    private TranslationResult Translate(bool keepsLoops) => Translator.Run(
        new TranslationProject
        {
            Name = "Example.Toolkit",
            Settings =
            [
                new TranslationSettings
                {
                    Folder = _root,
                    Output = "out",
                    PlainArrayLoops = keepsLoops,
                    Sources = [new SourceSettings { Name = "Core", Files = [new FileSettings { Paths = ["core/*.f"] }] }],
                },
            ],
            Summary = Path.Combine(_root, "out", ProjectSummary.GetFileName("Example.Toolkit")),
        },
        new TranslationOptions());

    private void Put(string text)
    {
        Directory.CreateDirectory(Path.Combine(_root, "core"));
        File.WriteAllText(Path.Combine(_root, "core", "search.f"), text);
    }

    // A file of the library's translation.
    private string Text(string name) =>
        File.ReadAllText(Path.Combine(_root, "out", Translator.GeneratedFolder, "Core", name)).ReplaceLineEndings("\n");
}
