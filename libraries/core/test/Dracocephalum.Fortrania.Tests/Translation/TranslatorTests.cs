using System.Text;
using Dracocephalum.Fortrania.Tests.Emit;
using Dracocephalum.Fortrania.Translation;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Shouldly;

namespace Dracocephalum.Fortrania.Tests.Translation;

public sealed class TranslatorTests : IDisposable
{
    private const string Core =
        """
              SUBROUTINE CPUT ( N )
              INTEGER N, TOTAL
              SAVE TOTAL
              DATA TOTAL / 0 /
              TOTAL = TOTAL + N
              RETURN

              ENTRY CGET ( N )
              N = TOTAL
              END
        """;

    // A library that calls a function it is passed beneath a buffer of its
    // own, and a function of its own that is called by name.
    private const string Library =
        """
              SUBROUTINE APPLY ( F, X, Y )
              DOUBLE PRECISION F, X, Y, W ( 8 )
              EXTERNAL F
              INTEGER I
              DO I = 1, 8
                 W ( I ) = X
              END DO
              Y = F ( W ( 3 ) )
              END

              DOUBLE PRECISION FUNCTION TWICE ( X )
              DOUBLE PRECISION X
              TWICE = 2 * X
              END
        """;

    // A program that passes a function of its own to the library, and
    // calls another of the library's by name.
    private const string Caller =
        """
              PROGRAM TOOL
              DOUBLE PRECISION Y, SQUARE, A ( 4 ), TWICE
              EXTERNAL SQUARE
              A ( 1 ) = 3
              CALL APPLY ( SQUARE, A ( 1 ), Y )
              CALL EXIT ( INT ( Y + TWICE ( 1.0D0 ) ) )
              END

              DOUBLE PRECISION FUNCTION SQUARE ( X )
              DOUBLE PRECISION X, B ( 16 )
              B ( 1 ) = X
              SQUARE = B ( 1 ) * B ( 1 )
              END
        """;

    private const string Tool =
        """
              PROGRAM TOOL
              INTEGER N
              CALL CPUT ( 5 )
              CALL CGET ( N )
              CALL EXIT ( N + 2 )
              END
        """;

    private readonly string _root = Path.Combine(AppContext.BaseDirectory, "translator-" + Guid.NewGuid().ToString("N"));

    public TranslatorTests() => Directory.CreateDirectory(_root);

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public void Run_WhenProjectsHaveNoProjectFiles_ShouldWriteWholeProjectsThatRun()
    {
        // Arrange
        Put("core/core.f", Core);
        Put("tool/tool.pgm", Tool);
        var settings = Settings(Project("Example.Toolkit", Source("Core", "core/*.f")), Project("Example.Tool", Source("ToolMain", "tool/*.pgm")));

        // Act
        var result = Translate(settings);

        // Assert
        result.Messages.ShouldBeEmpty();
        result.Succeeded.ShouldBeTrue();
        result.Units.ShouldBe(2);
        Files("out").ShouldBe(
        [
            "out/Example.Tool/Example.Tool.csproj",
            "out/Example.Tool/Example.Tool.fortrania-summary.json",
            "out/Example.Tool/Generated/.fortrania-manifest.json",
            "out/Example.Tool/Generated/ToolMain/TOOL.g.cs",
            "out/Example.Tool/Generated/ToolMain/ToolMain.g.cs",
            "out/Example.Tool/Program.cs",
            "out/Example.Toolkit/Example.Toolkit.csproj",
            "out/Example.Toolkit/Example.Toolkit.fortrania-summary.json",
            "out/Example.Toolkit/Generated/.fortrania-manifest.json",
            "out/Example.Toolkit/Generated/Core/CPUT.g.cs",
            "out/Example.Toolkit/Generated/Core/Core.g.cs",
        ]);
        result.Written.Count.ShouldBe(9);

        var library = Text("out/Example.Toolkit/Example.Toolkit.csproj");
        library.ShouldContain("<TargetFramework>net10.0</TargetFramework>");
        library.ShouldContain("<AllowUnsafeBlocks>true</AllowUnsafeBlocks>");
        library.ShouldContain("<PackageReference Include=\"Dracocephalum.Fortrania.Runtime\" Version=\"1.2.3\" />");
        library.ShouldNotContain("OutputType");
        library.ShouldNotContain("ProjectReference");
        library.ShouldNotContain("TreatWarningsAsErrors");
        Text("out/Example.Toolkit/Generated/Core/Core.g.cs").ShouldNotContain("ToolMain");
        Text("out/Example.Tool/Generated/ToolMain/ToolMain.g.cs").ShouldContain("_library_Core");

        var program = Text("out/Example.Tool/Example.Tool.csproj");
        program.ShouldContain("<OutputType>Exe</OutputType>");
        program.ShouldContain("<ProjectReference Include=\"../Example.Toolkit/Example.Toolkit.csproj\" />");
        Text("out/Example.Tool/Program.cs").ShouldContain("context.Get<ToolMain>().TOOL();");
        Text("out/Example.Tool/Generated/ToolMain/TOOL.g.cs").ShouldContain("_Core.CPUT(Arg(&_i1, 5));");

        var compiled = TranslatedProgram.FromSources(
            Directory.GetFiles(Path.Combine(_root, "out"), "*.cs", SearchOption.AllDirectories).Select(File.ReadAllText));
        compiled.RunMain("Example.Tool.Program").ShouldBe(7);
    }

    [Fact]
    public void Run_WhenTheOptionsGiveAFramework_ShouldWriteItIntoTheProjectFileItMakes()
    {
        // Arrange
        Put("core/core.f", Core);

        // Act
        var result = Translate(Project("Example.Toolkit", Source("Core", "core/*.f")), new TranslationOptions { Framework = "net11.0" });

        // Assert
        result.Succeeded.ShouldBeTrue();
        Text("out/Example.Toolkit/Example.Toolkit.csproj").ShouldContain("<TargetFramework>net11.0</TargetFramework>");
    }

    [Fact]
    public void Run_WhenNothingChanged_ShouldWriteNothingTheSecondTime()
    {
        // Arrange
        Put("core/core.f", Core);
        var settings = Settings(Project("Example.Toolkit", Source("Core", "core/*.f")));
        Translate(settings).Succeeded.ShouldBeTrue();

        // Act
        var second = Translate(settings);

        // Assert
        second.Succeeded.ShouldBeTrue();
        second.Written.ShouldBeEmpty();
        second.Removed.ShouldBeEmpty();
    }

    [Fact]
    public void Run_WhenAUnitIsGoneFromTheSources_ShouldRemoveItsFileAndLeaveWhatSomebodyElseWrote()
    {
        // Arrange
        Put("core/core.f", Core);
        Put("core/extra.f", "      SUBROUTINE EXTRA ( N )\n      INTEGER N\n      N = 1\n      END\n");
        var settings = Settings(Project("Example.Toolkit", Source("Core", "core/*.f")));
        Translate(settings).Succeeded.ShouldBeTrue();
        Put("out/Example.Toolkit/Generated/Core/Mine.g.cs", "// written by hand\n");
        Put("out/Example.Toolkit/Generated/Core/Partial.cs", "// also by hand\n");
        File.Delete(Path.Combine(_root, "core/extra.f"));

        // Act
        var result = Translate(settings);

        // Assert
        result.Succeeded.ShouldBeTrue();
        result.Removed.Select(Path.GetFileName).ShouldBe(["EXTRA.g.cs"]);
        Files("out/Example.Toolkit/Generated").ShouldBe(
        [
            "out/Example.Toolkit/Generated/.fortrania-manifest.json",
            "out/Example.Toolkit/Generated/Core/CPUT.g.cs",
            "out/Example.Toolkit/Generated/Core/Core.g.cs",
            "out/Example.Toolkit/Generated/Core/Mine.g.cs",
            "out/Example.Toolkit/Generated/Core/Partial.cs",
        ]);
    }

    [Fact]
    public void Run_WhenATranslationHasAProjectFile_ShouldWriteSourcesOnlyAndLeaveTheProjectFileAlone()
    {
        // Arrange
        Put("core/core.f", Core);
        Put("tool/tool.pgm", Tool);
        Put("tool/other.pgm", "      PROGRAM OTHER\n      CALL CPUT ( 1 )\n      END\n");
        Put("out/Example.Tool/Example.Tool.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\" />\n");
        var settings = Settings(Project("Example.Toolkit", Source("Core", "core/*.f")), Project("Example.Tool", Source("ToolMain", "tool/*.pgm")));

        // Act
        var result = Translate(settings);

        // Assert
        result.Succeeded.ShouldBeTrue();
        result.Messages.ShouldBeEmpty();
        Text("out/Example.Tool/Example.Tool.csproj").ShouldBe("<Project Sdk=\"Microsoft.NET.Sdk\" />\n");
        File.Exists(Path.Combine(_root, "out/Example.Tool/Program.cs")).ShouldBeFalse();
        File.Exists(Path.Combine(_root, "out/Example.Tool/Generated/ToolMain/OTHER.g.cs")).ShouldBeTrue();
        File.Exists(Path.Combine(_root, "out/Example.Toolkit/Example.Toolkit.csproj")).ShouldBeTrue();
    }

    [Fact]
    public void Run_WhenATranslationScaffoldedHasSeveralMainPrograms_ShouldAskForTheEntryAndTakeTheOneNamed()
    {
        // Arrange
        Put("core/core.f", Core);
        Put("tool/tool.pgm", Tool);
        Put("tool/other.pgm", "      PROGRAM OTHER\n      CALL CPUT ( 1 )\n      END\n");
        var settings = Settings(Project("Example.Toolkit", Source("Core", "core/*.f")), Project("Example.Tool", Source("ToolMain", "tool/*.pgm")));

        // Act
        var refused = Translate(settings);
        var accepted = Translate(settings, new TranslationOptions { Entry = "other" });

        // Assert
        refused.Succeeded.ShouldBeFalse();
        refused.Messages.ShouldHaveSingleItem().Text.ShouldBe("The project Example.Tool has 2 main programs, OTHER, TOOL; name the one its program starts with as the entry.");
        refused.Written.ShouldBeEmpty();
        accepted.Succeeded.ShouldBeTrue();
        Text("out/Example.Tool/Program.cs").ShouldContain("context.Get<ToolMain>().OTHER();");
        File.Exists(Path.Combine(_root, "out/Example.Toolkit/Program.cs")).ShouldBeFalse();
    }

    [Fact]
    public void Run_WhenTheEntryIsNoMainProgramOfTheTranslation_ShouldSaySoAndWriteNothing()
    {
        // Arrange
        Put("core/core.f", Core);
        Put("tool/tool.pgm", Tool);
        var settings = Settings(Project("Example.Toolkit", Source("Core", "core/*.f")), Project("Example.Tool", Source("ToolMain", "tool/*.pgm")));

        // Act
        var result = Translate(settings, new TranslationOptions { Entry = "MISSING" });

        // Assert
        result.Succeeded.ShouldBeFalse();
        result.Messages.ShouldHaveSingleItem().Text.ShouldBe("The entry MISSING is not one of the main programs of the project Example.Tool: TOOL.");
        Directory.Exists(Path.Combine(_root, "out")).ShouldBeFalse();
    }

    [Fact]
    public void Run_WhenNotScaffolding_ShouldWriteTheTranslationAndNoProjectFileOrMain()
    {
        // Arrange
        Put("core/core.f", Core);
        Put("tool/tool.pgm", Tool);
        var settings = Settings(Project("Example.Toolkit", Source("Core", "core/*.f")), Project("Example.Tool", Source("ToolMain", "tool/*.pgm")));

        // Act
        var result = Translator.Run(settings, new TranslationOptions());

        // Assert
        result.Succeeded.ShouldBeTrue();
        result.Messages.ShouldBeEmpty();
        Files("out").ShouldBe(
        [
            "out/Example.Tool/Example.Tool.fortrania-summary.json",
            "out/Example.Tool/Generated/.fortrania-manifest.json",
            "out/Example.Tool/Generated/ToolMain/TOOL.g.cs",
            "out/Example.Tool/Generated/ToolMain/ToolMain.g.cs",
            "out/Example.Toolkit/Example.Toolkit.fortrania-summary.json",
            "out/Example.Toolkit/Generated/.fortrania-manifest.json",
            "out/Example.Toolkit/Generated/Core/CPUT.g.cs",
            "out/Example.Toolkit/Generated/Core/Core.g.cs",
        ]);
    }

    [Theory]
    [InlineData("Example.Core", "Core", null, "The class Core of the project Example.Core has the name of a part of its namespace Example.Core; give the source a class of another name.")]
    [InlineData("Example.Kit", "Core", "not a class", "The source Core of the project Example.Kit needs a class name; 'not a class' is not one.")]
    [InlineData("Example Kit", "Core", null, "The namespace Example Kit of the project Example Kit is not a namespace; give it one.")]
    public void Run_WhenANameCannotBeUsed_ShouldRefuseAndNotRenameByItself(string project, string source, string? className, string message)
    {
        // Arrange
        Put("core/core.f", Core);
        var settings = Settings(Project(project, Source(source, "core/*.f")));
        settings[0].Part.Sources[0].Class = className;

        // Act
        var result = Translate(settings);

        // Assert
        result.Succeeded.ShouldBeFalse();
        result.Messages.ShouldHaveSingleItem().Text.ShouldBe(message);
    }

    [Fact]
    public void Run_WhenTheClashingClassIsGivenAnotherName_ShouldTranslate()
    {
        // Arrange
        Put("core/core.f", Core);
        var settings = Settings(Project("Example.Core", Source("Core", "core/*.f")));
        settings[0].Part.Sources[0].Class = "CoreLibrary";

        // Act
        var result = Translate(settings);

        // Assert
        result.Succeeded.ShouldBeTrue();
        Text("out/Example.Core/Generated/Core/CoreLibrary.g.cs").ShouldContain("public unsafe partial class CoreLibrary : IFortranLibrary<CoreLibrary>");
    }

    [Fact]
    public void Run_WhenTheSettingsAreWrongInSeveralWays_ShouldSayEachAndWriteNothing()
    {
        // Arrange
        var settings = Settings(
            Project("One", Source("A", "missing/*.f"), Source("A", "missing/*.f")),
            Project("One"),
            Project("Two", new SourceSettings { Name = "B" }));
        settings[1].Part.Output = null;

        // Act
        var result = Translate(settings);

        // Assert
        result.Succeeded.ShouldBeFalse();
        result.Messages.Select(message => message.Text).ShouldBe(
        [
            "Two sources become the class One.A.",
            "Two projects are named One.",
            "The project One has no sources.",
            "Settings of the project One name no output: say where they are written, \"output\": \".\" for the folder the settings file is in.",
            "The source B of the project Two names no files.",
        ]);
        Directory.Exists(Path.Combine(_root, "out")).ShouldBeFalse();
    }

    [Fact]
    public void Run_WhenAFileIsNotThereOrIsNotFortran_ShouldNameTheFileAndWriteNothing()
    {
        // Arrange
        Put("core/core.f", Core);
        Put("core/broken.f", "      SUBROUTINE BROKEN ( N\n      END\n");
        Put("core/later.f", "      SUBROUTINE LATER ( N )\n      COMPLEX N\n      N = N\n      END\n");
        var settings = Settings(Project("Example.Toolkit", Source("Core", "core/*.f", "none/*.f")));

        // Act
        var result = Translate(settings);

        // Assert
        result.Succeeded.ShouldBeFalse();
        result.Messages.ShouldContain(message => message.Text == "The source Core of the project Example.Toolkit names 'none/*.f', and there is no such file.");
        var broken = result.Messages.Single(message => message.File is not null);
        broken.File!.ShouldEndWith("broken.f");
        broken.ToString().ShouldContain("broken.f: error: Line 1:");
        Directory.Exists(Path.Combine(_root, "out")).ShouldBeFalse();
    }

    [Fact]
    public void Run_WhenAUnitUsesWhatIsNotTranslatedYet_ShouldSayWhichFileAndWriteNothing()
    {
        // Arrange
        Put("core/core.f", Core);
        Put("core/later.f", "      SUBROUTINE LATER ( N )\n      INTEGER N\n      ENDFILE ( N )\n      END\n");
        var settings = Settings(Project("Example.Toolkit", Source("Core", "core/*.f")));

        // Act
        var result = Translate(settings);

        // Assert
        result.Succeeded.ShouldBeFalse();
        var message = result.Messages.ShouldHaveSingleItem();
        message.File.ShouldNotBeNull().ShouldEndWith("later.f");
        message.Text.ShouldBe("Line 3: ENDFILE cannot be translated yet.");
        Directory.Exists(Path.Combine(_root, "out")).ShouldBeFalse();
    }

    [Fact]
    public void Run_WhenTranslationsCallEachOtherBothWays_ShouldRefuseAndSuggestOne()
    {
        // Arrange
        Put("a/a.f", "      SUBROUTINE AONE ( N )\n      INTEGER N\n      CALL BONE ( N )\n      END\n");
        Put("b/b.f", "      SUBROUTINE BONE ( N )\n      INTEGER N\n      IF ( N .GT. 0 ) CALL AONE ( N - 1 )\n      END\n");

        // Act
        var apart = Translate(Settings(Project("PA", Source("A", "a/*.f")), Project("PB", Source("B", "b/*.f"))));
        var together = Translate(Settings(Project("PAB", Source("A", "a/*.f"), Source("B", "b/*.f"))));

        // Assert
        apart.Succeeded.ShouldBeFalse();
        apart.Messages.ShouldHaveSingleItem().Text.ShouldStartWith("The project PA calls into itself by way of the projects it calls into");
        together.Succeeded.ShouldBeTrue();
    }

    [Fact]
    public void Run_WhenACallReachesNothing_ShouldWarnAndStillTranslateUnlessTheContextSuppliesIt()
    {
        // Arrange
        Put("core/calls.f", "      SUBROUTINE CALLS ( N )\n      INTEGER N\n      CALL NOWHERE ( N )\n      CALL EXIT ( N )\n      END\n");

        // Act
        var result = Translate(Settings(Project("Example.Toolkit", Source("Core", "core/*.f"))));

        // Assert
        result.Succeeded.ShouldBeTrue();
        result.Messages.ShouldHaveSingleItem().Text.ShouldBe("CALLS calls NOWHERE, which no source of the translation defines.");
    }

    [Fact]
    public void Run_WhenFilesIncludeOthers_ShouldFindThemBesideTheFileAndInTheFoldersNamed()
    {
        // Arrange
        Put("core/sizes.inc", "      INTEGER SIZE\n      PARAMETER ( SIZE = 7 )\n");
        Put("shared/more.inc", "      INTEGER MORE\n      PARAMETER ( MORE = 35 )\n");
        Put("core/uses.f", "      SUBROUTINE USES ( N )\n      INTEGER N\n      INCLUDE 'sizes.inc'\n      INCLUDE 'more.inc'\n      N = SIZE + MORE\n      END\n");
        var source = Source("Core", "core/*.f");
        source.Includes = ["shared"];

        // Act
        var result = Translate(Settings(Project("Example.Toolkit", source)));

        // Assert
        result.Messages.ShouldBeEmpty();
        Text("out/Example.Toolkit/Generated/Core/USES.g.cs").ShouldContain("const int size = 7;");
    }

    [Fact]
    public void Run_WhenARoutineHasTheNameOfADevice_ShouldGiveItsFileANameEverySystemTakes()
    {
        // Arrange
        Put("core/con.f", "      SUBROUTINE CON ( N )\n      INTEGER N\n      N = 1\n      END\n");

        // Act
        var result = Translate(Settings(Project("Example.Toolkit", Source("Core", "core/*.f"))));

        // Assert
        result.Succeeded.ShouldBeTrue();
        File.Exists(Path.Combine(_root, "out/Example.Toolkit/Generated/Core/_CON.g.cs")).ShouldBeTrue();
    }

    [Fact]
    public void Run_WhenATranslationNamesItsOwnFolderAndTheRuntimeIsAProject_ShouldWriteThereAndReferToTheProject()
    {
        // Arrange
        Put("core/core.f", Core);
        var project = Project("Example.Toolkit", Source("Core", "core/*.f"));
        project.Part.Output = "elsewhere/kit";
        project.Summary = Full("elsewhere/kit/Example.Toolkit.fortrania-summary.json");
        var runtime = new RuntimeSettings { Project = Path.Combine(_root, "runtime/Runtime.csproj") };

        // Act
        var result = Translate(project, new TranslationOptions { Runtime = runtime });

        // Assert
        result.Succeeded.ShouldBeTrue();
        Text("elsewhere/kit/Example.Toolkit.csproj").ShouldContain("<ProjectReference Include=\"../../runtime/Runtime.csproj\" />");
    }

    [Fact]
    public void Run_WhenThePackageVersionIsOmitted_ShouldReferToItsPackageWithNoVersion()
    {
        // Arrange
        Put("core/core.f", Core);
        var settings = Settings(Project("Example.Toolkit", Source("Core", "core/*.f")));

        // Act
        var result = Translate(settings, new TranslationOptions { Runtime = new RuntimeSettings { OmitPackageVersion = true } });

        // Assert
        result.Succeeded.ShouldBeTrue();
        var library = Text("out/Example.Toolkit/Example.Toolkit.csproj");
        library.ShouldContain("<PackageReference Include=\"Dracocephalum.Fortrania.Runtime\" />");
        library.ShouldNotContain("Version=");
    }

    [Fact]
    public void Run_WhenThePackageVersionIsOmittedAndGiven_ShouldRefuseTheOptions()
    {
        // Arrange
        Put("core/core.f", Core);
        var settings = Settings(Project("Example.Toolkit", Source("Core", "core/*.f")));

        // Act
        var result = Translate(settings, new TranslationOptions { Runtime = new RuntimeSettings { Package = "1.2.3", OmitPackageVersion = true } });

        // Assert
        result.Succeeded.ShouldBeFalse();
        result.Messages.ShouldHaveSingleItem().Text.ShouldBe("The options give the runtime's package the version 1.2.3 and say to write none; it is one or the other.");
        result.Written.ShouldBeEmpty();
    }

    [Fact]
    public void Run_WhenATranslatedFileWasChangedByHand_ShouldPutItRightAndWriteNothingElse()
    {
        // Arrange
        Put("core/core.f", Core);
        var settings = Settings(Project("Example.Toolkit", Source("Core", "core/*.f")));
        Translate(settings).Succeeded.ShouldBeTrue();
        var path = "out/Example.Toolkit/Generated/Core/CPUT.g.cs";
        var translated = Text(path);
        Put(path, translated + "// changed by hand\n");

        // Act
        var result = Translate(settings);

        // Assert
        result.Written.Select(Path.GetFileName).ShouldBe(["CPUT.g.cs"]);
        Text(path).ShouldBe(translated);
    }

    [Fact]
    public void Run_WhenATranslatedFileHasOtherLineEndings_ShouldTakeItAsUnchanged()
    {
        // Arrange: the file as a checkout that changes line endings has it.
        Put("core/core.f", Core);
        var settings = Settings(Project("Example.Toolkit", Source("Core", "core/*.f")));
        Translate(settings).Succeeded.ShouldBeTrue();
        var path = "out/Example.Toolkit/Generated/Core/CPUT.g.cs";
        Put(path, Text(path).ReplaceLineEndings("\r\n"));

        // Act
        var result = Translate(settings);

        // Assert
        result.Written.ShouldBeEmpty();
    }

    [Fact]
    public void Run_WhenATranslatedFileIsDeletedAndTheManifestSpoiled_ShouldWriteThatFileAgainAndTheManifest()
    {
        // Arrange
        Put("core/core.f", Core);
        var settings = Settings(Project("Example.Toolkit", Source("Core", "core/*.f")));
        Translate(settings).Succeeded.ShouldBeTrue();
        var manifest = Text("out/Example.Toolkit/Generated/.fortrania-manifest.json");
        File.Delete(Path.Combine(_root, "out/Example.Toolkit/Generated/Core/CPUT.g.cs"));
        Put("out/Example.Toolkit/Generated/.fortrania-manifest.json", "not a manifest");

        // Act
        var result = Translate(settings);

        // Assert
        result.Succeeded.ShouldBeTrue();
        result.Written.Select(Path.GetFileName).ShouldBe(["CPUT.g.cs"]);
        Text("out/Example.Toolkit/Generated/.fortrania-manifest.json").ShouldBe(manifest);
    }

    [Fact]
    public void Run_WhenTheProjectHasNoName_ShouldSaySoAndWriteNothing()
    {
        // Arrange: settings beside a project file, whose name is not taken.
        Put("lib/core/core.f", Core);
        Put("lib/Example.Lib.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\" />\n");
        var settings = Project("ignored", Source("Core", "core/*.f"));
        settings.Name = null;
        settings.Part.Folder = Path.Combine(_root, "lib");

        // Act
        var result = Translate(settings);

        // Assert
        result.Succeeded.ShouldBeFalse();
        result.Messages.ShouldHaveSingleItem().Text.ShouldBe(
            "The project to translate has no name; give it the name of its assembly.");
        result.Written.ShouldBeEmpty();
    }

    [Fact]
    public void Run_WhenSettingsOfTwoProjectsGoToOneFolder_ShouldSaySoAndWriteNothing()
    {
        // Arrange
        Put("core/core.f", Core);
        Put("tool/tool.pgm", Tool);
        var tool = Project("Example.Tool", Source("ToolMain", "tool/*.pgm"));
        tool.Part.Output = "out/Example.Toolkit";

        // Act
        var result = Translate(Settings(Project("Example.Toolkit", Source("Core", "core/*.f")), tool));

        // Assert
        result.Succeeded.ShouldBeFalse();
        result.Messages.ShouldHaveSingleItem().Text.ShouldBe(
            $"Two settings files, of Example.Toolkit and of Example.Tool, are both written to {Full("out/Example.Toolkit")}; give each an output of its own.");
        Directory.Exists(Path.Combine(_root, "out")).ShouldBeFalse();
    }

    [Fact]
    public void Run_WhenTheOptionsGiveANamespace_ShouldTranslateToItOverWhatTheSettingsSay()
    {
        // Arrange
        Put("core/core.f", Core);
        var settings = Project("Example.Toolkit", Source("Core", "core/*.f"));
        settings.Part.Namespace = "Example.Given";

        // Act
        var result = Translator.Run(settings, new TranslationOptions { Namespace = "Example.Chosen" });

        // Assert
        result.Succeeded.ShouldBeTrue();
        Text("out/Example.Toolkit/Generated/Core/Core.g.cs").ShouldContain("namespace Example.Chosen;");
    }

    [Fact]
    public void Run_WhenSettingsSayNoFolder_ShouldThrow()
    {
        // Arrange
        var settings = Project("Example.Toolkit", Source("Core", "core/*.f"));
        settings.Part.Folder = null;

        // Act & Assert
        Should.Throw<ArgumentException>(() => Translator.Run(settings));
    }

    [Fact]
    public void Run_WhenPathsNameVariables_ShouldTakeTheLocalFileFirstThenTheEnvironmentThenTheSettings()
    {
        // Arrange
        Put("first/core.f", Core);
        Put("second/core.f", Core);
        Put("third/core.f", Core);
        var name = "FORTRANIA_TEST_" + Guid.NewGuid().ToString("N");
        var settings = Settings(Project("Example.Toolkit", Source("Core", $"$({name})/*.f")));
        settings[0].Part.Variables[name] = new VariableSettings { Path = "third" };

        // Act
        var fromSettings = Translate(settings);
        Environment.SetEnvironmentVariable(name, "second");
        var fromEnvironment = Translate(settings);
        settings[0].Part.LocalVariables[name] = "first";
        var fromLocal = Translate(settings);
        Environment.SetEnvironmentVariable(name, null);

        // Assert
        fromSettings.Succeeded.ShouldBeTrue();
        fromEnvironment.Succeeded.ShouldBeTrue();
        fromLocal.Succeeded.ShouldBeTrue();
        Text("out/Example.Toolkit/Generated/Core/CPUT.g.cs").ShouldContain("// Translated from FORTRAN 77 by Fortrania: CPUT, from core.f.\n");
    }

    [Fact]
    public void Run_WhenAVariableHasNoValue_ShouldSayWhereToGiveItOneAndWriteNothing()
    {
        // Arrange
        var settings = Settings(Project("Example.Toolkit", Source("Core", "$(SourcesNowhere)/*.f", "$(SourcesNowhere)/*.for")));

        // Act
        var result = Translate(settings);

        // Assert
        result.Succeeded.ShouldBeFalse();
        result.Messages.ShouldHaveSingleItem().Text.ShouldBe(
            "The variable SourcesNowhere has no value. Give it one in the local settings file, in the environment, or under variables in the settings.");
        Directory.Exists(Path.Combine(_root, "out")).ShouldBeFalse();
    }

    [Fact]
    public void Run_WhenWritten_ShouldNameTheUnitAndItsFileButNoFolderAndListTheFilesInTheManifest()
    {
        // Arrange
        Put("core/core.f", Core);

        // Act
        Translate(Settings(Project("Example.Toolkit", Source("Core", "core/*.f")))).Succeeded.ShouldBeTrue();

        // Assert: neither says where on the machine it was made, since both
        // may be committed.
        var file = Text("out/Example.Toolkit/Generated/Core/CPUT.g.cs");
        file.ShouldStartWith("// <auto-generated>\n// Translated from FORTRAN 77 by Fortrania: CPUT, from core.f.\n");
        file.ShouldNotContain(_root);
        var manifest = Text("out/Example.Toolkit/Generated/.fortrania-manifest.json");
        manifest.ShouldBe("{\n  \"sources\": {\n    \"Core\": [\n      \"CPUT.g.cs\",\n      \"Core.g.cs\"\n    ]\n  }\n}\n");
    }

    [Fact]
    public void Run_WhenProceduresCallEachOther_ShouldGiveEachTheDeepestChainBeneathIt()
    {
        // Arrange: TOP calls MID, and passes F to RUNF, which passes it on to
        // INNER, which calls it. MID calls LEAF and SMALL. F is counted
        // where INNER calls it, beneath RUNF, which is deeper than MID.
        Put("core/chain.f", """
                  SUBROUTINE TOP
                  INTEGER K ( 4 )
                  DOUBLE PRECISION F
                  EXTERNAL F
                  K ( 1 ) = 1
                  CALL MID
                  CALL RUNF ( F )
                  END

                  SUBROUTINE MID
                  CALL LEAF
                  CALL SMALL
                  END

                  SUBROUTINE LEAF
                  DOUBLE PRECISION A ( 100 )
                  A ( 1 ) = 1
                  END

                  SUBROUTINE SMALL
                  INTEGER B ( 2 )
                  B ( 1 ) = 1
                  END

                  SUBROUTINE RUNF ( G )
                  DOUBLE PRECISION G, R ( 200 )
                  EXTERNAL G
                  R ( 1 ) = 1
                  CALL INNER ( G )
                  END

                  SUBROUTINE INNER ( H )
                  DOUBLE PRECISION H, V ( 10 )
                  EXTERNAL H
                  V ( 1 ) = H ( 1D0 )
                  END

                  DOUBLE PRECISION FUNCTION F ( Y )
                  DOUBLE PRECISION Y, W ( 50 )
                  W ( 1 ) = Y
                  F = W ( 1 )
                  END
            """);

        // Act
        var result = Translate(Settings(Project("Example.Toolkit", Source("Core", "core/*.f"))));

        // Assert
        result.Succeeded.ShouldBeTrue();
        result.StackUse.Select(use => use.Procedure).ShouldBe(["F", "INNER", "LEAF", "MID", "RUNF", "SMALL", "TOP"]);

        var top = result.StackUse.Single(use => use.Procedure == "TOP");
        top.OwnBytes.ShouldBe(16);
        top.DeepestBytes.ShouldBe(16 + 1600 + 80 + 400);
        top.DeepestChain.ShouldBe(["TOP", "RUNF", "INNER", "F"]);
        top.TakesProcedures.ShouldBeFalse();
        top.IsRecursive.ShouldBeFalse();

        // Called from outside, RUNF takes what it is passed on top.
        var runf = result.StackUse.Single(use => use.Procedure == "RUNF");
        runf.DeepestBytes.ShouldBe(1600 + 80);
        runf.DeepestChain.ShouldBe(["RUNF", "INNER"]);
        runf.TakesProcedures.ShouldBeTrue();

        result.StackUse.Single(use => use.Procedure == "MID").DeepestChain.ShouldBe(["MID", "LEAF"]);
        result.StackUse.ShouldAllBe(use => use.Project == "Example.Toolkit" && use.Source == "Core");

        // In the project's region, each starts where the buffers of its
        // deepest caller by name end: MID has none of its own and still
        // pushes nothing down; LEAF and SMALL, called one after the other,
        // share their place. F is called by name by nothing: INNER calls
        // it through an argument, which the runtime gives a region of its
        // own, so F starts at the top of that.
        var places = new Dictionary<string, long?>
        {
            ["TOP"] = 0,
            ["MID"] = 16,
            ["LEAF"] = 16,
            ["SMALL"] = 16,
            ["RUNF"] = 16,
            ["INNER"] = 16 + 1600,
            ["F"] = 0,
        };
        result.StackUse.ToDictionary(use => use.Procedure, use => use.Place).ShouldBe(places, ignoreOrder: true);
        result.WorkingRegions.ShouldBe([new WorkingRegion("Example.Toolkit", 16 + 1600 + 80, 16 + 1600 + 80)]);
    }

    [Fact]
    public void Run_WhenProceduresHaveBuffers_ShouldLetAContextKeepThemOnTheStackOrInItsBlockWithTheSameResult()
    {
        // Arrange: TOP passes F to RUNF, which calls it while its own array
        // holds what it reads afterwards. F fills an array and has SUMUP
        // add it up from a copy. Were F to lie where RUNF does, RUNF would
        // read what F wrote.
        Put("core/buffers.f", """
                  SUBROUTINE TOP ( R )
                  DOUBLE PRECISION R, A ( 10 ), F
                  EXTERNAL F
                  INTEGER I
                  DO I = 1, 10
                     A ( I ) = I
                  END DO
                  CALL RUNF ( F, A, R )
                  R = R + A ( 10 )
                  END

                  SUBROUTINE RUNF ( G, A, R )
                  DOUBLE PRECISION G, A ( 10 ), R, W ( 20 )
                  EXTERNAL G
                  INTEGER I
                  DO I = 1, 20
                     W ( I ) = 2 * I
                  END DO
                  R = G ( A ( 3 ) )
                  R = R + W ( 20 ) + W ( 1 )
                  END

                  DOUBLE PRECISION FUNCTION F ( Y )
                  DOUBLE PRECISION Y, V ( 30 ), S
                  INTEGER I
                  DO I = 1, 30
                     V ( I ) = Y * I
                  END DO
                  CALL SUMUP ( V, S )
                  F = S
                  END

                  SUBROUTINE SUMUP ( V, S )
                  DOUBLE PRECISION V ( 30 ), S, B ( 30 )
                  CHARACTER*12 TAG
                  INTEGER I
                  TAG = 'SUM'
                  S = 0
                  DO I = 1, 30
                     B ( I ) = V ( I )
                  END DO
                  DO I = 1, 30
                     S = S + B ( I )
                  END DO
                  IF ( TAG .NE. 'SUM' ) S = -1
                  END
            """);
        const string Driver =
            """
            using Dracocephalum.Fortrania.Runtime;

            namespace Translated;

            public static unsafe class Driver
            {
                public static double RunOnStack() => Run(BufferPlacement.Stack).Result;

                public static double RunInArena() => Run(BufferPlacement.Arena).Result;

                public static long BytesOnStack() => Run(BufferPlacement.Stack).Bytes;

                public static long BytesInArena() => Run(BufferPlacement.Arena).Bytes;

                private static (double Result, long Bytes) Run(BufferPlacement place)
                {
                    using var context = new FortranContext(new FortranContextOptions { BufferPlacement = place });
                    double result = 0;
                    context.Get<Example.Toolkit.Core>().TOP(&result);
                    return (result, context.Arena.Bytes);
                }
            }
            """;

        // Act
        var result = Translate(Settings(Project("Example.Toolkit", Source("Core", "core/*.f"))));

        // Assert
        result.Succeeded.ShouldBeTrue();

        // TOP and RUNF one after the other, F at the top of a level of its
        // own and SUMUP after it: 240 bytes for F, then 240 and 12 more.
        result.StackUse.ToDictionary(use => use.Procedure, use => use.Place).ShouldBe(
            new Dictionary<string, long?> { ["TOP"] = 0, ["RUNF"] = 80, ["F"] = 0, ["SUMUP"] = 240 },
            ignoreOrder: true);
        result.WorkingRegions.ShouldBe([new WorkingRegion("Example.Toolkit", 496, 496)]);

        var runf = Text("out/Example.Toolkit/Generated/Core/RUNF.g.cs");
        runf.ShouldContain("byte* _locals = _region.Start + 80;");
        runf.ShouldContain("if (_region.CurrentSource == BufferSource.Stack)");
        runf.ShouldNotContain("else if (_region.CurrentSource == BufferSource.None)");
        runf.ShouldContain("public void RUNF(");
        runf.ShouldContain("if (_region.CurrentSource == BufferSource.None)");
        runf.ShouldContain("_RUNF_with_arena(");
        runf.ShouldContain("internal void _RUNF_core(");

        // A core calls the core of the procedure it calls, which has one too.
        Text("out/Example.Toolkit/Generated/Core/TOP.g.cs").ShouldContain("_RUNF_core(");
        runf.ShouldContain("byte* _onStack = stackalloc byte[160];");
        runf.ShouldContain("double* w = (double*)_locals;");
        Text("out/Example.Toolkit/Generated/Core/SUMUP.g.cs").ShouldContain("byte* tag = _locals + 240;");
        Text("out/Example.Toolkit/Generated/Core/Core.g.cs")
            .ShouldContain("_region = context.Arena.Register(\"Example.Toolkit\", 496, 496, [], BufferPlacement.Default);");

        var compiled = TranslatedProgram.FromSources(
            Directory.GetFiles(Path.Combine(_root, "out"), "*.cs", SearchOption.AllDirectories).Select(File.ReadAllText).Append(Driver));
        const double Expected = (3 * 465) + 40 + 2 + 10;
        compiled.Run<double>("RunOnStack").ShouldBe(Expected);
        compiled.Run<double>("RunInArena").ShouldBe(Expected);
        compiled.Run<long>("BytesOnStack").ShouldBe(0);
        compiled.Run<long>("BytesInArena").ShouldBe(496);
    }

    [Fact]
    public void Run_WhenUnitsHaveCharacterConstants_ShouldKeepEachOnceInABlockOfTheLibrarysAndReachItThere()
    {
        // Arrange: a constant two units have, and one that holds what
        // would end a note and what a literal has to escape, and that
        // starts with a digit, which follows the zero bytes before it.
        Put("core/text.f", """
                  SUBROUTINE ONE ( S )
                  CHARACTER*(*) S
                  S = 'first'
                  END

                  SUBROUTINE TWO ( S, T )
                  CHARACTER*(*) S, T
                  S = 'first'
                  T = '2*/"q'
                  END

                  INTEGER FUNCTION CHECK ( )
                  CHARACTER*8 A, B, C
                  CALL ONE ( A )
                  CALL TWO ( B, C )
                  CHECK = 0
                  IF ( A .EQ. 'first' .AND. B .EQ. A ) CHECK = 1
                  IF ( C .NE. '2*/"q' ) CHECK = 0
                  END
            """);
        const string Driver =
            """
            using Dracocephalum.Fortrania.Runtime;

            namespace Translated;

            public static unsafe class Driver
            {
                public static int Check()
                {
                    using var context = new FortranContext();
                    return context.Get<Example.Toolkit.Core>().CHECK();
                }
            }
            """;

        // Act
        var result = Translate(Settings(Project("Example.Toolkit", Source("Core", "core/*.f"))));

        // Assert: the units are given their places in the order of their
        // files' names, CHECK before ONE before TWO.
        result.Succeeded.ShouldBeTrue();
        var one = Text("out/Example.Toolkit/Generated/Core/ONE.g.cs");
        one.ShouldContain("byte* _text = _textBlock;");
        one.ShouldContain("_text /* \"first\" */, 5);");
        one.ShouldNotContain("u8");
        Text("out/Example.Toolkit/Generated/Core/TWO.g.cs").ShouldContain("_text + 8 /* \"2* /\\\"q\" */, 5);");
        var block = Text("out/Example.Toolkit/Generated/Core/Core.Text.g.cs");
        block.ShouldContain("\"first\\0\\0\\02*/\\\"q\\0\\0\\0\"u8);");
        block.ShouldContain("set \"inlineTextConstants\": true in the translation's settings");
        Text("out/Example.Toolkit/Generated/Core/Core.g.cs").ShouldNotContain("_textBlock");

        var compiled = TranslatedProgram.FromSources(
            Directory.GetFiles(Path.Combine(_root, "out"), "*.cs", SearchOption.AllDirectories).Select(File.ReadAllText).Append(Driver));
        compiled.Run<int>("Check").ShouldBe(1);
    }

    [Fact]
    public void Run_WhenConstantsAreToBeWrittenWhereTheyAreUsed_ShouldWriteEachAsALiteralAndNoBlock()
    {
        // Arrange
        Put("core/text.f", """
                  SUBROUTINE ONE ( S )
                  CHARACTER*(*) S
                  S = 'first'
                  END
            """);
        var settings = Settings(Project("Example.Toolkit", Source("Core", "core/*.f")));
        settings[0].Part.InlineTextConstants = true;

        // Act
        var result = Translate(settings);

        // Assert
        result.Succeeded.ShouldBeTrue();
        var one = Text("out/Example.Toolkit/Generated/Core/ONE.g.cs");
        one.ShouldContain("AddrOf(\"first\"u8), 5);");
        one.ShouldNotContain("_text");
        File.Exists(Path.Combine(_root, "out/Example.Toolkit/Generated/Core/Core.Text.g.cs")).ShouldBeFalse();
    }

    [Fact]
    public void Run_WhenACharacterValueIsTestedForEquality_ShouldTestForEqualityAndNotOrderTheTwo()
    {
        // Arrange: one byte against one, a name in a longer variable, the
        // constant on the left, a test for difference, two variables, and
        // one character of each of two. A comparison that asks for an
        // order stays as it is. COUNT adds a power of two for each test
        // that comes out as FORTRAN has it.
        Put("core/tests.f", """
                  INTEGER FUNCTION COUNT ( C, NAME, OTHER, I )
                  CHARACTER*1 C
                  CHARACTER*(*) NAME, OTHER
                  INTEGER I
                  COUNT = 0
                  IF ( C .EQ. 'U' ) COUNT = COUNT + 1
                  IF ( C .NE. 'X' ) COUNT = COUNT + 2
                  IF ( NAME .EQ. 'J2000' ) COUNT = COUNT + 4
                  IF ( 'B1950' .NE. NAME ) COUNT = COUNT + 8
                  IF ( NAME .EQ. OTHER ) COUNT = COUNT + 16
                  IF ( NAME .LT. 'K' ) COUNT = COUNT + 32
                  IF ( NAME .EQ. 'J2000   X' ) COUNT = COUNT + 64
                  IF ( OTHER .NE. NAME ) COUNT = COUNT + 128
                  IF ( NAME(I:I) .EQ. OTHER(I+1:I+1) ) COUNT = COUNT + 256
                  IF ( NAME(I:I) .NE. C ) COUNT = COUNT + 512
                  IF ( 'J' .EQ. NAME(1:1) ) COUNT = COUNT + 1024
                  END
            """);
        const string Driver =
            """
            using Dracocephalum.Fortrania.Runtime;
            using static Dracocephalum.Fortrania.Runtime.Fortran;

            namespace Translated;

            public static unsafe class Driver
            {
                public static int Count()
                {
                    using var context = new FortranContext();
                    var i = 3;
                    return context.Get<Example.Toolkit.Core>().COUNT(AddrOf("U"u8), 1, AddrOf("J2000   "u8), 8, AddrOf("J2000"u8), 5, &i);
                }
            }
            """;

        // Act
        var result = Translate(Settings(Project("Example.Toolkit", Source("Core", "core/*.f"))));

        // Assert
        result.Succeeded.ShouldBeTrue();
        var count = Text("out/Example.Toolkit/Generated/Core/COUNT.g.cs");
        count.ShouldContain("if (*c == 'U')");
        count.ShouldContain("if (*c != 'X')");
        count.ShouldContain("if (IsEqual(name, name_len, _text");
        count.ShouldContain("/* \"J2000\" */, 5))");
        count.ShouldContain("if (!IsEqual(name, name_len, _text");
        count.ShouldContain("if (IsEqual(name, name_len, other, other_len))");
        count.ShouldContain("if (!IsEqual(other, other_len, name, name_len))");
        count.ShouldContain("/* \"K\" */, 1) < 0)");
        count.ShouldContain("if (*(name + i_value - 1) == *(other + i_value + 1 - 1))");
        count.ShouldContain("if (*(name + i_value - 1) != *c)");
        count.ShouldContain("if (*name == 'J')");

        var compiled = TranslatedProgram.FromSources(
            Directory.GetFiles(Path.Combine(_root, "out"), "*.cs", SearchOption.AllDirectories).Select(File.ReadAllText).Append(Driver));
        compiled.Run<int>("Count").ShouldBe(1 + 2 + 4 + 8 + 16 + 32 + 256 + 512 + 1024);
    }

    [Fact]
    public void Run_WhenOneCharacterIsAssignedToOne_ShouldStoreTheByteAndCallNothing()
    {
        // Arrange: one position of a value written twice is one character;
        // one assigned a variable's character, a constant, a value whose
        // length is not known, which stays a call, and a character the
        // target language does not write plainly. An assignment to more
        // than one character stays a call.
        Put("core/turn.f", """
                  SUBROUTINE TURN ( IN, OUT, N )
                  CHARACTER*(*) IN, OUT
                  INTEGER N, I
                  OUT = '#'
                  DO I = 1, N
                     OUT(I:I) = IN(N+1-I:N+1-I)
                  END DO
                  OUT(N+1:N+1) = '*'
                  OUT(N+2:N+2) = IN
                  OUT(N+3:N+3) = ''''
                  OUT(N+4:N+5) = IN(1:1)
                  END
            """);
        const string Driver =
            """
            using System.Text;
            using Dracocephalum.Fortrania.Runtime;
            using static Dracocephalum.Fortrania.Runtime.Fortran;

            namespace Translated;

            public static unsafe class Driver
            {
                public static string Turn()
                {
                    using var context = new FortranContext();
                    var target = stackalloc byte[10];
                    var n = 3;
                    context.Get<Example.Toolkit.Core>().TURN(AddrOf("ABC"u8), 3, target, 10, &n);
                    return Encoding.ASCII.GetString(target, 10);
                }
            }
            """;

        // Act
        var result = Translate(Settings(Project("Example.Toolkit", Source("Core", "core/*.f"))));

        // Assert
        result.Succeeded.ShouldBeTrue();
        var turn = Text("out/Example.Toolkit/Generated/Core/TURN.g.cs");
        turn.ShouldContain("*(@out + i - 1) = *(@in + n_value + 1 - i - 1);");
        turn.ShouldContain("*(@out + n_value + 1 - 1) = (byte)'*';");
        turn.ShouldContain("Assign(@out + n_value + 2 - 1, 1, @in, in_len);");
        turn.ShouldContain("*(@out + n_value + 3 - 1) = *");
        turn.ShouldContain("Assign(@out + n_value + 4 - 1, n_value + 5 - (n_value + 4) + 1, @in, 1);");

        var compiled = TranslatedProgram.FromSources(
            Directory.GetFiles(Path.Combine(_root, "out"), "*.cs", SearchOption.AllDirectories).Select(File.ReadAllText).Append(Driver));
        compiled.Run<string>("Turn").ShouldBe("CBA*A'A   ");
    }

    [Fact]
    public void Run_WhenALoopOnlyCopiesOrFills_ShouldDoALongRunAtOnceAndGiveWhatTheLoopGives()
    {
        // Arrange: COPY is called with arrays apart, and with the target a
        // few elements into the source, where the loop reads what it has
        // just written and a copy made at once would not. LAST is the
        // loop's variable as each leaves it.
        Put("core/loops.f", """
                  SUBROUTINE COPY ( A, N, B, LAST )
                  INTEGER N, LAST, I
                  DOUBLE PRECISION A ( * ), B ( * )
                  DO I = 1, N
                     B ( I ) = A ( I )
                  END DO
                  LAST = I
                  END

                  SUBROUTINE SETALL ( V, N, B, LAST )
                  INTEGER N, LAST, I
                  DOUBLE PRECISION V, B ( * )
                  DO I = 1, N
                     B ( I ) = V
                  END DO
                  LAST = I
                  END

                  SUBROUTINE OTHERS ( A, N, B )
                  INTEGER N, I
                  DOUBLE PRECISION A ( * ), B ( * )
                  DO I = 1, N
                     B ( I ) = A ( I ) + 1.0D0
                  END DO
                  DO I = 2, N
                     B ( I ) = A ( I )
                  END DO
                  END
            """);
        const string Driver =
            """
            using Dracocephalum.Fortrania.Runtime;

            namespace Translated;

            public static unsafe class Driver
            {
                // Twenty elements counting from one, copied or filled, and
                // then read back as one number each a digit of base 100.
                public static string Run(string what, int count, int shift)
                {
                    using var context = new FortranContext();
                    var library = context.Get<Example.Toolkit.Core>();
                    var a = stackalloc double[40];
                    for (var index = 0; index < 40; index++)
                    {
                        a[index] = index + 1;
                    }

                    var last = 0;
                    var value = 7.0;
                    if (what == "copy")
                    {
                        library.COPY(a, &count, a + shift, &last);
                    }
                    else
                    {
                        library.SETALL(&value, &count, a + shift, &last);
                    }

                    return last + ":" + string.Join(",", new System.Span<double>(a, 24).ToArray());
                }

                public static string CopyApart() => Run("copy", 18, 20);

                public static string CopyAhead() => Run("copy", 18, 3);

                public static string CopyBehind() => RunBehind();

                public static string CopyFew() => Run("copy", 3, 20);

                public static string CopyNone() => Run("copy", 0, 20);

                public static string FillMany() => Run("fill", 18, 2);

                public static string FillFew() => Run("fill", 2, 2);

                private static string RunBehind()
                {
                    using var context = new FortranContext();
                    var a = stackalloc double[40];
                    for (var index = 0; index < 40; index++)
                    {
                        a[index] = index + 1;
                    }

                    var count = 18;
                    var last = 0;
                    context.Get<Example.Toolkit.Core>().COPY(a + 3, &count, a, &last);
                    return last + ":" + string.Join(",", new System.Span<double>(a, 16).ToArray());
                }
            }
            """;

        // Act
        var result = Translate(Settings(Project("Example.Toolkit", Source("Core", "core/*.f"))));

        // Assert
        result.Succeeded.ShouldBeTrue();
        var copy = Text("out/Example.Toolkit/Generated/Core/COPY.g.cs");
        copy.ShouldContain("/* A long run is done at once and a short one by the loop; \"plainArrayLoops\": true in the translation's settings writes the loop alone. */");
        copy.ShouldContain("if (ShouldMoveElements(a, b, _last1))");
        copy.ShouldContain("i = MoveElements(a, b, _last1);");
        copy.ShouldContain("b[i - 1] = a[i - 1];");
        var fill = Text("out/Example.Toolkit/Generated/Core/SETALL.g.cs");
        fill.ShouldContain("if (_last1 >= LongRun)");
        fill.ShouldContain("i = FillElements(b, _last1, v_value);");
        var others = Text("out/Example.Toolkit/Generated/Core/OTHERS.g.cs");
        others.ShouldNotContain("Elements(");

        var compiled = TranslatedProgram.FromSources(
            Directory.GetFiles(Path.Combine(_root, "out"), "*.cs", SearchOption.AllDirectories).Select(File.ReadAllText).Append(Driver));
        compiled.Run<string>("CopyApart").ShouldBe("19:1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,17,18,19,20,1,2,3,4");

        // Three ahead, the loop repeats the first three elements to the end.
        compiled.Run<string>("CopyAhead").ShouldBe("19:1,2,3,1,2,3,1,2,3,1,2,3,1,2,3,1,2,3,1,2,3,22,23,24");
        compiled.Run<string>("CopyBehind").ShouldBe("19:4,5,6,7,8,9,10,11,12,13,14,15,16,17,18,19");
        compiled.Run<string>("CopyFew").ShouldBe("4:1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,17,18,19,20,1,2,3,24");
        compiled.Run<string>("CopyNone").ShouldBe("1:1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,17,18,19,20,21,22,23,24");
        compiled.Run<string>("FillMany").ShouldBe("19:1,2,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,21,22,23,24");
        compiled.Run<string>("FillFew").ShouldBe("3:1,2,7,7,5,6,7,8,9,10,11,12,13,14,15,16,17,18,19,20,21,22,23,24");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Run_WhenALoopsLengthIsKnown_ShouldWriteOnlyTheWayThatSuitsItUnlessLoopsAreToStayPlain(bool keepsLoops)
    {
        // Arrange: a short fill, a long one, one to a named constant, and
        // a long copy, which keeps its loop for arrays that overlap.
        Put("core/known.f", """
                  DOUBLE PRECISION FUNCTION KNOWN ( V )
                  INTEGER I, N
                  PARAMETER ( N = 24 )
                  DOUBLE PRECISION V, B ( 30 ), C ( 30 )
                  DO I = 1, 3
                     B ( I ) = 1.0D0
                  END DO
                  DO I = 1, 20
                     B ( I ) = 2.0D0
                  END DO
                  DO I = 1, N
                     C ( I ) = V
                  END DO
                  DO I = 1, 20
                     C ( I ) = B ( I )
                  END DO
                  KNOWN = B ( 3 ) + B ( 20 ) + C ( 20 ) + C ( 24 ) + I
                  END
            """);
        const string Driver =
            """
            using Dracocephalum.Fortrania.Runtime;

            namespace Translated;

            public static unsafe class Driver
            {
                public static double Known()
                {
                    using var context = new FortranContext();
                    var first = 5.0;
                    return context.Get<Example.Toolkit.Core>().KNOWN(&first);
                }
            }
            """;
        var settings = Settings(Project("Example.Toolkit", Source("Core", "core/*.f")));
        settings[0].Part.PlainArrayLoops = keepsLoops;

        // Act
        var result = Translate(settings);

        // Assert
        result.Succeeded.ShouldBeTrue();
        var known = Text("out/Example.Toolkit/Generated/Core/KNOWN.g.cs");
        known.ShouldContain("for (i = 1; i <= 3; i += 1)");
        if (keepsLoops)
        {
            known.ShouldNotContain("Elements(");
            known.ShouldNotContain("plainArrayLoops");
            known.ShouldMatch(@"for \(i = 1; i <= (n|24); i \+= 1\)");
        }
        else
        {
            known.ShouldContain("/* A loop that fills, done at once; \"plainArrayLoops\": true in the translation's settings writes the loop. */");
            known.ShouldContain("i = FillElements(b, 20, 2.0);");
            known.ShouldMatch(@"i = FillElements\(c, (n|24), \*v\);");
            known.ShouldNotContain("LongRun");
            known.ShouldContain("if (ShouldMoveElements(b, c, 20))");
            known.ShouldContain("i = MoveElements(b, c, 20);");
            known.Split("for (i = 1;").Length.ShouldBe(3);
        }

        var compiled = TranslatedProgram.FromSources(
            Directory.GetFiles(Path.Combine(_root, "out"), "*.cs", SearchOption.AllDirectories).Select(File.ReadAllText).Append(Driver));
        compiled.Run<double>("Known").ShouldBe(2.0 + 2.0 + 2.0 + 5.0 + 21);
    }

    [Fact]
    public void Run_WhenAProjectIsTranslatedAgainstASummaryFile_ShouldWriteWhatTranslatingBothAtOnceDoes()
    {
        // Arrange: the tool passes a function of its own to the library,
        // which calls it beneath a buffer of its own, so that the tool's
        // stack use, its region and its calls all lean on what the library
        // shows of itself.
        Put("core/lib.f", Library);
        Put("tool/main.f", Caller);
        var settings = Settings(Project("Example.Toolkit", Source("Core", "core/*.f")), Project("Example.Tool", Source("ToolMain", "tool/*.f")));
        var whole = Translate(settings);
        whole.Succeeded.ShouldBeTrue();
        var files = Contents("out/Example.Tool");
        var stack = StackOf(whole, "Example.Tool");

        // The tool's translation goes, and so do the library's sources.
        Directory.Delete(Path.Combine(_root, "out/Example.Tool"), recursive: true);
        File.Delete(Path.Combine(_root, "core/lib.f"));

        // Act: the tool alone, against the library's summary.
        var apart = Translate(settings[1], new TranslationOptions { References = [Full("out/Example.Toolkit/Example.Toolkit.fortrania-summary.json")] });

        // Assert
        apart.Succeeded.ShouldBeTrue(string.Join('\n', apart.Messages));
        apart.Units.ShouldBe(2);
        Contents("out/Example.Tool").ShouldBe(files);
        StackOf(apart, "Example.Tool").ShouldBe(stack);
        StackOf(apart, "Example.Toolkit").ShouldBeEmpty();
        apart.WorkingRegions.ShouldBe(whole.WorkingRegions);
        apart.Written.ShouldNotContain(path => path.Contains("Example.Toolkit", StringComparison.Ordinal));

        var compiled = TranslatedProgram.FromSources(
            Directory.GetFiles(Path.Combine(_root, "out"), "*.cs", SearchOption.AllDirectories).Select(File.ReadAllText));
        compiled.RunMain("Example.Tool.Program").ShouldBe(9 + 2);
    }

    [Fact]
    public void Run_WhenATranslationMadeIsUnchanged_ShouldWriteTheSameSummaryAndSayWhatCallersNeed()
    {
        // Arrange
        Put("core/lib.f", Library);
        var settings = Settings(Project("Example.Toolkit", Source("Core", "core/*.f")));
        Translate(settings).Succeeded.ShouldBeTrue();
        var first = Text("out/Example.Toolkit/Example.Toolkit.fortrania-summary.json");

        // Act
        var again = Translate(settings, new TranslationOptions { Force = true });

        // Assert: nothing a caller sees changed, so the file is not
        // written again; and it says how APPLY is called and how deep a
        // procedure passed to it is called.
        again.Written.ShouldNotContain(path => path.EndsWith(".fortrania-summary.json", StringComparison.Ordinal));
        Text("out/Example.Toolkit/Example.Toolkit.fortrania-summary.json").ShouldBe(first);
        first.ShouldContain("\"project\": \"Example.Toolkit\"");
        first.ShouldContain("{\"name\":\"APPLY\",");
        first.ShouldContain("\"kind\":\"function\"");
        first.ShouldContain("\"through\":{");
    }

    [Fact]
    public void Run_WhenAReferencedSummaryFileIsNotThere_ShouldSayWhichAndFail()
    {
        // Arrange
        Put("tool/main.f", Caller);
        var summary = Full("out/Example.Toolkit/Example.Toolkit.fortrania-summary.json");

        // Act
        var result = Translate(Project("Example.Tool", Source("ToolMain", "tool/*.f")), new TranslationOptions { References = [summary] });

        // Assert
        result.Succeeded.ShouldBeFalse();
        result.Messages.ShouldContain(message => message.IsError && message.File == summary);
    }

    [Fact]
    public void Run_WhenASummaryIsOfOtherSourcesThanTheSettingsHave_ShouldSayToTranslateItAgain()
    {
        // Arrange
        Put("core/lib.f", Library);
        Put("tool/main.f", Caller);
        var library = Project("Example.Toolkit", Source("Core", "core/*.f"));
        var settings = Settings(library, Project("Example.Tool", Source("ToolMain", "tool/*.f")));
        Translate(settings).Succeeded.ShouldBeTrue();
        library.Part.Sources[0].Name = "Renamed";
        File.Delete(Path.Combine(_root, "core/lib.f"));

        // Act: offline, the library is read from its summary.
        var result = Translate(settings, new TranslationOptions { Fetch = FetchPolicy.Never });

        // Assert
        result.Succeeded.ShouldBeFalse();
        result.Messages.ShouldContain(message => message.IsError && message.Text.Contains("translate Example.Toolkit again", StringComparison.Ordinal));
    }

    [Fact]
    public void Run_WhenTheSettingsNameNoOutput_ShouldSaySoAndWriteNothing()
    {
        // Arrange
        Put("core/core.f", Core);
        var settings = Settings(Project("Example.Toolkit", Source("Core", "core/*.f")));
        settings[0].Part.Output = null;

        // Act
        var result = Translate(settings);

        // Assert
        result.Succeeded.ShouldBeFalse();
        result.Messages.ShouldContain(message => message.IsError && message.Text.Contains("name no output", StringComparison.Ordinal));
        result.Written.ShouldBeEmpty();
    }

    [Fact]
    public void Run_WhenTranslationsLieInFoldersApart_ShouldReferToEachOtherFromWhereEachLies()
    {
        // Arrange: each translation's settings in a folder of its own, and
        // what it is written to in another.
        Put("p/core/core.f", Core);
        Put("q/tool/tool.f", Tool);
        var library = Project("Example.Toolkit", Source("Core", "core/*.f"));
        library.Part.Folder = Path.Combine(_root, "p");
        library.Part.Output = "A";
        library.Summary = Full("p/A/Example.Toolkit.fortrania-summary.json");
        var program = Project("Example.Tool", Source("ToolMain", "tool/*.f"));
        program.Part.Folder = Path.Combine(_root, "q");
        program.Part.Output = "../r/B";
        program.Summary = Full("r/B/Example.Tool.fortrania-summary.json");

        // Act
        var result = Translate(Settings(library, program));

        // Assert
        result.Succeeded.ShouldBeTrue(string.Join('\n', result.Messages));
        File.Exists(Path.Combine(_root, "p/A/Example.Toolkit.csproj")).ShouldBeTrue();
        Text("r/B/Example.Tool.csproj").ShouldContain("<ProjectReference Include=\"../../p/A/Example.Toolkit.csproj\" />");
    }

    [Fact]
    public void Run_WhenNeverToFetchAndAProjectsSourcesAreGone_ShouldReadItFromItsSummaryAndTranslateTheRest()
    {
        // Arrange: both translated once; then the library's sources go.
        Put("core/lib.f", Library);
        Put("tool/main.f", Caller);
        var library = Project("Example.Toolkit", Source("Core", "core/*.f"));
        var settings = Settings(library, Project("Example.Tool", Source("ToolMain", "tool/*.f")));
        Translate(settings).Succeeded.ShouldBeTrue();
        File.Delete(Path.Combine(_root, "core/lib.f"));

        // Act
        var offline = Translate(settings, new TranslationOptions { Fetch = FetchPolicy.Never });
        var online = Translate(settings);

        // Assert: offline, the library is read from its summary; online, it
        // is translated, and its sources are missed.
        offline.Succeeded.ShouldBeTrue(string.Join('\n', offline.Messages));
        offline.Units.ShouldBe(2);
        online.Succeeded.ShouldBeFalse();
        online.Messages.ShouldContain(message => message.IsError && message.Text.Contains("there is no such file", StringComparison.Ordinal));
    }

    [Fact]
    public void Run_WhenForced_ShouldWriteEveryTranslatedFileAgain()
    {
        // Arrange
        Put("core/core.f", Core);
        Put("tool/tool.f", Tool);
        var settings = Settings(Project("Example.Toolkit", Source("Core", "core/*.f")), Project("Example.Tool", Source("ToolMain", "tool/*.f")));
        Translate(settings).Succeeded.ShouldBeTrue();

        // Act
        var result = Translate(settings, new TranslationOptions { Force = true });

        // Assert
        result.Succeeded.ShouldBeTrue();
        result.Written.Select(Path.GetFileName).Order(StringComparer.Ordinal).ShouldBe(["CPUT.g.cs", "Core.g.cs", "TOOL.g.cs", "ToolMain.g.cs"]);
    }

    [Fact]
    public void Run_WhenTranslated_ShouldSayWhatItReadWhatItMatchedAndWhatTheTranslationIsMadeOf()
    {
        // Arrange: a project read from its summary, offline and its sources
        // gone, and one translated. Its program is not scaffolded again, the
        // project file being there. What the one read is made of is what
        // its folders hold.
        Put("core/lib.f", Library);
        Put("tool/main.f", Caller);
        var toolkit = Project("Example.Toolkit", Source("Core", "core/*.f"));
        var tool = Project("Example.Tool", Source("ToolMain", "tool/*.f"));
        Translate(Settings(toolkit, tool)).Succeeded.ShouldBeTrue();
        File.Delete(Path.Combine(_root, "core/lib.f"));

        // Act
        var result = Translate(Settings(toolkit, tool), new TranslationOptions { Fetch = FetchPolicy.Never });

        // Assert
        result.Succeeded.ShouldBeTrue(string.Join('\n', result.Messages));
        result.Read.ShouldBe([Full("out/Example.Toolkit/Example.Toolkit.fortrania-summary.json"), Full("tool/main.f")]);
        result.Patterns.ShouldBe([Full("tool/*.f")]);
        result.Files.Order(StringComparer.Ordinal).ShouldBe(
            [
                Full("out/Example.Tool/Example.Tool.fortrania-summary.json"),
                Full("out/Example.Tool/Generated/.fortrania-manifest.json"),
                Full("out/Example.Tool/Generated/ToolMain/SQUARE.g.cs"),
                Full("out/Example.Tool/Generated/ToolMain/TOOL.g.cs"),
                Full("out/Example.Tool/Generated/ToolMain/ToolMain.g.cs"),
                Full("out/Example.Toolkit/Example.Toolkit.fortrania-summary.json"),
                Full("out/Example.Toolkit/Generated/.fortrania-manifest.json"),
                Full("out/Example.Toolkit/Generated/Core/APPLY.g.cs"),
                Full("out/Example.Toolkit/Generated/Core/Core.g.cs"),
                Full("out/Example.Toolkit/Generated/Core/TWICE.g.cs"),
            ],
            ignoreOrder: true);
        result.Files.ShouldAllBe(file => File.Exists(file));
    }

    [Fact]
    public void Run_WhenAProjectFileLiesBesideTheSummary_ShouldScaffoldNoneForIt()
    {
        // Arrange: a program whose project file lies beside its settings and
        // its summary, as a project keeps them whose translation lies apart.
        Put("core/lib.f", Library);
        Put("tool/main.f", Caller);
        Put("Example.Tool.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\" />");
        var tool = Project("Example.Tool", Source("ToolMain", "tool/*.f"));
        tool.Summary = Full("Example.Tool.fortrania-summary.json");
        var settings = Settings(Project("Example.Toolkit", Source("Core", "core/*.f")), tool);

        // Act
        var result = Translate(settings);

        // Assert: the library, whose file is nowhere, is scaffolded; the
        // program, whose file is there, is not.
        result.Succeeded.ShouldBeTrue(string.Join('\n', result.Messages));
        File.Exists(Path.Combine(_root, "out/Example.Toolkit/Example.Toolkit.csproj")).ShouldBeTrue();
        File.Exists(Path.Combine(_root, "out/Example.Tool/Example.Tool.csproj")).ShouldBeFalse();
        result.Messages.ShouldBeEmpty();
    }

    [Fact]
    public void Run_WhenALibraryIsReferencedAsAnAssemblyCarryingItsSummary_ShouldTranslateTheCallerAsWithTheLibraryInTheSettings()
    {
        // Arrange: both translated together; then the library is an
        // assembly that carries its summary, and the settings have the
        // program alone, as a build of the program sees it.
        Put("core/lib.f", Library);
        Put("tool/main.f", Caller);
        var tool = Project("Example.Tool", Source("ToolMain", "tool/*.f"));
        Translate(Settings(Project("Example.Toolkit", Source("Core", "core/*.f")), tool)).Succeeded.ShouldBeTrue();
        var generated = Contents("out/Example.Tool/Generated");
        var assembly = MakeAssembly("Example.Toolkit", Text("out/Example.Toolkit/Example.Toolkit.fortrania-summary.json"));
        Directory.Delete(Path.Combine(_root, "out/Example.Tool"), recursive: true);
        File.Delete(Path.Combine(_root, "core/lib.f"));

        // Act
        var result = Translate(Settings(tool), new TranslationOptions { References = [assembly] });

        // Assert: the same translation; the project file refers to no
        // project, since whoever passed the assembly refers to it.
        result.Succeeded.ShouldBeTrue(string.Join('\n', result.Messages));
        Contents("out/Example.Tool/Generated").ShouldBe(generated);
        Text("out/Example.Tool/Example.Tool.csproj").ShouldNotContain("Example.Toolkit");
        result.WorkingRegions.Select(region => region.Project).ShouldBe(["Example.Tool", "Example.Toolkit"], ignoreOrder: true);

        var compiled = TranslatedProgram.FromSources(
            Directory.GetFiles(Path.Combine(_root, "out"), "*.cs", SearchOption.AllDirectories).Select(File.ReadAllText));
        compiled.RunMain("Example.Tool.Program").ShouldBe(9 + 2);
    }

    [Fact]
    public void Run_WhenAReferencedAssemblyCarriesNoSummary_ShouldPassItOver()
    {
        // Arrange
        Put("tool/main.f", Caller);
        var assembly = MakeAssembly("Plain", summary: null);

        // Act
        var result = Translate(Settings(Project("Example.Tool", Source("ToolMain", "tool/*.f"))), new TranslationOptions { References = [assembly] });

        // Assert: what the program calls is then defined nowhere.
        result.Messages.ShouldContain(message => !message.IsError && message.Text.Contains("calls APPLY, which no source of the translation defines", StringComparison.Ordinal));
    }

    [Fact]
    public void Run_WhenTwoReferencedAssembliesDefineOneProcedure_ShouldSayWhichAndFail()
    {
        // Arrange: one library's summary under two projects' names.
        Put("core/lib.f", Library);
        Put("tool/main.f", Caller);
        Translate(Settings(Project("Example.Toolkit", Source("Core", "core/*.f")))).Succeeded.ShouldBeTrue();
        var summary = Text("out/Example.Toolkit/Example.Toolkit.fortrania-summary.json");
        var first = MakeAssembly("Example.First", summary.Replace("\"Example.Toolkit\"", "\"Example.First\"", StringComparison.Ordinal));
        var second = MakeAssembly("Example.Second", summary.Replace("\"Example.Toolkit\"", "\"Example.Second\"", StringComparison.Ordinal));

        // Act
        var result = Translate(Settings(Project("Example.Tool", Source("ToolMain", "tool/*.f"))), new TranslationOptions { References = [first, second] });

        // Assert
        result.Succeeded.ShouldBeFalse();
        result.Messages.ShouldContain(message => message.IsError && message.Text.Contains("APPLY is defined by Example.First and by Example.Second", StringComparison.Ordinal));
    }

    [Fact]
    public void Run_WhenAReferencedAssemblyHoldsAProjectOfTheSettings_ShouldSaySoAndFail()
    {
        // Arrange
        Put("core/lib.f", Library);
        var settings = Settings(Project("Example.Toolkit", Source("Core", "core/*.f")));
        Translate(settings).Succeeded.ShouldBeTrue();
        var assembly = MakeAssembly("Example.Toolkit", Text("out/Example.Toolkit/Example.Toolkit.fortrania-summary.json"));

        // Act
        var result = Translate(settings, new TranslationOptions { References = [assembly] });

        // Assert
        result.Succeeded.ShouldBeFalse();
        result.Messages.ShouldContain(message => message.IsError && message.Text.Contains("holds the project Example.Toolkit, which the run or another reference has as well", StringComparison.Ordinal));
    }

    [Fact]
    public void Run_WhenAUnitHasEntryPoints_ShouldCountEachByWhatItsOwnStatementsTakeAndCall()
    {
        // Arrange: only KEEP names the array, and only PEEK calls LEAF.
        Put("core/keep.f", """
                  SUBROUTINE KEEP ( N )
                  INTEGER N
                  DOUBLE PRECISION BIG ( 100 )
                  BIG ( 1 ) = N
                  RETURN

                  ENTRY PEEK ( N )
                  CALL LEAF
                  N = 1
                  END

                  SUBROUTINE LEAF
                  INTEGER B ( 8 )
                  B ( 1 ) = 1
                  END
            """);

        // Act
        var result = Translate(Settings(Project("Example.Toolkit", Source("Core", "core/*.f"))));

        // Assert
        result.Succeeded.ShouldBeTrue();
        var keep = result.StackUse.Single(use => use.Procedure == "KEEP");
        keep.OwnBytes.ShouldBe(800);
        keep.DeepestChain.ShouldBe(["KEEP"]);

        var peek = result.StackUse.Single(use => use.Procedure == "PEEK");
        peek.OwnBytes.ShouldBe(0);
        peek.DeepestBytes.ShouldBe(32);
        peek.DeepestChain.ShouldBe(["PEEK", "LEAF"]);
    }

    [Fact]
    public void Run_WhenACallReachesAnotherProject_ShouldFollowItThere()
    {
        // Arrange
        Put("core/leaf.f", "      SUBROUTINE LEAF\n      INTEGER B ( 8 )\n      B ( 1 ) = 1\n      END\n");
        Put("tool/tool.pgm", "      PROGRAM TOOL\n      CALL LEAF\n      END\n");

        // Act
        var result = Translate(
            Settings(Project("Example.Toolkit", Source("Core", "core/*.f")), Project("Example.Tool", Source("ToolMain", "tool/*.pgm"))));

        // Assert
        result.Succeeded.ShouldBeTrue();
        var tool = result.StackUse.Single(use => use.Procedure == "TOOL");
        tool.Project.ShouldBe("Example.Tool");
        tool.DeepestBytes.ShouldBe(32);
        tool.DeepestChain.ShouldBe(["TOOL", "LEAF"]);
        result.StackUse.Select(use => use.Project).ShouldBe(["Example.Tool", "Example.Toolkit"]);

        // Each project has a region of its own, and the tool's reach is its
        // own and, after it, the library's it calls into. Each procedure's
        // place is within its own project's.
        result.StackUse.ShouldAllBe(use => use.Place == 0);
        result.WorkingRegions.ShouldBe([new WorkingRegion("Example.Tool", 0, 32), new WorkingRegion("Example.Toolkit", 32, 32)]);
    }

    [Fact]
    public void Run_WhenProceduresCallEachOtherInACircle_ShouldSaySoAndStopWhereItTurnsBack()
    {
        // Arrange
        Put("core/circle.f", "      SUBROUTINE ONE\n      CALL TWO\n      END\n\n      SUBROUTINE TWO\n      INTEGER B ( 2 )\n      B ( 1 ) = 1\n      CALL ONE\n      END\n");

        // Act
        var result = Translate(Settings(Project("Example.Toolkit", Source("Core", "core/*.f"))));

        // Assert
        result.Succeeded.ShouldBeTrue();
        var one = result.StackUse.Single(use => use.Procedure == "ONE");
        one.IsRecursive.ShouldBeTrue();

        // Procedures that call each other in a circle have no place in a
        // region, since neither can lie after the other.
        result.StackUse.ShouldAllBe(use => use.Place == null);
        result.WorkingRegions.ShouldHaveSingleItem().OwnBytes.ShouldBe(0);
        one.DeepestBytes.ShouldBe(8);
        one.DeepestChain.ShouldBe(["ONE", "TWO"]);
    }

    [Fact]
    public void Run_WhenEquivalencePlacesAVariableOffItsAlignment_ShouldListIt()
    {
        // Arrange
        Put("core/shared.f", """
                  SUBROUTINE SHARED ( X )
                  DOUBLE PRECISION X, D, E
                  INTEGER I ( 3 ), J ( 4 )
                  EQUIVALENCE ( I ( 2 ), D ), ( J ( 3 ), E )
                  I ( 1 ) = 0
                  J ( 1 ) = 0
                  X = D + E
                  END
            """);

        // Act
        var result = Translate(Settings(Project("Example.Toolkit", Source("Core", "core/*.f"))));

        // Assert: D begins 4 bytes in, which a double is not aligned on; E
        // begins 8 bytes in, which it is.
        result.Succeeded.ShouldBeTrue();
        var place = result.UnalignedPlaces.ShouldHaveSingleItem();
        place.ShouldBe(new UnalignedPlace("Example.Toolkit", "Core", "SHARED", Path.Combine(_root, "core", "shared.f"), "D", 4, 8, 2));
        result.StackUse.Single().OwnBytes.ShouldBe(12 + 16);
    }

    [Fact]
    public void Run_WhenACallPassesAFourByteNumberForAnEightByteOne_ShouldListItOnce()
    {
        // Arrange: the statements after TOO run on from CALLER as well, so
        // both of its methods hold the first call.
        Put("core/widen.f", """
                  SUBROUTINE CALLER
                  INTEGER IC ( 4 )
                  REAL R
                  DOUBLE PRECISION D
                  IC ( 1 ) = 0
                  R = 0
                  D = 0
                  ENTRY TOO
                  CALL TAKE ( IC ( 2 ) )
                  CALL TAKE ( R )
                  CALL TAKE ( D )
                  CALL TAKE ( 1.0D0 )
                  CALL TAKE ( 1 )
                  END

                  SUBROUTINE TAKE ( X )
                  DOUBLE PRECISION X
                  X = 1
                  END
            """);

        // Act
        var result = Translate(Settings(Project("Example.Toolkit", Source("Core", "core/*.f"))));

        // Assert: a value has a local of the larger type to itself, and a
        // double is passed as itself, so neither is listed.
        result.Succeeded.ShouldBeTrue();
        var file = Path.Combine(_root, "core", "widen.f");
        result.WidenedPlaces.ShouldBe(
        [
            new WidenedPlace("Example.Toolkit", "Core", "CALLER", file, "IC", "TAKE", 1, 9),
            new WidenedPlace("Example.Toolkit", "Core", "CALLER", file, "R", "TAKE", 1, 10),
        ]);
    }

    [Fact]
    public void Run_WhenAProjectIsMadeOfTwoSettingsFiles_ShouldTranslateThemAsOneWhoseSourcesCallEachOtherBothWays()
    {
        // Arrange: each settings file's source calls into the other's, which
        // two projects could not do.
        Put("a/a.f", "      SUBROUTINE AONE ( N )\n      INTEGER N\n      CALL BONE ( N )\n      END\n");
        Put("b/b.f", "      SUBROUTINE BONE ( N )\n      INTEGER N\n      IF ( N .GT. 0 ) CALL AONE ( N - 1 )\n      END\n");
        var project = new TranslationProject
        {
            Name = "Example.Both",
            Settings =
            [
                new TranslationSettings { Folder = _root, Output = "out/a", Sources = [Source("A", "a/*.f")] },
                new TranslationSettings { Folder = _root, Output = "out/b", Sources = [Source("B", "b/*.f")] },
            ],
        };

        // Act
        var result = Translator.Run(project);

        // Assert: one summary, beside the first settings file, of both
        // sources, and each settings file's translation in its own folder.
        result.Succeeded.ShouldBeTrue(string.Join('\n', result.Messages));
        var summary = Text("Example.Both.fortrania-summary.json");
        summary.ShouldContain("\"project\": \"Example.Both\"");
        summary.ShouldContain("\"name\": \"A\"");
        summary.ShouldContain("\"name\": \"B\"");
        File.Exists(Path.Combine(_root, "out/a/Generated/.fortrania-manifest.json")).ShouldBeTrue();
        Text("out/b/Generated/B/BONE.g.cs").ShouldContain("namespace Example.Both;");
        result.WorkingRegions.Select(region => region.Project).ShouldBe(["Example.Both"]);
    }

    [Fact]
    public void Run_WhenScaffoldingAProjectWhoseTranslationLiesOutsideItsFolder_ShouldCompileTheTranslationFromWhereItLies()
    {
        // Arrange: the summary, and so the project, beside the settings, and
        // the translation elsewhere.
        Put("core/core.f", Core);
        var project = Project("Example.Toolkit", Source("Core", "core/*.f"));
        project.Summary = null;
        project.Part.Output = "../generated";

        // Act
        var result = Translate(project);

        // Assert
        result.Succeeded.ShouldBeTrue(string.Join('\n', result.Messages));
        File.Exists(Path.Combine(_root, "Example.Toolkit.fortrania-summary.json")).ShouldBeTrue();
        Text("Example.Toolkit.csproj").ShouldContain("<Compile Include=\"../generated/Generated/**/*.g.cs\" />");
    }

    [Fact]
    public void Run_WhenNeverToFetchAndOnlyTheSummaryIsThere_ShouldSayToFetch()
    {
        // Arrange: translated once, then the sources and the translation go
        // and the summary stays, as a checkout has it that commits only it.
        Put("core/core.f", Core);
        var project = Project("Example.Toolkit", Source("Core", "core/*.f"));
        Translate(project).Succeeded.ShouldBeTrue();
        File.Delete(Path.Combine(_root, "core/core.f"));
        Directory.Delete(Path.Combine(_root, "out/Example.Toolkit/Generated"), recursive: true);

        // Act
        var result = Translate(project, new TranslationOptions { Fetch = FetchPolicy.Never });

        // Assert
        result.Succeeded.ShouldBeFalse();
        result.Messages.ShouldHaveSingleItem().Text.ShouldContain("fetch them first");
    }

    [Fact]
    public void Run_WhenNeverToFetchAndAManifestCannotBeRead_ShouldTakeTheTranslationForNotAllThere()
    {
        // Arrange: translated once, then the sources go and the manifest is
        // spoiled, as one of an older translator's would be.
        Put("core/core.f", Core);
        var project = Project("Example.Toolkit", Source("Core", "core/*.f"));
        Translate(project).Succeeded.ShouldBeTrue();
        File.Delete(Path.Combine(_root, "core/core.f"));
        Put("out/Example.Toolkit/Generated/.fortrania-manifest.json", "not a manifest");

        // Act
        var result = Translate(project, new TranslationOptions { Fetch = FetchPolicy.Never });

        // Assert
        result.Succeeded.ShouldBeFalse();
        result.Messages.ShouldHaveSingleItem().Text.ShouldContain("fetch them first");
    }

    [Fact]
    public void Run_WhenNeverToFetchAndASettingsFileOfTheProjectHasNoSources_ShouldStillTakeTheTranslationForAllThere()
    {
        // Arrange: a second settings file of the project with no sources of
        // its own, whose manifest lists none.
        Put("core/core.f", Core);
        var project = Project("Example.Toolkit", Source("Core", "core/*.f"));
        project.Settings = [project.Part, new TranslationSettings { Folder = _root, Output = "out/Example.Toolkit/more" }];
        Translate(project).Succeeded.ShouldBeTrue();
        Text("out/Example.Toolkit/more/Generated/.fortrania-manifest.json").ShouldBe("{\n  \"sources\": {}\n}\n");
        File.Delete(Path.Combine(_root, "core/core.f"));

        // Act
        var result = Translate(project, new TranslationOptions { Fetch = FetchPolicy.Never });

        // Assert
        result.Succeeded.ShouldBeTrue(string.Join('\n', result.Messages));
        result.Units.ShouldBe(0);
    }

    [Fact]
    public void Run_WhenAProjectIsTranslatedAgainstASummaryFileBesideAProjectFile_ShouldScaffoldAReferenceToThatProject()
    {
        // Arrange: the library scaffolded on its own, then the tool against
        // its summary file.
        Put("core/core.f", Core);
        Put("tool/tool.pgm", Tool);
        Translate(Project("Example.Toolkit", Source("Core", "core/*.f"))).Succeeded.ShouldBeTrue();

        // Act
        var result = Translate(
            Project("Example.Tool", Source("ToolMain", "tool/*.pgm")),
            new TranslationOptions { References = [Full("out/Example.Toolkit/Example.Toolkit.fortrania-summary.json")] });

        // Assert
        result.Succeeded.ShouldBeTrue(string.Join('\n', result.Messages));
        Text("out/Example.Tool/Example.Tool.csproj").ShouldContain("<ProjectReference Include=\"../Example.Toolkit/Example.Toolkit.csproj\" />");
    }

    [Fact]
    public void Run_WhenLibrariesOfAProjectDoNotCallEachOther_ShouldHaveEachAskForItsOwnBuffersAndACallerForWhatItReaches()
    {
        // Arrange: two libraries with buffers of different sizes that never
        // call each other, and a third that calls the larger.
        Put("big/big.f", "      SUBROUTINE BIG ( X )\n      DOUBLE PRECISION X, W ( 200 )\n      INTEGER I\n      DO I = 1, 200\n         W ( I ) = X + I\n      END DO\n      X = W ( 200 )\n      END\n");
        Put("small/small.f", "      SUBROUTINE SMALL ( X )\n      DOUBLE PRECISION X, W ( 20 )\n      INTEGER I\n      DO I = 1, 20\n         W ( I ) = X + I\n      END DO\n      X = W ( 20 )\n      END\n");
        Put("caller/caller.f", "      SUBROUTINE CALLER ( X )\n      DOUBLE PRECISION X\n      CALL BIG ( X )\n      END\n");
        const string Driver =
            """
            using Dracocephalum.Fortrania.Runtime;

            namespace Translated;

            public static unsafe class Driver
            {
                // The block when the smaller library alone is made, and when
                // the caller is too, which makes the larger on its first call.
                public static long[] Sizes()
                {
                    using var context = new FortranContext(new FortranContextOptions { BufferPlacement = BufferPlacement.Arena });
                    double x = 1;
                    context.Get<Example.Kit.SmallLib>().SMALL(&x);
                    var small = context.Arena.Bytes;
                    context.Get<Example.Kit.CallerLib>().CALLER(&x);
                    return [small, context.Arena.Bytes, (long)x];
                }
            }
            """;

        // Act
        var result = Translate(Project("Example.Kit", Source("BigLib", "big/*.f"), Source("SmallLib", "small/*.f"), Source("CallerLib", "caller/*.f")));

        // Assert: the region is as large as the larger library needs, the
        // smaller asks for less, and the caller for what the larger needs.
        result.Succeeded.ShouldBeTrue(string.Join('\n', result.Messages));
        var region = result.WorkingRegions.ShouldHaveSingleItem();
        long Asked(string source)
        {
            var match = System.Text.RegularExpressions.Regex.Match(
                Text($"out/Example.Kit/Generated/{source}/{source}.g.cs"), @"Arena.Register\(""Example\.Kit"", (\d+), (\d+),");
            match.Success.ShouldBeTrue();
            return long.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
        }

        Asked("BigLib").ShouldBe(region.OwnBytes);
        Asked("SmallLib").ShouldBeLessThan(Asked("BigLib"));
        Asked("SmallLib").ShouldBeGreaterThan(0);
        Asked("CallerLib").ShouldBe(Asked("BigLib"));

        var compiled = TranslatedProgram.FromSources(
            Directory.GetFiles(Path.Combine(_root, "out"), "*.cs", SearchOption.AllDirectories).Select(File.ReadAllText).Append(Driver));
        var sizes = compiled.Run<long[]>("Sizes");
        sizes[0].ShouldBe(Asked("SmallLib"));
        sizes[1].ShouldBe(region.OwnBytes);
        sizes[2].ShouldBe(1 + 20 + 200);
    }

    [Fact]
    public void Run_WhenProceduresCallEachOtherInACircle_ShouldKeepTheirBuffersOnTheStackAndSayWhyAbove()
    {
        // Arrange: TOP and MID call each other, LEAF is called from MID,
        // and OUTER calls into the circle from above it. Each has buffers.
        Put("core/circle.f", """
                  SUBROUTINE OUTER ( N )
                  INTEGER N, A ( 4 )
                  A ( 1 ) = N
                  CALL TOP ( A ( 1 ) )
                  N = A ( 1 )
                  END

                  SUBROUTINE TOP ( N )
                  INTEGER N, W ( 4 )
                  W ( 1 ) = N
                  IF ( N .GT. 0 ) CALL MID ( W ( 1 ) )
                  N = W ( 1 )
                  END

                  SUBROUTINE MID ( N )
                  INTEGER N, B ( 4 )
                  B ( 1 ) = N - 1
                  CALL LEAF ( B ( 1 ) )
                  CALL TOP ( B ( 1 ) )
                  N = B ( 1 ) + 10
                  END

                  SUBROUTINE LEAF ( N )
                  INTEGER N, C ( 4 )
                  C ( 1 ) = N
                  N = C ( 1 )
                  END
            """);
        const string Driver =
            """
            using Dracocephalum.Fortrania.Runtime;

            namespace Translated;

            public static unsafe class Driver
            {
                public static int[] RunInArena()
                {
                    using var context = new FortranContext(new FortranContextOptions { BufferPlacement = BufferPlacement.Arena });
                    int n = 3;
                    context.Get<Example.Toolkit.Core>().OUTER(&n);
                    return [n, context.Arena.Depth];
                }
            }
            """;

        // Act
        var result = Translate(Project("Example.Toolkit", Source("Core", "core/*.f")));

        // Assert: the circle and what it calls keep the stack, each saying
        // why; OUTER, above it, has a place.
        result.Succeeded.ShouldBeTrue(string.Join('\n', result.Messages));
        var top = Text("out/Example.Toolkit/Generated/Core/TOP.g.cs");
        top.ShouldContain("// TOP lies on a chain of calls that comes back to itself, TOP → MID → TOP,");
        top.ShouldNotContain("CurrentSource");
        Text("out/Example.Toolkit/Generated/Core/MID.g.cs").ShouldContain("// MID lies on a chain of calls that comes back to itself, MID → TOP → MID,");
        var leaf = Text("out/Example.Toolkit/Generated/Core/LEAF.g.cs");
        leaf.ShouldContain("// LEAF is called from a chain of calls that comes back to itself, MID → TOP → MID,");
        leaf.ShouldNotContain("CurrentSource");
        var outer = Text("out/Example.Toolkit/Generated/Core/OUTER.g.cs");
        outer.ShouldContain("_region.CurrentSource == BufferSource.Stack");
        outer.ShouldNotContain("comes back to itself");

        var compiled = TranslatedProgram.FromSources(
            Directory.GetFiles(Path.Combine(_root, "out"), "*.cs", SearchOption.AllDirectories).Select(File.ReadAllText).Append(Driver));
        compiled.Run<int[]>("RunInArena").ShouldBe([3 + (3 * 9), 0]);
    }

    [Fact]
    public void Run_WhenAProcedureCalledFromOutsideStops_ShouldCloseTheCopyOfTheArenaItOpened()
    {
        // Arrange
        Put("core/stops.f", "      SUBROUTINE STOPS ( N )\n      INTEGER N, W ( 8 )\n      W ( 1 ) = N\n      IF ( W ( 1 ) .GT. 0 ) STOP 3\n      N = W ( 1 )\n      END\n");
        const string Driver =
            """
            using Dracocephalum.Fortrania.Runtime;

            namespace Translated;

            public static unsafe class Driver
            {
                public static int[] RunInArena()
                {
                    using var context = new FortranContext(new FortranContextOptions { BufferPlacement = BufferPlacement.Arena });
                    int n = 1;
                    try
                    {
                        context.Get<Example.Toolkit.Core>().STOPS(&n);
                        return [-1, context.Arena.Depth];
                    }
                    catch (FortranStopException stop)
                    {
                        return [stop.ExitCode, context.Arena.Depth];
                    }
                }
            }
            """;

        // Act
        var result = Translate(Project("Example.Toolkit", Source("Core", "core/*.f")));

        // Assert
        result.Succeeded.ShouldBeTrue(string.Join('\n', result.Messages));
        var compiled = TranslatedProgram.FromSources(
            Directory.GetFiles(Path.Combine(_root, "out"), "*.cs", SearchOption.AllDirectories).Select(File.ReadAllText).Append(Driver));
        compiled.Run<int[]>("RunInArena").ShouldBe([3, 0]);
    }

    [Fact]
    public void Run_WhenProceduresWithBuffersCallEachOther_ShouldHaveCoresCallCoresAndOthersTheEntry()
    {
        // Arrange: PLAIN has no buffers and is called from outside; it
        // calls OUTER, whose core calls INNER of another library of the
        // project, both with buffers.
        Put("first/first.f", """
                  SUBROUTINE PLAIN ( N )
                  INTEGER N
                  CALL OUTER ( N )
                  END

                  SUBROUTINE OUTER ( N )
                  INTEGER N, W ( 4 )
                  W ( 1 ) = N
                  CALL INNER ( W ( 1 ) )
                  N = W ( 1 )
                  END
            """);
        Put("second/second.f", "      SUBROUTINE INNER ( N )\n      INTEGER N, B ( 4 )\n      B ( 1 ) = N * 2\n      N = B ( 1 )\n      END\n");
        const string Driver =
            """
            using Dracocephalum.Fortrania.Runtime;

            namespace Translated;

            public static unsafe class Driver
            {
                public static int[] RunInArena()
                {
                    using var context = new FortranContext(new FortranContextOptions { BufferPlacement = BufferPlacement.Arena });
                    int n = 5;
                    context.Get<Example.Toolkit.FirstLib>().PLAIN(&n);
                    return [n, context.Arena.Depth];
                }
            }
            """;

        // Act
        var result = Translate(Project("Example.Toolkit", Source("FirstLib", "first/*.f"), Source("SecondLib", "second/*.f")));

        // Assert: PLAIN, which may be the outermost call, reaches OUTER by its
        // entry; OUTER's core reaches INNER's core in the other library.
        result.Succeeded.ShouldBeTrue(string.Join('\n', result.Messages));
        var plain = Text("out/Example.Toolkit/Generated/FirstLib/PLAIN.g.cs");
        plain.ShouldContain("OUTER(");
        plain.ShouldNotContain("_OUTER_core(");
        Text("out/Example.Toolkit/Generated/FirstLib/OUTER.g.cs").ShouldContain("_SecondLib._INNER_core(");

        var compiled = TranslatedProgram.FromSources(
            Directory.GetFiles(Path.Combine(_root, "out"), "*.cs", SearchOption.AllDirectories).Select(File.ReadAllText).Append(Driver));
        compiled.Run<int[]>("RunInArena").ShouldBe([10, 0]);
    }

    // A run that scaffolds, as most of these tests look at the project files
    // too, and whose project files refer to a runtime of a version that does
    // not change with the translator's, unless the options say otherwise.
    private static TranslationResult Translate(IReadOnlyList<TranslationProject> projects, TranslationOptions? options = null)
    {
        options ??= new TranslationOptions();
        options.Scaffold = true;
        if (options.Runtime is { Project: null, Package: null, OmitPackageVersion: false })
        {
            options.Runtime = new RuntimeSettings { Package = "1.2.3" };
        }

        return Translator.Run(projects, options);
    }

    private static TranslationResult Translate(TranslationProject project, TranslationOptions? options = null) => Translate([project], options);

    // A run of the projects given.
    private static List<TranslationProject> Settings(params TranslationProject[] projects) => [.. projects];

    // A project of one settings file, as a file in the test's folder would
    // give it: written under out/, to a folder of its name, with its summary
    // there as well, beside its project file.
    private TranslationProject Project(string name, params SourceSettings[] sources) => new()
    {
        Name = name,
        Settings = [new TranslationSettings { Folder = _root, Output = "out/" + name, Sources = [.. sources] }],
        Summary = Path.Combine(_root, "out", name, ProjectSummary.GetFileName(name)),
    };

    private static SourceSettings Source(string name, params string[] files) => new() { Name = name, Files = [new FileSettings { Paths = [.. files] }] };

    private void Put(string path, string text)
    {
        var full = Path.Combine(_root, path);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, text);
    }

    private string Text(string path) => File.ReadAllText(Path.Combine(_root, path));

    private string Full(string path) => Path.GetFullPath(Path.Combine(_root, path));

    // An assembly of the name given that carries a summary as its resource,
    // as the build package is to embed one; or none.
    private string MakeAssembly(string name, string? summary)
    {
        var compilation = CSharpCompilation.Create(
            name,
            [],
            [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var path = Path.Combine(_root, name + ".dll");
        using var file = File.Create(path);
        var bytes = summary is null ? null : Encoding.UTF8.GetBytes(summary);
        var result = compilation.Emit(
            file,
            manifestResources: bytes is null ? null : [new ResourceDescription(ProjectSummary.ResourceName, () => new MemoryStream(bytes), isPublic: true)]);
        result.Success.ShouldBeTrue();
        return path;
    }

    // Every file under a folder, by its path from the root, with its bytes
    // as text.
    private Dictionary<string, string> Contents(string folder) => Files(folder).ToDictionary(path => path, Text);

    // A project's stack use as text, the chain of each spelled out, since a
    // record that holds a list compares the list by reference.
    private static string[] StackOf(TranslationResult result, string project) =>
    [
        .. result.StackUse
            .Where(use => use.Project == project)
            .Select(use => $"{use.Source} {use.Procedure} {use.OwnBytes} {use.DeepestBytes} {use.Place} {use.TakesProcedures} {use.IsRecursive}: {string.Join(' ', use.DeepestChain)}"),
    ];

    private string[] Files(string folder) =>
    [
        .. Directory.GetFiles(Path.Combine(_root, folder), "*", SearchOption.AllDirectories)
            .Select(file => Path.GetRelativePath(_root, file).Replace('\\', '/'))
            .Order(StringComparer.Ordinal),
    ];
}
