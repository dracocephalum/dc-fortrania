using System.Runtime.InteropServices;
using Dracocephalum.Fortrania.Tests.Emit;
using Dracocephalum.Fortrania.Translation;
using Shouldly;

namespace Dracocephalum.Fortrania.Tests.Translation;

public sealed class TranslatorVariantTests : IDisposable
{
    // A library whose PLAT says which platform's copy it is, and a function
    // that calls it as any caller would.
    private const string Base =
        """
              SUBROUTINE PLAT ( N )
              INTEGER N
              N = 0
              END

              INTEGER FUNCTION WHICH ( )
              INTEGER N
              CALL PLAT ( N )
              WHICH = N
              END
        """;

    // A driver that asks the library which copy of PLAT runs, for a context
    // told it is on the system named, or the machine's where none is.
    private const string Driver =
        """
        using System.Runtime.InteropServices;
        using Dracocephalum.Fortrania.Runtime;

        namespace Translated;

        public static unsafe class Driver
        {
            public static int OnWindows() => On("windows");

            public static int OnLinux() => On("linux");

            public static int OnMacOS() => On("macos");

            public static int OnOSX() => On("OSX");

            public static int OnXyz() => On("xyz");

            public static int OnSolaris() => On("solaris");

            public static int OnTheMachine()
            {
                using var context = new FortranContext();
                return context.Get<Example.Toolkit.Core>().WHICH();
            }

            private static int On(string system)
            {
                using var context = new FortranContext(new FortranContextOptions { OSPlatform = OSPlatform.Create(system) });
                return context.Get<Example.Toolkit.Core>().WHICH();
            }
        }
        """;

    private readonly string _root = Path.Combine(AppContext.BaseDirectory, "variants-" + Guid.NewGuid().ToString("N"));

    public TranslatorVariantTests() => Directory.CreateDirectory(_root);

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public void Run_WhenAUnitHasVariants_ShouldRunTheOneItsContextIsOn()
    {
        // Arrange
        Put("core/core.f", Base);
        Put("windows/plat.f", Plat(1));
        Put("macos/plat.f", Plat(2));
        Put("xyz/plat.f", Plat(3));
        var source = Source(
            Variant("windows", null, "windows/*.f"),
            Variant("macos", null, "macos/*.f"),
            Variant("xyz", null, "xyz/*.f"));

        // Act
        var result = Translate(source);

        // Assert
        result.Succeeded.ShouldBeTrue(string.Join('\n', result.Messages));
        Files().ShouldBe(
        [
            "Core.g.cs", "PLAT.base.g.cs", "PLAT.g.cs", "PLAT.macos.g.cs", "PLAT.windows.g.cs", "PLAT.xyz.g.cs", "WHICH.g.cs",
        ],
        ignoreOrder: true);

        var dispatch = Text("PLAT.g.cs");
        dispatch.ShouldContain("public void PLAT(int* n)");
        dispatch.ShouldContain("if (_variant_PLAT == 1)");
        dispatch.ShouldContain("_PLAT_windows(n);");
        dispatch.ShouldContain("_PLAT_base(n);");
        Text("PLAT.windows.g.cs").ShouldContain("// Translated from FORTRAN 77 by Fortrania: PLAT, its variant for windows, from plat.f.");
        Text("PLAT.windows.g.cs").ShouldContain("private void _PLAT_windows(int* n)");
        Text("PLAT.base.g.cs").ShouldContain("private void _PLAT_base(int* n)");
        Text("Core.g.cs").ShouldContain(
            "_variant_PLAT = context.IsOSPlatform(\"windows\") ? 1 : context.IsOSPlatform(\"macos\") ? 2 : context.IsOSPlatform(\"xyz\") ? 3 : 0;");

        var compiled = Compile();
        compiled.Run<int>("OnWindows").ShouldBe(1);
        compiled.Run<int>("OnMacOS").ShouldBe(2);
        compiled.Run<int>("OnOSX").ShouldBe(2);
        compiled.Run<int>("OnXyz").ShouldBe(3);
        compiled.Run<int>("OnLinux").ShouldBe(0);
        compiled.Run<int>("OnSolaris").ShouldBe(0);
        compiled.Run<int>("OnTheMachine").ShouldBe(OperatingSystem.IsWindows() ? 1 : OperatingSystem.IsMacOS() ? 2 : 0);
    }

    [Fact]
    public void Run_WhenSeveralVariantsSuitAContext_ShouldRunTheMostSpecific()
    {
        // Arrange: for the system alone, given first, for the system on this
        // architecture, and for this architecture alone.
        var architecture = RuntimeInformation.ProcessArchitecture;
        Put("core/core.f", Base);
        Put("system/plat.f", Plat(1));
        Put("both/plat.f", Plat(2));
        Put("architecture/plat.f", Plat(3));
        var source = Source(
            Variant("xyz", null, "system/*.f"),
            Variant("xyz", architecture, "both/*.f"),
            Variant(null, architecture, "architecture/*.f"));

        // Act
        var result = Translate(source);

        // Assert: tried in the order of how much each says.
        result.Succeeded.ShouldBeTrue(string.Join('\n', result.Messages));
        var key = architecture.ToString().ToLowerInvariant();
        Files().ShouldContain($"PLAT.xyz_{key}.g.cs");
        Files().ShouldContain($"PLAT.{key}.g.cs");
        Text("Core.g.cs").ShouldContain(
            $"_variant_PLAT = context.IsOSPlatform(\"xyz\") && context.ProcessArchitecture == global::System.Runtime.InteropServices.Architecture.{architecture} ? 1 : context.IsOSPlatform(\"xyz\") ? 2 : context.ProcessArchitecture == global::System.Runtime.InteropServices.Architecture.{architecture} ? 3 : 0;");

        var compiled = Compile();
        compiled.Run<int>("OnXyz").ShouldBe(2);
        compiled.Run<int>("OnSolaris").ShouldBe(3);
    }

    [Fact]
    public void Run_WhenVariantsKeepStateAndBuffers_ShouldGiveEachItsOwnAndCallersRoomForTheDeepest()
    {
        // Arrange: a unit with two entries, saved state and a buffer, and a
        // variant whose buffer is the larger, and which writes an argument
        // its base only reads.
        Put("core/core.f", """
                  SUBROUTINE KEEP ( N )
                  INTEGER N, TOTAL, W ( 10 )
                  SAVE TOTAL
                  DATA TOTAL / 0 /
                  W ( 1 ) = N
                  TOTAL = TOTAL + W ( 1 )
                  RETURN

                  ENTRY TAKE ( N )
                  IF ( N .LT. 0 ) TOTAL = 0
                  END

                  INTEGER FUNCTION RUN ( )
                  INTEGER N
                  CALL KEEP ( 5 )
                  CALL KEEP ( 6 )
                  N = 0
                  CALL TAKE ( N )
                  RUN = N
                  END
            """);
        Put("xyz/keep.f", """
                  SUBROUTINE KEEP ( N )
                  INTEGER N, TOTAL, W ( 100 )
                  SAVE TOTAL
                  DATA TOTAL / 100 /
                  W ( 100 ) = N
                  TOTAL = TOTAL + W ( 100 )
                  RETURN

                  ENTRY TAKE ( N )
                  N = TOTAL
                  END
            """);
        const string Runner =
            """
            using System.Runtime.InteropServices;
            using Dracocephalum.Fortrania.Runtime;

            namespace Translated;

            public static unsafe class Driver
            {
                public static int[] Run() =>
                [
                    Run("linux", BufferPlacement.Stack),
                    Run("linux", BufferPlacement.Arena),
                    Run("xyz", BufferPlacement.Stack),
                    Run("xyz", BufferPlacement.Arena),
                ];

                private static int Run(string system, BufferPlacement placement)
                {
                    using var context = new FortranContext(new FortranContextOptions { OSPlatform = OSPlatform.Create(system), BufferPlacement = placement });
                    return context.Get<Example.Toolkit.Core>().RUN();
                }
            }
            """;

        // Act
        var result = Translate(Source(Variant("xyz", null, "xyz/*.f")));

        // Assert: the base's TAKE reads its argument and the variant's writes
        // it, so the procedure does both; the variant's buffer is the deepest.
        result.Succeeded.ShouldBeTrue(string.Join('\n', result.Messages));
        Text("KEEP.g.cs").ShouldContain("public void TAKE(int* n)");
        Text("KEEP.xyz.g.cs").ShouldContain("_state_KEEP_xyz");
        Text("KEEP.base.g.cs").ShouldContain("_state_KEEP_base");
        Text("KEEP.xyz.g.cs").ShouldContain("_KEEP_xyz_core(");
        var run = result.StackUse.Single(use => use.Procedure == "RUN");
        run.DeepestChain.ShouldBe(["RUN", "KEEP", "_KEEP_xyz"]);
        run.DeepestBytes.ShouldBe(400);
        File.ReadAllText(Path.Combine(_root, "out", ProjectSummary.GetFileName("Example.Toolkit")))
            .ShouldContain("{\"name\":\"TAKE\",\"line\":9,\"arguments\":[{\"name\":\"N\",\"kind\":\"variable\",\"type\":\"integer\",\"rank\":0,\"uses\":\"read, written\"}]");

        TranslatedProgram.FromSources(Directory.GetFiles(Path.Combine(_root, "out"), "*.cs", SearchOption.AllDirectories).Select(File.ReadAllText).Append(Runner))
            .Run<int[]>("Run")
            .ShouldBe([0, 0, 111, 111]);
    }

    [Fact]
    public void Run_WhenAVariantHasNoBase_ShouldRefuse()
    {
        // Arrange
        Put("core/core.f", Base);
        Put("xyz/other.f", """
                  SUBROUTINE OTHER ( N )
                  INTEGER N
                  N = 1
                  END
            """);

        // Act
        var result = Translate(Source(Variant("xyz", null, "xyz/*.f")));

        // Assert
        result.Succeeded.ShouldBeFalse();
        result.Messages.ShouldContain(message => message.Text == "The source Core has a variant of OTHER for xyz and no OTHER of its own for every other platform.");
    }

    [Fact]
    public void Run_WhenAVariantHasOtherArguments_ShouldRefuse()
    {
        // Arrange
        Put("core/core.f", Base);
        Put("xyz/plat.f", """
                  SUBROUTINE PLAT ( N, M )
                  INTEGER N, M
                  N = 1
                  END
            """);

        // Act
        var result = Translate(Source(Variant("xyz", null, "xyz/*.f")));

        // Assert
        result.Succeeded.ShouldBeFalse();
        result.Messages.ShouldContain(message => message.Text == "The variant of PLAT for xyz is not called as PLAT is: it has to have the same procedures, each with the same arguments.");
    }

    [Fact]
    public void Run_WhenAVariantTakesAnArgumentOfAnotherType_ShouldRefuse()
    {
        // Arrange
        Put("core/core.f", Base);
        Put("xyz/plat.f", """
                  SUBROUTINE PLAT ( X )
                  DOUBLE PRECISION X
                  X = 1
                  END
            """);

        // Act
        var result = Translate(Source(Variant("xyz", null, "xyz/*.f")));

        // Assert
        result.Succeeded.ShouldBeFalse();
        result.Messages.ShouldContain(message => message.Text == "The variant of PLAT for xyz is called as void PLAT(double* x), where PLAT is called as void PLAT(int* n); a variant has to be called as the procedure it stands in for is.");
    }

    [Theory]
    [InlineData("a-b", "a_b", "a_b")]
    [InlineData("base", null, "base")]
    public void Run_WhenTwoVariantsWouldShareAName_ShouldRefuse(string first, string? second, string name)
    {
        // Arrange: two systems whose names are written alike, or one written
        // as the base is.
        Put("core/core.f", Base);
        Put("one/plat.f", Plat(1));
        Put("two/plat.f", Plat(2));
        FileSettings[] variants = second is null
            ? [Variant(first, null, "one/*.f")]
            : [Variant(first, null, "one/*.f"), Variant(second, null, "two/*.f")];

        // Act
        var result = Translate(Source(variants));

        // Assert
        result.Succeeded.ShouldBeFalse();
        result.Messages.ShouldContain(message => message.Text == $"The source Core has two variants of PLAT that are both named for {name}; give each a platform of its own.");
    }

    [Fact]
    public void Run_WhenEveryFileIsForAPlatform_ShouldRefuseForWantOfABase()
    {
        // Arrange
        Put("xyz/plat.f", Plat(1));

        // Act
        var result = Translate(new SourceSettings { Name = "Core", Files = [Variant("xyz", null, "xyz/*.f")] });

        // Assert
        result.Succeeded.ShouldBeFalse();
        result.Messages.ShouldContain(message => message.Text == "The source Core of the project Example.Toolkit names no files of its own, for no platform in particular.");
    }

    [Fact]
    public void Run_WhenFilesOfTheSourcesOwnAreGivenTwice_ShouldReadThemAsOne()
    {
        // Arrange: the base in two entries, and a variant between them.
        Put("core/core.f", Base);
        Put("more/more.f", """
                  INTEGER FUNCTION MORE ( )
                  MORE = 7
                  END
            """);
        Put("xyz/plat.f", Plat(3));
        var source = new SourceSettings
        {
            Name = "Core",
            Files = [new FileSettings { Patterns = ["core/*.f"] }, Variant("xyz", null, "xyz/*.f"), new FileSettings { Patterns = ["more/*.f"] }],
        };

        // Act
        var result = Translate(source);

        // Assert
        result.Succeeded.ShouldBeTrue(string.Join('\n', result.Messages));
        Files().ShouldContain("MORE.g.cs");
        Compile().Run<int>("OnXyz").ShouldBe(3);
    }

    private static string Plat(int number) =>
        $"""
              SUBROUTINE PLAT ( N )
              INTEGER N
              N = {number}
              END
        """;

    // A source of core/*.f, with files for other platforms after them.
    private static SourceSettings Source(params FileSettings[] variants) =>
        new() { Name = "Core", Files = [new FileSettings { Patterns = ["core/*.f"] }, .. variants] };

    private static FileSettings Variant(string? system, Architecture? architecture, params string[] patterns) =>
        new() { OSPlatform = system, ProcessArchitecture = architecture, Patterns = [.. patterns] };

    private TranslationResult Translate(SourceSettings source) => Translator.Run(
        new TranslationProject
        {
            Name = "Example.Toolkit",
            Settings = [new TranslationSettings { Folder = _root, Output = "out", Sources = [source] }],
            Summary = Path.Combine(_root, "out", ProjectSummary.GetFileName("Example.Toolkit")),
        },
        new TranslationOptions());

    private TranslatedProgram Compile() =>
        TranslatedProgram.FromSources(Directory.GetFiles(Path.Combine(_root, "out"), "*.cs", SearchOption.AllDirectories).Select(File.ReadAllText).Append(Driver));

    private void Put(string path, string text)
    {
        var full = Path.Combine(_root, path);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, text);
    }

    // A file of the source's translation.
    private string Text(string name) => File.ReadAllText(Path.Combine(_root, "out", Translator.GeneratedFolder, "Core", name));

    private string[] Files() => [.. Directory.GetFiles(Path.Combine(_root, "out", Translator.GeneratedFolder, "Core")).Select(Path.GetFileName).OfType<string>()];
}
