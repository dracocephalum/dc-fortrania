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
            "out/Example.Tool/Generated/ToolMain/TOOL.g.cs",
            "out/Example.Tool/Generated/ToolMain/ToolMain.g.cs",
            "out/Example.Tool/Program.cs",
            "out/Example.Toolkit/Example.Toolkit.csproj",
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
