using System.Text.Json;
using Dracocephalum.Fortrania.Translation;
using Shouldly;

namespace Dracocephalum.Fortrania.Tests.Translation;

public class TranslationSettingsTests
{
    [Fact]
    public void Parse_WhenTheTextIsASettingsFile_ShouldReadEveryProjectAndSource()
    {
        // Arrange
        const string Json =
            """
            {
              // Where the projects go.
              "output": "src",
              "framework": "net10.0",
              "runtime": { "project": "../runtime/Runtime.csproj" },
              "projects": [
                {
                  "name": "Example.Toolkit",
                  "sources": [
                    { "name": "Core", "files": [ "core/*.f" ], "includes": [ "core/inc" ] },
                    { "name": "Support", "class": "SupportLibrary", "files": [ "support/*.f", "support/extra.for" ] },
                  ],
                },
                {
                  "name": "Example.Tool",
                  "namespace": "Example.Tools",
                  "output": "tools/tool",
                  "kind": "executable",
                  "entry": "TOOL",
                  "references": [ "Example.Toolkit" ],
                  "sources": [ { "name": "ToolMain", "files": [ "tool/*.pgm" ] } ]
                }
              ]
            }
            """;

        // Act
        var settings = TranslationSettings.Parse(Json);

        // Assert
        settings.Output.ShouldBe("src");
        settings.Runtime.Project.ShouldBe("../runtime/Runtime.csproj");
        settings.Runtime.Package.ShouldBeNull();
        settings.Projects.Count.ShouldBe(2);

        var toolkit = settings.Projects[0];
        toolkit.Name.ShouldBe("Example.Toolkit");
        toolkit.Kind.ShouldBeNull();
        toolkit.References.ShouldBeNull();
        toolkit.Sources.Select(source => source.Name).ShouldBe(["Core", "Support"]);
        toolkit.Sources[0].Includes.ShouldBe(["core/inc"]);
        toolkit.Sources[1].Class.ShouldBe("SupportLibrary");
        toolkit.Sources[1].Files.ShouldBe(["support/*.f", "support/extra.for"]);

        var tool = settings.Projects[1];
        tool.Namespace.ShouldBe("Example.Tools");
        tool.Output.ShouldBe("tools/tool");
        tool.Kind.ShouldBe(ProjectKind.Executable);
        tool.Entry.ShouldBe("TOOL");
        tool.References.ShouldBe(["Example.Toolkit"]);
    }

    [Fact]
    public void Parse_WhenNothingButProjectsIsGiven_ShouldLeaveTheRestAsItIsByDefault()
    {
        // Act
        var settings = TranslationSettings.Parse("""{ "projects": [ { "name": "P", "sources": [ { "name": "S", "files": [ "a.f" ] } ] } ] }""");

        // Assert
        settings.Output.ShouldBe(".");
        settings.Framework.ShouldBe("net10.0");
        settings.Runtime.Project.ShouldBeNull();
        settings.Projects[0].Sources[0].Includes.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("""{ "projects": [], "colour": "red" }""")]
    [InlineData("""{ "projects": [ { "name": "P", "sorces": [] } ] }""")]
    [InlineData("""{ "projects": [ { "name": "P", "kind": "plugin" } ] }""")]
    [InlineData("""{ "projects": [ { "name": "P", "sources": [ { "name": "S", "files": "a.f" } ] } ] }""")]
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
        File.WriteAllText(path, """{ "variables": { "Sources": "shared" }, "projects": [] }""");
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
    public void Load_WhenGivenAFile_ShouldReadIt()
    {
        // Arrange
        var path = Path.Combine(AppContext.BaseDirectory, Guid.NewGuid().ToString("N") + ".json");
        File.WriteAllText(path, """{ "output": "there", "projects": [] }""");

        try
        {
            // Act
            var settings = TranslationSettings.Load(path);

            // Assert
            settings.Output.ShouldBe("there");
        }
        finally
        {
            File.Delete(path);
        }
    }
}
