using System.Text.Json;
using Dracocephalum.Fortrania.Translation;
using Shouldly;

namespace Dracocephalum.Fortrania.Tests.Translation;

public class TranslationSettingsTests
{
    [Fact]
    public void Parse_WhenTheTextIsASettingsFile_ShouldReadEverySource()
    {
        // Arrange
        const string Json =
            """
            {
              // Where the translation goes.
              "namespace": "Example.Tools",
              "output": "tools/tool",
              "sources": [
                { "name": "Core", "files": [ "core/*.f" ], "includes": [ "core/inc" ] },
                { "name": "Support", "class": "SupportLibrary", "files": [ "support/*.f", "support/extra.for" ] },
              ],
            }
            """;

        // Act
        var settings = TranslationSettings.Parse(Json);

        // Assert
        settings.Namespace.ShouldBe("Example.Tools");
        settings.Output.ShouldBe("tools/tool");
        settings.Sources.Select(source => source.Name).ShouldBe(["Core", "Support"]);
        settings.Sources[0].Includes.ShouldBe(["core/inc"]);
        settings.Sources[1].Class.ShouldBe("SupportLibrary");
        settings.Sources[1].Files.ShouldBe(["support/*.f", "support/extra.for"]);
        settings.Folder.ShouldBeNull();
    }

    [Fact]
    public void Parse_WhenNothingButSourcesIsGiven_ShouldLeaveTheRestAsItIsByDefault()
    {
        // Act
        var settings = TranslationSettings.Parse("""{ "sources": [ { "name": "S", "files": [ "a.f" ] } ] }""");

        // Assert
        settings.Output.ShouldBeNull();
        settings.Sources[0].Includes.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("""{ "sources": [], "colour": "red" }""")]
    [InlineData("""{ "output": ".", "sorces": [] }""")]
    [InlineData("""{ "name": "P", "sources": [] }""")]
    [InlineData("""{ "output": ".", "translate": false }""")]
    [InlineData("""{ "output": ".", "sources": [ { "name": "S", "files": "a.f" } ] }""")]
    [InlineData("""{ "projects": [ { "name": "P", "sources": [] } ] }""")]
    [InlineData("""{ "solution": "All.slnx" }""")]
    [InlineData("""{ "output": ".", "scaffold": "once" }""")]
    [InlineData("""{ "output": ".", "kind": "executable" }""")]
    [InlineData("""{ "output": ".", "entry": "MAIN" }""")]
    [InlineData("""{ "output": ".", "framework": "net10.0" }""")]
    [InlineData("""{ "output": ".", "runtime": { "package": "1.2.3" } }""")]
    [InlineData("""{ "output": ".", "references": [ "Q" ] }""")]
    [InlineData("""{ "output": ".", "properties": { "A": "B" } }""")]
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
