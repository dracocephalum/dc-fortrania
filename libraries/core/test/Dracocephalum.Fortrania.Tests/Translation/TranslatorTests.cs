using Dracocephalum.Fortrania.Tests.Emit;
using Dracocephalum.Fortrania.Translation;
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
        var result = Translator.Run(settings, _root);

        // Assert
        result.Messages.ShouldBeEmpty();
        result.Succeeded.ShouldBeTrue();
        result.Units.ShouldBe(2);
        Files("out").ShouldBe(
        [
            "out/Example.Tool/Example.Tool.csproj",
            "out/Example.Tool/Generated/ToolMain/.fortrania.json",
            "out/Example.Tool/Generated/ToolMain/TOOL.g.cs",
            "out/Example.Tool/Generated/ToolMain/ToolMain.g.cs",
            "out/Example.Tool/Program.cs",
            "out/Example.Toolkit/Example.Toolkit.csproj",
            "out/Example.Toolkit/Generated/Core/.fortrania.json",
            "out/Example.Toolkit/Generated/Core/CPUT.g.cs",
            "out/Example.Toolkit/Generated/Core/Core.g.cs",
        ]);
        result.Written.Count.ShouldBe(7);

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
    public void Run_WhenAProjectGivesProperties_ShouldWriteThemIntoTheProjectFileItMakes()
    {
        // Arrange
        Put("core/core.f", Core);
        var project = Project("Example.Toolkit", Source("Core", "core/*.f"));
        project.Properties["TreatWarningsAsErrors"] = "false";
        project.Properties["Description"] = "Translated <unchanged> & kept";

        // Act
        var result = Translator.Run(Settings(project), _root);

        // Assert
        result.Succeeded.ShouldBeTrue();
        var file = Text("out/Example.Toolkit/Example.Toolkit.csproj");
        file.ShouldContain("    <AllowUnsafeBlocks>true</AllowUnsafeBlocks>\n    <TreatWarningsAsErrors>false</TreatWarningsAsErrors>\n");
        file.ShouldContain("<Description>Translated &lt;unchanged&gt; &amp; kept</Description>");
    }

    [Fact]
    public void Run_WhenNothingChanged_ShouldWriteNothingTheSecondTime()
    {
        // Arrange
        Put("core/core.f", Core);
        var settings = Settings(Project("Example.Toolkit", Source("Core", "core/*.f")));
        Translator.Run(settings, _root).Succeeded.ShouldBeTrue();

        // Act
        var second = Translator.Run(settings, _root);

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
        Translator.Run(settings, _root).Succeeded.ShouldBeTrue();
        Put("out/Example.Toolkit/Generated/Core/Mine.g.cs", "// written by hand\n");
        Put("out/Example.Toolkit/Generated/Core/Partial.cs", "// also by hand\n");
        File.Delete(Path.Combine(_root, "core/extra.f"));

        // Act
        var result = Translator.Run(settings, _root);

        // Assert
        result.Succeeded.ShouldBeTrue();
        result.Removed.Select(Path.GetFileName).ShouldBe(["EXTRA.g.cs"]);
        Files("out/Example.Toolkit/Generated").ShouldBe(
        [
            "out/Example.Toolkit/Generated/Core/.fortrania.json",
            "out/Example.Toolkit/Generated/Core/CPUT.g.cs",
            "out/Example.Toolkit/Generated/Core/Core.g.cs",
            "out/Example.Toolkit/Generated/Core/Mine.g.cs",
            "out/Example.Toolkit/Generated/Core/Partial.cs",
        ]);
    }

    [Fact]
    public void Run_WhenAProjectHasAProjectFile_ShouldWriteSourcesOnlyAndSayWhatTheProjectFileLacks()
    {
        // Arrange
        Put("core/core.f", Core);
        Put("tool/tool.pgm", Tool);
        Put("tool/other.pgm", "      PROGRAM OTHER\n      CALL CPUT ( 1 )\n      END\n");
        Put("out/Example.Tool/Example.Tool.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\" />\n");
        var settings = Settings(Project("Example.Toolkit", Source("Core", "core/*.f")), Project("Example.Tool", Source("ToolMain", "tool/*.pgm")));

        // Act
        var result = Translator.Run(settings, _root);

        // Assert
        result.Succeeded.ShouldBeTrue();
        Text("out/Example.Tool/Example.Tool.csproj").ShouldBe("<Project Sdk=\"Microsoft.NET.Sdk\" />\n");
        File.Exists(Path.Combine(_root, "out/Example.Tool/Program.cs")).ShouldBeFalse();
        File.Exists(Path.Combine(_root, "out/Example.Tool/Generated/ToolMain/OTHER.g.cs")).ShouldBeTrue();
        var warning = result.Messages.ShouldHaveSingleItem();
        warning.IsError.ShouldBeFalse();
        warning.Text.ShouldBe("The project calls into Example.Toolkit and its project file does not refer to it.");
        warning.File.ShouldNotBeNull().ShouldEndWith("Example.Tool.csproj");
    }

    [Fact]
    public void Run_WhenAProjectBeingMadeHasSeveralMainPrograms_ShouldAskForItsEntryAndTakeTheOneNamed()
    {
        // Arrange
        Put("core/core.f", Core);
        Put("tool/tool.pgm", Tool);
        Put("tool/other.pgm", "      PROGRAM OTHER\n      CALL CPUT ( 1 )\n      END\n");
        var tool = Project("Example.Tool", Source("ToolMain", "tool/*.pgm"));
        var settings = Settings(Project("Example.Toolkit", Source("Core", "core/*.f")), tool);

        // Act
        var refused = Translator.Run(settings, _root);
        tool.Entry = "other";
        var accepted = Translator.Run(settings, _root);

        // Assert
        refused.Succeeded.ShouldBeFalse();
        refused.Messages.ShouldHaveSingleItem().Text.ShouldBe("The project Example.Tool has 2 main programs, OTHER, TOOL; name one as its entry.");
        refused.Written.ShouldBeEmpty();
        accepted.Succeeded.ShouldBeTrue();
        Text("out/Example.Tool/Program.cs").ShouldContain("context.Get<ToolMain>().OTHER();");
    }

    [Theory]
    [InlineData("TOOL", null, "The project Example.Tool names MISSING as its entry, which is not one of its main programs: TOOL.")]
    [InlineData(null, ProjectKind.Executable, "The project Example.Toolkit is to be an executable and has no main program.")]
    public void Run_WhenAnExecutableCannotBeStarted_ShouldSayWhy(string? program, ProjectKind? libraryKind, string message)
    {
        // Arrange
        Put("core/core.f", Core);
        Put("tool/tool.pgm", Tool);
        var library = Project("Example.Toolkit", Source("Core", "core/*.f"));
        library.Kind = libraryKind;
        var tool = Project("Example.Tool", Source("ToolMain", "tool/*.pgm"));
        tool.Entry = program is null ? null : "MISSING";

        // Act
        var result = Translator.Run(Settings(library, tool), _root);

        // Assert
        result.Succeeded.ShouldBeFalse();
        result.Messages.Select(found => found.Text).ShouldContain(message);
        Directory.Exists(Path.Combine(_root, "out")).ShouldBeFalse();
    }

    [Fact]
    public void Run_WhenAProjectWithAMainProgramIsSaidToBeALibrary_ShouldWriteNoMain()
    {
        // Arrange
        Put("core/core.f", Core);
        Put("tool/tool.pgm", Tool);
        var project = Project("Example.All", Source("Core", "core/*.f"), Source("ToolMain", "tool/*.pgm"));
        project.Kind = ProjectKind.Library;

        // Act
        var result = Translator.Run(Settings(project), _root);

        // Assert
        result.Succeeded.ShouldBeTrue();
        File.Exists(Path.Combine(_root, "out/Example.All/Program.cs")).ShouldBeFalse();
        Text("out/Example.All/Example.All.csproj").ShouldNotContain("OutputType");
    }

    [Theory]
    [InlineData("Example.Core", "Core", null, "The class Core of the project Example.Core has the name of a part of its namespace Example.Core; give the source a class of another name.")]
    [InlineData("Example.Kit", "Core", "not a class", "The source Core of the project Example.Kit needs a class name; 'not a class' is not one.")]
    [InlineData("Example Kit", "Core", null, "The namespace Example Kit of the project Example Kit is not a namespace; give the project one.")]
    public void Run_WhenANameCannotBeUsed_ShouldRefuseAndNotRenameByItself(string project, string source, string? className, string message)
    {
        // Arrange
        Put("core/core.f", Core);
        var settings = Settings(Project(project, Source(source, "core/*.f")));
        settings.Projects[0].Sources[0].Class = className;

        // Act
        var result = Translator.Run(settings, _root);

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
        settings.Projects[0].Sources[0].Class = "CoreLibrary";

        // Act
        var result = Translator.Run(settings, _root);

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
        settings.Projects[2].References = ["Nowhere"];

        // Act
        var result = Translator.Run(settings, _root);

        // Assert
        result.Succeeded.ShouldBeFalse();
        result.Messages.Select(message => message.Text).ShouldBe(
        [
            "Two sources become the class One.A.",
            "Two projects are named One.",
            "The project One has no sources.",
            "The source B of the project Two names no files.",
            "The project Two refers to Nowhere, which is no other project of the settings.",
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
        var result = Translator.Run(settings, _root);

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
        var result = Translator.Run(settings, _root);

        // Assert
        result.Succeeded.ShouldBeFalse();
        var message = result.Messages.ShouldHaveSingleItem();
        message.File.ShouldNotBeNull().ShouldEndWith("later.f");
        message.Text.ShouldBe("Line 3: ENDFILE cannot be translated yet.");
        Directory.Exists(Path.Combine(_root, "out")).ShouldBeFalse();
    }

    [Fact]
    public void Run_WhenProjectsCallEachOtherBothWays_ShouldRefuseAndSuggestOneProject()
    {
        // Arrange
        Put("a/a.f", "      SUBROUTINE AONE ( N )\n      INTEGER N\n      CALL BONE ( N )\n      END\n");
        Put("b/b.f", "      SUBROUTINE BONE ( N )\n      INTEGER N\n      IF ( N .GT. 0 ) CALL AONE ( N - 1 )\n      END\n");

        // Act
        var apart = Translator.Run(Settings(Project("PA", Source("A", "a/*.f")), Project("PB", Source("B", "b/*.f"))), _root);
        var together = Translator.Run(Settings(Project("PAB", Source("A", "a/*.f"), Source("B", "b/*.f"))), _root);

        // Assert
        apart.Succeeded.ShouldBeFalse();
        apart.Messages.ShouldHaveSingleItem().Text.ShouldStartWith("The project PA refers to itself by way of the projects it refers to");
        together.Succeeded.ShouldBeTrue();
    }

    [Fact]
    public void Run_WhenReferencesAreGiven_ShouldUseThemAndRefuseACallIntoAProjectNotAmongThem()
    {
        // Arrange
        Put("core/core.f", Core);
        Put("tool/tool.pgm", Tool);
        var tool = Project("Example.Tool", Source("ToolMain", "tool/*.pgm"));
        tool.References = [];
        var settings = Settings(Project("Example.Toolkit", Source("Core", "core/*.f")), tool);

        // Act
        var refused = Translator.Run(settings, _root);
        tool.References = ["Example.Toolkit"];
        var accepted = Translator.Run(settings, _root);

        // Assert
        refused.Messages.ShouldHaveSingleItem().Text.ShouldBe("The project Example.Tool calls into Example.Toolkit and does not refer to it.");
        accepted.Succeeded.ShouldBeTrue();
    }

    [Fact]
    public void Run_WhenACallReachesNothing_ShouldWarnAndStillTranslateUnlessTheContextSuppliesIt()
    {
        // Arrange
        Put("core/calls.f", "      SUBROUTINE CALLS ( N )\n      INTEGER N\n      CALL NOWHERE ( N )\n      CALL EXIT ( N )\n      END\n");

        // Act
        var result = Translator.Run(Settings(Project("Example.Toolkit", Source("Core", "core/*.f"))), _root);

        // Assert
        result.Succeeded.ShouldBeTrue();
        result.Messages.ShouldHaveSingleItem().Text.ShouldBe("CALLS calls NOWHERE, which no source of the settings defines.");
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
        var result = Translator.Run(Settings(Project("Example.Toolkit", source)), _root);

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
        var result = Translator.Run(Settings(Project("Example.Toolkit", Source("Core", "core/*.f"))), _root);

        // Assert
        result.Succeeded.ShouldBeTrue();
        File.Exists(Path.Combine(_root, "out/Example.Toolkit/Generated/Core/_CON.g.cs")).ShouldBeTrue();
    }

    [Fact]
    public void Run_WhenAProjectNamesItsOwnFolderAndTheRuntimeIsAProject_ShouldWriteThereAndReferToTheProject()
    {
        // Arrange
        Put("core/core.f", Core);
        var project = Project("Example.Toolkit", Source("Core", "core/*.f"));
        project.Output = "elsewhere/kit";
        var settings = Settings(project);
        settings.Runtime = new RuntimeSettings { Project = "runtime/Runtime.csproj" };

        // Act
        var result = Translator.Run(settings, _root);

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
        settings.Runtime = new RuntimeSettings { OmitPackageVersion = true };

        // Act
        var result = Translator.Run(settings, _root);

        // Assert
        result.Succeeded.ShouldBeTrue();
        var library = Text("out/Example.Toolkit/Example.Toolkit.csproj");
        library.ShouldContain("<PackageReference Include=\"Dracocephalum.Fortrania.Runtime\" />");
        library.ShouldNotContain("Version=");
    }

    [Fact]
    public void Run_WhenThePackageVersionIsOmittedAndGiven_ShouldRefuseTheSettings()
    {
        // Arrange
        Put("core/core.f", Core);
        var settings = Settings(Project("Example.Toolkit", Source("Core", "core/*.f")));
        settings.Runtime = new RuntimeSettings { Package = "1.2.3", OmitPackageVersion = true };

        // Act
        var result = Translator.Run(settings, _root);

        // Assert
        result.Succeeded.ShouldBeFalse();
        result.Messages.ShouldHaveSingleItem().Text.ShouldBe("The settings give the runtime's package the version 1.2.3 and say to write none; it is one or the other.");
        result.Written.ShouldBeEmpty();
    }

    [Fact]
    public void Run_WhenAStepAfterwardsChangedATranslatedFile_ShouldKeepItUntilTheTranslationOfItChanges()
    {
        // Arrange
        Put("core/core.f", Core);
        var settings = Settings(Project("Example.Toolkit", Source("Core", "core/*.f")));
        Translator.Run(settings, _root).Succeeded.ShouldBeTrue();
        var path = "out/Example.Toolkit/Generated/Core/CPUT.g.cs";
        Put(path, Text(path).Replace("TOTAL = TOTAL + N", "changed afterwards", StringComparison.Ordinal) + "// changed afterwards\n");
        var changed = Text(path);

        // Act
        var unchanged = Translator.Run(settings, _root);
        Put("core/core.f", Core.Replace("TOTAL = TOTAL + N", "TOTAL = TOTAL + 2 * N", StringComparison.Ordinal));
        var different = Translator.Run(settings, _root);

        // Assert
        unchanged.Written.ShouldBeEmpty();
        different.Written.Select(Path.GetFileName).ShouldBe(["CPUT.g.cs"]);
        Text(path).ShouldNotBe(changed);
        Text(path).ShouldContain("*total = *total + 2 * *n;");
    }

    [Fact]
    public void Run_WhenATranslatedFileIsDeleted_ShouldWriteItAgain()
    {
        // Arrange
        Put("core/core.f", Core);
        var settings = Settings(Project("Example.Toolkit", Source("Core", "core/*.f")));
        Translator.Run(settings, _root).Succeeded.ShouldBeTrue();
        File.Delete(Path.Combine(_root, "out/Example.Toolkit/Generated/Core/CPUT.g.cs"));
        Put("out/Example.Toolkit/Generated/Core/.fortrania.json", "not a manifest");

        // Act
        var result = Translator.Run(settings, _root);

        // Assert
        result.Succeeded.ShouldBeTrue();
        result.Written.Select(Path.GetFileName).Order(StringComparer.Ordinal).ShouldBe(["CPUT.g.cs", "Core.g.cs"]);
    }

    [Fact]
    public void Run_WhenCommandsAreToRunOnSuccess_ShouldRunEachInTheSettingsFolderAndTellItWhatWasWritten()
    {
        // Arrange
        Put("core/core.f", Core);
        var settings = Settings(Project("Example.Toolkit", Source("Core", "core/*.f")));
        settings.OnSuccess = [Shell("echo %FORTRANIA_OUTPUT%> output.txt & type %FORTRANIA_WRITTEN% > written.txt", "echo \"$FORTRANIA_OUTPUT\" > output.txt; cat \"$FORTRANIA_WRITTEN\" > written.txt")];

        // Act
        var result = Translator.Run(settings, _root);

        // Assert
        result.Messages.ShouldBeEmpty();
        Text("output.txt").Trim().ShouldBe(Path.Combine(_root, "out"));
        File.ReadAllLines(Path.Combine(_root, "written.txt")).Select(Path.GetFileName).Order(StringComparer.Ordinal)
            .ShouldBe(["CPUT.g.cs", "Core.g.cs", "Example.Toolkit.csproj"]);
    }

    [Fact]
    public void Run_WhenACommandFails_ShouldSaySoWithWhatItSaidAndRunNoneAfterIt()
    {
        // Arrange
        Put("core/core.f", Core);
        var settings = Settings(Project("Example.Toolkit", Source("Core", "core/*.f")));
        settings.OnSuccess =
        [
            Shell("echo it went wrong & exit 3", "echo it went wrong; exit 3"),
            Shell("echo ran > after.txt", "echo ran > after.txt"),
        ];

        // Act
        var result = Translator.Run(settings, _root);

        // Assert
        result.Succeeded.ShouldBeFalse();
        var message = result.Messages.ShouldHaveSingleItem();
        message.Text.ShouldContain("ended with status 3");
        message.Text.ShouldContain("it went wrong");
        File.Exists(Path.Combine(_root, "after.txt")).ShouldBeFalse();
        File.Exists(Path.Combine(_root, "out/Example.Toolkit/Generated/Core/CPUT.g.cs")).ShouldBeTrue();
    }

    [Fact]
    public void Run_WhenACommandCannotBeStartedOrNamesNoProgram_ShouldSaySo()
    {
        // Arrange
        Put("core/core.f", Core);
        var missing = Settings(Project("Example.Toolkit", Source("Core", "core/*.f")));
        missing.OnSuccess = [new CommandSettings { Run = ["no-such-program-" + Guid.NewGuid().ToString("N")] }];
        var empty = Settings(Project("Example.Toolkit", Source("Core", "core/*.f")));
        empty.OnSuccess = [new CommandSettings()];

        // Act
        var notStarted = Translator.Run(missing, _root);
        var refused = Translator.Run(empty, _root);

        // Assert
        notStarted.Messages.ShouldHaveSingleItem().Text.ShouldContain("could not be started");
        refused.Messages.ShouldHaveSingleItem().Text.ShouldBe("Command 1 of onSuccess names no program to run.");
        refused.Written.ShouldBeEmpty();
    }

    [Fact]
    public void Run_WhenTheTranslationFails_ShouldRunNoCommand()
    {
        // Arrange
        var settings = Settings(Project("Example.Toolkit", Source("Core", "missing/*.f")));
        settings.OnSuccess = [Shell("echo ran > ran.txt", "echo ran > ran.txt")];

        // Act
        var result = Translator.Run(settings, _root);

        // Assert
        result.Succeeded.ShouldBeFalse();
        File.Exists(Path.Combine(_root, "ran.txt")).ShouldBeFalse();
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
        settings.Variables[name] = new VariableSettings { Path = "third" };

        // Act
        var fromSettings = Translator.Run(settings, _root);
        Environment.SetEnvironmentVariable(name, "second");
        var fromEnvironment = Translator.Run(settings, _root);
        settings.LocalVariables[name] = "first";
        var fromLocal = Translator.Run(settings, _root);
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
        var result = Translator.Run(settings, _root);

        // Assert
        result.Succeeded.ShouldBeFalse();
        result.Messages.ShouldHaveSingleItem().Text.ShouldBe(
            "The variable SourcesNowhere has no value. Give it one in the local settings file, in the environment, or under variables in the settings.");
        Directory.Exists(Path.Combine(_root, "out")).ShouldBeFalse();
    }

    [Fact]
    public void Run_WhenWritten_ShouldNameTheUnitAndItsFileButNoFolderAndRecordTheTranslatorInTheManifest()
    {
        // Arrange
        Put("core/core.f", Core);

        // Act
        Translator.Run(Settings(Project("Example.Toolkit", Source("Core", "core/*.f"))), _root).Succeeded.ShouldBeTrue();

        // Assert
        var file = Text("out/Example.Toolkit/Generated/Core/CPUT.g.cs");
        file.ShouldStartWith("// <auto-generated>\n// Translated from FORTRAN 77 by Fortrania: CPUT, from core.f.\n");
        file.ShouldNotContain(_root);
        Text("out/Example.Toolkit/Generated/Core/.fortrania.json").ShouldContain("\"translator\": \"Fortrania ");
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
        var result = Translator.Run(Settings(Project("Example.Toolkit", Source("Core", "core/*.f"))), _root);

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
                public static double RunOnStack() => Run(WorkingBuffers.Stack).Result;

                public static double RunInBlock() => Run(WorkingBuffers.Block).Result;

                public static long BlockBytesOnStack() => Run(WorkingBuffers.Stack).BlockBytes;

                public static long BlockBytesInBlock() => Run(WorkingBuffers.Block).BlockBytes;

                private static (double Result, long BlockBytes) Run(WorkingBuffers place)
                {
                    using var context = new FortranContext(new FortranContextOptions { WorkingBuffers = place });
                    double result = 0;
                    context.Get<Example.Toolkit.Core>().TOP(&result);
                    return (result, context.Working.BlockBytes);
                }
            }
            """;

        // Act
        var result = Translator.Run(Settings(Project("Example.Toolkit", Source("Core", "core/*.f"))), _root);

        // Assert
        result.Succeeded.ShouldBeTrue();

        // TOP and RUNF one after the other, F at the top of a level of its
        // own and SUMUP after it: 240 bytes for F, then 240 and 12 more.
        result.StackUse.ToDictionary(use => use.Procedure, use => use.Place).ShouldBe(
            new Dictionary<string, long?> { ["TOP"] = 0, ["RUNF"] = 80, ["F"] = 0, ["SUMUP"] = 240 },
            ignoreOrder: true);
        result.WorkingRegions.ShouldBe([new WorkingRegion("Example.Toolkit", 496, 496)]);

        var runf = Text("out/Example.Toolkit/Generated/Core/RUNF.g.cs");
        runf.ShouldContain("byte* _locals = _working.Start + 80;");
        runf.ShouldContain("if (_working.IsOnStack)");
        runf.ShouldContain("byte* _onStack = stackalloc byte[160];");
        runf.ShouldContain("double* w = (double*)_locals;");
        Text("out/Example.Toolkit/Generated/Core/SUMUP.g.cs").ShouldContain("byte* tag = _locals + 240;");
        Text("out/Example.Toolkit/Generated/Core/Core.g.cs")
            .ShouldContain("_working = context.Working.Register(\"Example.Toolkit\", 496, 496, [], WorkingBuffers.Default);");

        var compiled = TranslatedProgram.FromSources(
            Directory.GetFiles(Path.Combine(_root, "out"), "*.cs", SearchOption.AllDirectories).Select(File.ReadAllText).Append(Driver));
        const double Expected = (3 * 465) + 40 + 2 + 10;
        compiled.Run<double>("RunOnStack").ShouldBe(Expected);
        compiled.Run<double>("RunInBlock").ShouldBe(Expected);
        compiled.Run<long>("BlockBytesOnStack").ShouldBe(0);
        compiled.Run<long>("BlockBytesInBlock").ShouldBe(496);
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
        var result = Translator.Run(Settings(Project("Example.Toolkit", Source("Core", "core/*.f"))), _root);

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
        settings.InlineTextConstants = true;

        // Act
        var result = Translator.Run(settings, _root);

        // Assert
        result.Succeeded.ShouldBeTrue();
        var one = Text("out/Example.Toolkit/Generated/Core/ONE.g.cs");
        one.ShouldContain("AddrOf(\"first\"u8), 5);");
        one.ShouldNotContain("_text");
        File.Exists(Path.Combine(_root, "out/Example.Toolkit/Generated/Core/Core.Text.g.cs")).ShouldBeFalse();
    }

    [Fact]
    public void Run_WhenACharacterValueIsTestedAgainstAConstant_ShouldTestForEqualityAndNotOrderTheTwo()
    {
        // Arrange: one byte against one, a name in a longer variable, the
        // constant on the left, a test for difference, and a comparison
        // of two variables, which stays as it is. COUNT adds a power of
        // two for each test that comes out as FORTRAN has it.
        Put("core/tests.f", """
                  INTEGER FUNCTION COUNT ( C, NAME, OTHER )
                  CHARACTER*1 C
                  CHARACTER*(*) NAME, OTHER
                  COUNT = 0
                  IF ( C .EQ. 'U' ) COUNT = COUNT + 1
                  IF ( C .NE. 'X' ) COUNT = COUNT + 2
                  IF ( NAME .EQ. 'J2000' ) COUNT = COUNT + 4
                  IF ( 'B1950' .NE. NAME ) COUNT = COUNT + 8
                  IF ( NAME .EQ. OTHER ) COUNT = COUNT + 16
                  IF ( NAME .LT. 'K' ) COUNT = COUNT + 32
                  IF ( NAME .EQ. 'J2000   X' ) COUNT = COUNT + 64
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
                    return context.Get<Example.Toolkit.Core>().COUNT(AddrOf("U"u8), 1, AddrOf("J2000   "u8), 8, AddrOf("J2000"u8), 5);
                }
            }
            """;

        // Act
        var result = Translator.Run(Settings(Project("Example.Toolkit", Source("Core", "core/*.f"))), _root);

        // Assert
        result.Succeeded.ShouldBeTrue();
        var count = Text("out/Example.Toolkit/Generated/Core/COUNT.g.cs");
        count.ShouldContain("if (*c == 'U')");
        count.ShouldContain("if (*c != 'X')");
        count.ShouldContain("if (IsEqual(name, name_len, _text");
        count.ShouldContain("/* \"J2000\" */, 5))");
        count.ShouldContain("if (!IsEqual(name, name_len, _text");
        count.ShouldContain("if (Compare(name, name_len, other, other_len) == 0)");
        count.ShouldContain("/* \"K\" */, 1) < 0)");

        var compiled = TranslatedProgram.FromSources(
            Directory.GetFiles(Path.Combine(_root, "out"), "*.cs", SearchOption.AllDirectories).Select(File.ReadAllText).Append(Driver));
        compiled.Run<int>("Count").ShouldBe(1 + 2 + 4 + 8 + 16 + 32);
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
        var result = Translator.Run(Settings(Project("Example.Toolkit", Source("Core", "core/*.f"))), _root);

        // Assert
        result.Succeeded.ShouldBeTrue();
        var copy = Text("out/Example.Toolkit/Generated/Core/COPY.g.cs");
        copy.ShouldContain("if (CanMoveElements(a, b, _last1))");
        copy.ShouldContain("i = MoveElements(a, b, _last1);");
        copy.ShouldContain("b[i - 1] = a[i - 1];");
        var fill = Text("out/Example.Toolkit/Generated/Core/SETALL.g.cs");
        fill.ShouldContain("if (_last1 >= LongRun)");
        fill.ShouldContain("i = FillElements(b, _last1, *v);");
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
        var result = Translator.Run(Settings(Project("Example.Toolkit", Source("Core", "core/*.f"))), _root);

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
        var result = Translator.Run(
            Settings(Project("Example.Toolkit", Source("Core", "core/*.f")), Project("Example.Tool", Source("ToolMain", "tool/*.pgm"))),
            _root);

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
        var result = Translator.Run(Settings(Project("Example.Toolkit", Source("Core", "core/*.f"))), _root);

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
        var result = Translator.Run(Settings(Project("Example.Toolkit", Source("Core", "core/*.f"))), _root);

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
        var result = Translator.Run(Settings(Project("Example.Toolkit", Source("Core", "core/*.f"))), _root);

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

    // A command for the system's own shell: the test needs one that is
    // on every machine the tests run on.
    private static CommandSettings Shell(string windows, string other) => new()
    {
        Run = OperatingSystem.IsWindows() ? ["cmd", "/c", windows] : ["/bin/sh", "-c", other],
    };

    private static TranslationSettings Settings(params ProjectSettings[] projects) => new()
    {
        Output = "out",
        Runtime = new RuntimeSettings { Package = "1.2.3" },
        Projects = [.. projects],
    };

    private static ProjectSettings Project(string name, params SourceSettings[] sources) => new() { Name = name, Sources = [.. sources] };

    private static SourceSettings Source(string name, params string[] files) => new() { Name = name, Files = [.. files] };

    private void Put(string path, string text)
    {
        var full = Path.Combine(_root, path);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, text);
    }

    private string Text(string path) => File.ReadAllText(Path.Combine(_root, path));

    private string[] Files(string folder) =>
    [
        .. Directory.GetFiles(Path.Combine(_root, folder), "*", SearchOption.AllDirectories)
            .Select(file => Path.GetRelativePath(_root, file).Replace('\\', '/'))
            .Order(StringComparer.Ordinal),
    ];
}
