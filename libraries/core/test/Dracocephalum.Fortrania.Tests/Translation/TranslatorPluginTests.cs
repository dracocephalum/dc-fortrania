using Dracocephalum.Fortrania.Plugins;
using Dracocephalum.Fortrania.Translation;
using Shouldly;

namespace Dracocephalum.Fortrania.Tests.Translation;

public sealed class TranslatorPluginTests : IDisposable
{
    private const string Core =
        """
              SUBROUTINE CPUT ( N )
              INTEGER N, TOTAL
              SAVE TOTAL
              TOTAL = TOTAL + N
              END

              SUBROUTINE CGET ( N )
              INTEGER N
              N = 0
              END
        """;

    private static readonly string[] Points =
    [
        nameof(ITranslationPlugin.BeforeRead),
        nameof(ITranslationPlugin.AfterRead),
        nameof(ITranslationPlugin.BeforeAnalysis),
        nameof(ITranslationPlugin.AfterAnalysis),
        nameof(ITranslationPlugin.BeforeWrite),
        nameof(ITranslationPlugin.AfterWrite),
    ];

    private readonly string _root = Path.Combine(AppContext.BaseDirectory, "plugins-" + Guid.NewGuid().ToString("N"));
    private readonly List<string> _calls = [];

    public TranslatorPluginTests()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, "core.f"), Core);
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public void Run_WhenPluginsAreGiven_ShouldCallEachAtEveryPointInTheirOrder()
    {
        // Act
        var result = Translate(new Plugin("first", _calls), new Plugin("second", _calls));

        // Assert
        result.Succeeded.ShouldBeTrue(string.Join('\n', result.Messages));
        _calls.ShouldBe(Points.SelectMany(point => new[] { "first:" + point, "second:" + point }));
    }

    [Fact]
    public void Run_WhenPluginsGiveAnOrder_ShouldCallThemInItAndThoseOfTheSameOrderAsGiven()
    {
        // Act
        var result = Translate(
            new Plugin("late", _calls, order: 1),
            new Plugin("plain", _calls),
            new Plugin("early", _calls, order: -1),
            new Plugin("plain too", _calls));

        // Assert
        result.Succeeded.ShouldBeTrue(string.Join('\n', result.Messages));
        _calls.Take(4).ShouldBe(["early:BeforeRead", "plain:BeforeRead", "plain too:BeforeRead", "late:BeforeRead"]);
    }

    [Fact]
    public void Run_WhenTheSettingsAndTheOptionsNameAnAssemblyOfPlugins_ShouldHaveThemTakePartOnceAndCountItAsRead()
    {
        // Arrange: this assembly, whose one public plugin marks every file,
        // named twice by the settings and once by the options.
        var assembly = typeof(MarkingPlugin).Assembly.Location;
        var project = Project();
        project.Settings[0].Plugins = [assembly, Path.GetRelativePath(_root, assembly)];

        // Act
        var result = Translator.Run(project, new TranslationOptions { PluginAssemblies = [assembly] });

        // Assert
        result.Succeeded.ShouldBeTrue(string.Join('\n', result.Messages));
        var text = File.ReadAllText(Path.Combine(_root, "out", "Generated", "Core", "CPUT.g.cs"));
        text.ShouldEndWith(MarkingPlugin.Mark + "\n");
        text.Split(MarkingPlugin.Mark).Length.ShouldBe(2);
        result.Read.ShouldContain(assembly);
    }

    [Fact]
    public void Run_WhenTheSettingsNameAPluginAssemblyThatIsNotThere_ShouldSaySoAndWriteNothing()
    {
        // Arrange
        var project = Project();
        project.Settings[0].Plugins = ["plugins/missing.dll"];

        // Act
        var result = Translator.Run(project, new TranslationOptions());

        // Assert
        result.Messages.ShouldHaveSingleItem().Text.ShouldBe($"There is no plugin assembly {Path.Combine(_root, "plugins", "missing.dll")}.");
        Directory.Exists(Path.Combine(_root, "out")).ShouldBeFalse();
    }

    [Fact]
    public void Run_WhenBeforeWriting_ShouldGiveEachFileWithWhereItGoesAndWhatItWasMadeFrom()
    {
        // Arrange
        IReadOnlyList<GeneratedFile> files = [];
        var plugin = new Plugin("files", _calls, (point, context) => files = point == nameof(ITranslationPlugin.BeforeWrite) ? context.GeneratedFiles : files);

        // Act
        Translate(plugin).Succeeded.ShouldBeTrue();

        // Assert
        var cput = files.Single(file => file.Unit == "CPUT");
        (cput.Project, cput.Source, Path.GetFileName(cput.SourceFile)).ShouldBe(("Example.Toolkit", "Core", "core.f"));
        cput.Path.ShouldBe(Path.Combine(_root, "out", "Generated", "Core", "CPUT.g.cs"));
        cput.Text.ShouldBe(File.ReadAllText(cput.Path));
        var library = files.Single(file => Path.GetFileName(file.Path) == "Core.g.cs");
        (library.Unit, library.SourceFile).ShouldBe((null, null));
    }

    [Fact]
    public void Run_WhenAPluginChangesAFileBeforeItIsWritten_ShouldWriteItAsChangedAndAgainOnlyWhenTheChangeDoes()
    {
        // Arrange: each run marks CPUT with a word of its own.
        TranslationResult Mark(string word) => Translate(new Plugin("mark", _calls, (point, context) =>
        {
            if (point == nameof(ITranslationPlugin.BeforeWrite))
            {
                var file = context.GeneratedFiles.Single(file => file.Unit == "CPUT");
                file.Text += $"// {word}\n";
            }
        }));
        Mark("one").Succeeded.ShouldBeTrue();

        // Act
        var changed = Mark("two");
        var same = Mark("two");

        // Assert: what was written is what tells whether a file changed,
        // so a change of the plugin's needs no --force.
        var cput = Path.Combine(_root, "out", "Generated", "Core", "CPUT.g.cs");
        File.ReadAllText(cput).ShouldEndWith("// two\n");
        changed.Written.ShouldBe([cput]);
        same.Written.ShouldBeEmpty();
    }

    [Fact]
    public void Run_WhenAPluginReportsAnErrorBeforeWriting_ShouldWriteNothingAndCallNoOtherPluginAfterIt()
    {
        // Arrange
        var failing = new Plugin("failing", _calls, (point, context) =>
        {
            if (point == nameof(ITranslationPlugin.BeforeWrite))
            {
                context.Error("CPUT is not to be translated.", context.GeneratedFiles[0].SourceFile, 1);
            }
        });

        // Act
        var result = Translate(failing, new Plugin("later", _calls));

        // Assert
        result.Succeeded.ShouldBeFalse();
        result.Messages.ShouldHaveSingleItem().ShouldBe(new TranslationMessage(true, Path.Combine(_root, "core.f"), 1, "CPUT is not to be translated."));
        _calls.ShouldNotContain("later:" + nameof(ITranslationPlugin.BeforeWrite));
        _calls.ShouldNotContain("failing:" + nameof(ITranslationPlugin.AfterWrite));
        Directory.Exists(Path.Combine(_root, "out")).ShouldBeFalse();
    }

    [Fact]
    public void Run_WhenAPluginThrows_ShouldFailWithAnErrorNamingItAndThePoint()
    {
        // Arrange
        var throwing = new Plugin("throwing", _calls, (point, _) =>
        {
            if (point == nameof(ITranslationPlugin.AfterRead))
            {
                throw new InvalidOperationException("Nothing to read.");
            }
        });

        // Act
        var result = Translate(throwing);

        // Assert
        result.Succeeded.ShouldBeFalse();
        result.Messages.ShouldHaveSingleItem().Text.ShouldBe($"The plugin {typeof(Plugin).FullName} failed in AfterRead: Nothing to read.");
        _calls.ShouldNotContain("throwing:" + nameof(ITranslationPlugin.BeforeAnalysis));
        Directory.Exists(Path.Combine(_root, "out")).ShouldBeFalse();
    }

    [Fact]
    public void Run_WhenAPluginTakesAwayTheLineThatSaysAFileWasGenerated_ShouldFailAndWriteNothing()
    {
        // Arrange
        var plugin = new Plugin("header", _calls, (point, context) =>
        {
            if (point == nameof(ITranslationPlugin.BeforeWrite))
            {
                context.GeneratedFiles[0].Text = "// Mine.\n" + context.GeneratedFiles[0].Text;
            }
        });

        // Act
        var result = Translate(plugin);

        // Assert
        result.Succeeded.ShouldBeFalse();
        result.Messages.ShouldHaveSingleItem().Text.ShouldContain("starts with the line // <auto-generated>");
        Directory.Exists(Path.Combine(_root, "out")).ShouldBeFalse();
    }

    [Fact]
    public void Run_WhenWritten_ShouldGiveThePluginsWhatWasDoneAndKeepTheirWarnings()
    {
        // Arrange
        TranslationResult? seen = null;
        var plugin = new Plugin("report", _calls, (point, context) =>
        {
            if (point == nameof(ITranslationPlugin.AfterWrite))
            {
                seen = context.Result;
                context.Warning("Reported.");
            }
        });

        // Act
        var result = Translate(plugin);

        // Assert
        result.Succeeded.ShouldBeTrue();
        seen.ShouldNotBeNull().Written.Count.ShouldBe(result.Written.Count);
        seen.Units.ShouldBe(2);
        result.Messages.ShouldHaveSingleItem().ShouldBe(new TranslationMessage(false, null, 0, "Reported."));
    }

    private TranslationResult Translate(params ITranslationPlugin[] plugins) => Translator.Run(Project(), new TranslationOptions { Plugins = plugins });

    private TranslationProject Project() => new()
    {
        Name = "Example.Toolkit",
        Settings = [new TranslationSettings { Folder = _root, Output = "out", Sources = [new SourceSettings { Name = "Core", Files = [new FileSettings { Paths = ["*.f"] }] }] }],
        Summary = Path.Combine(_root, "out", ProjectSummary.GetFileName("Example.Toolkit")),
    };

    // Records each call it is given by the name it was made, and hands it
    // on to what the test does at that point.
    private sealed class Plugin(string name, List<string> calls, Action<string, TranslationContext>? handler = null, int order = 0) : ITranslationPlugin
    {
        public int Order => order;

        public void BeforeRead(TranslationContext context) => Record(nameof(BeforeRead), context);

        public void AfterRead(TranslationContext context) => Record(nameof(AfterRead), context);

        public void BeforeAnalysis(TranslationContext context) => Record(nameof(BeforeAnalysis), context);

        public void AfterAnalysis(TranslationContext context) => Record(nameof(AfterAnalysis), context);

        public void BeforeWrite(TranslationContext context) => Record(nameof(BeforeWrite), context);

        public void AfterWrite(TranslationContext context) => Record(nameof(AfterWrite), context);

        private void Record(string point, TranslationContext context)
        {
            calls.Add($"{name}:{point}");
            handler?.Invoke(point, context);
        }
    }
}
