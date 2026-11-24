using System.Text.Json;
using Dracocephalum.Fortrania.Translation;
using Shouldly;

namespace Dracocephalum.Fortrania.Tests.Translation;

public class TranslationSettingsTests
{
    [Fact]
    public void Parse_WhenTheTextIsASettingsFile_ShouldReadTheProjectAndEverySource()
    {
        // Arrange
        const string Json =
            """
            {
              // Where the project goes.
              "name": "Example.Tool",
              "namespace": "Example.Tools",
              "output": "tools/tool",
              "framework": "net10.0",
              "runtime": { "project": "../runtime/Runtime.csproj" },
              "kind": "executable",
              "entry": "TOOL",
              "references": [ "Example.Toolkit" ],
              "sources": [
                { "name": "Core", "files": [ "core/*.f" ], "includes": [ "core/inc" ] },
                { "name": "Support", "class": "SupportLibrary", "files": [ "support/*.f", "support/extra.for" ] },
              ],
            }
            """;

        // Act
        var settings = TranslationSettings.Parse(Json);

        // Assert
        settings.Name.ShouldBe("Example.Tool");
        settings.Namespace.ShouldBe("Example.Tools");
        settings.Output.ShouldBe("tools/tool");
        settings.Runtime.Project.ShouldBe("../runtime/Runtime.csproj");
        settings.Runtime.Package.ShouldBeNull();
        settings.Kind.ShouldBe(ProjectKind.Executable);
        settings.Entry.ShouldBe("TOOL");
        settings.References.ShouldBe(["Example.Toolkit"]);
        settings.Sources.Select(source => source.Name).ShouldBe(["Core", "Support"]);
        settings.Sources[0].Includes.ShouldBe(["core/inc"]);
        settings.Sources[1].Class.ShouldBe("SupportLibrary");
        settings.Sources[1].Files.ShouldBe(["support/*.f", "support/extra.for"]);
        settings.Folder.ShouldBeNull();
    }

    [Fact]
    public void Parse_WhenTheSettingsSayWhenToTranslateAndScaffold_ShouldReadBoth()
    {
        // Act
        var settings = TranslationSettings.Parse("""{ "output": ".", "translate": "never", "scaffold": "always", "sources": [] }""");

        // Assert
        (settings.Translate, settings.Scaffold).ShouldBe((TranslateMode.Never, ScaffoldMode.Always));
    }

    [Fact]
    public void Parse_WhenNothingButSourcesIsGiven_ShouldLeaveTheRestAsItIsByDefault()
    {
        // Act
        var settings = TranslationSettings.Parse("""{ "sources": [ { "name": "S", "files": [ "a.f" ] } ] }""");

        // Assert
        settings.Name.ShouldBeNull();
        settings.Output.ShouldBeNull();
        settings.Translate.ShouldBe(TranslateMode.IfChanged);
        settings.Scaffold.ShouldBe(ScaffoldMode.Once);
        settings.Kind.ShouldBeNull();
        settings.References.ShouldBeNull();
        settings.Framework.ShouldBe("net10.0");
        settings.Runtime.Project.ShouldBeNull();
        settings.Sources[0].Includes.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("""{ "sources": [], "colour": "red" }""")]
    [InlineData("""{ "name": "P", "sorces": [] }""")]
    [InlineData("""{ "name": "P", "kind": "plugin" }""")]
    [InlineData("""{ "name": "P", "sources": [ { "name": "S", "files": "a.f" } ] }""")]
    [InlineData("""{ "projects": [ { "name": "P", "sources": [] } ] }""")]
    [InlineData("""{ "solution": "All.slnx" }""")]
    [InlineData("not json")]
    [InlineData("null")]
    public void Parse_WhenTheTextIsNotSettings_ShouldThrow(string json)
    {
        // Act & Assert
        Should.Throw<JsonException>(() => TranslationSettings.Parse(json));
    }

    [Fact]
    public void Load_WhenALocalSettingsFileIsBesideIt_ShouldReadItsVariables()
    {
        // Arrange
        var folder = Path.Combine(AppContext.BaseDirectory, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, "example.fortrania.json");
        File.WriteAllText(path, """{ "variables": { "Sources": "shared" }, "sources": [] }""");
        File.WriteAllText(Path.Combine(folder, "example.fortrania.local.json"), """{ "variables": { "sources": "mine" } }""");

        try
        {
            // Act
            var settings = TranslationSettings.Load(path);

            // Assert
            settings.Variables["SOURCES"].Path.ShouldBe("shared");
            settings.LocalVariables["Sources"].ShouldBe("mine");
            TranslationSettings.LocalFileOf(path).ShouldBe(Path.Combine(folder, "example.fortrania.local.json"));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void Load_WhenGivenAFile_ShouldReadItAndTakeItsFolderAsTheSettings()
    {
        // Arrange
        var path = Path.Combine(AppContext.BaseDirectory, Guid.NewGuid().ToString("N") + ".json");
        File.WriteAllText(path, """{ "output": "there", "sources": [] }""");

        try
        {
            // Act
            var settings = TranslationSettings.Load(path);

            // Assert
            settings.Output.ShouldBe("there");
            settings.Folder.ShouldBe(Path.GetDirectoryName(path));
        }
        finally
        {
            File.Delete(path);
        }
    }
}
