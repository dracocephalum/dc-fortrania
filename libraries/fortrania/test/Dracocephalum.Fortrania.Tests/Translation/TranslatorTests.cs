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
        settings.Variables[name] = "third";

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
