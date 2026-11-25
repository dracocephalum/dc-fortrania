using System.Formats.Tar;
using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using Dracocephalum.Fortrania.Translation;
using Shouldly;

namespace Dracocephalum.Fortrania.Tests.Translation;

public sealed class TranslatorDownloadTests : IDisposable
{
    private const string Core =
        """
              SUBROUTINE CPUT ( N )
              INTEGER N, TOTAL
              SAVE TOTAL
              TOTAL = TOTAL + N
              END
        """;

    private readonly string _root = Path.Combine(AppContext.BaseDirectory, "download-" + Guid.NewGuid().ToString("N"));

    public TranslatorDownloadTests() => Directory.CreateDirectory(_root);

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Theory]
    [InlineData("toolkit.tar")]
    [InlineData("toolkit.tar.gz")]
    [InlineData("toolkit.tgz")]
    [InlineData("toolkit.zip")]
    public void Run_WhenTheFolderOfAVariableIsNotThere_ShouldDownloadAndExpandItAndTranslateFromIt(string name)
    {
        // Arrange
        var archive = MakeArchive(name, ("toolkit/src/core/core.f", Core), ("toolkit/README", "read me"));
        var server = new FakeServer(archive);
        var settings = Settings(Variable(name, archive, strip: 1));

        // Act
        var result = Translator.Run(settings, new TranslationOptions { HttpHandler = server });

        // Assert
        result.Messages.ShouldBeEmpty();
        server.Requests.ShouldBe(1);
        server.UserAgent.ShouldBe("Fortrania");
        File.ReadAllText(Path.Combine(_root, "sources/README")).ShouldBe("read me");
        File.Exists(Path.Combine(_root, "out/Example.Toolkit/Generated/Core/CPUT.g.cs")).ShouldBeTrue();
        Directory.GetFileSystemEntries(_root).Select(Path.GetFileName).Order(StringComparer.Ordinal).ShouldBe(["out", "sources"]);
    }

    [Fact]
    public void Run_WhenTheFolderIsThere_ShouldDownloadNothing()
    {
        // Arrange
        Put("sources/src/core/core.f", Core);
        var server = new FakeServer([]);
        var settings = Settings(Variable("toolkit.tar", [], strip: 1));

        // Act
        var result = Translator.Run(settings, new TranslationOptions { HttpHandler = server });

        // Assert
        result.Messages.ShouldBeEmpty();
        server.Requests.ShouldBe(0);
    }

    [Fact]
    public void Run_WhenTheVariableHasAnotherValueLocally_ShouldUseThatAsItIsAndDownloadNothing()
    {
        // Arrange
        Put("mine/src/core/core.f", Core);
        var server = new FakeServer([]);
        var settings = Settings(Variable("toolkit.tar", [], strip: 1));
        settings.LocalVariables["Sources"] = "mine";

        // Act
        var result = Translator.Run(settings, new TranslationOptions { HttpHandler = server });

        // Assert
        result.Messages.ShouldBeEmpty();
        server.Requests.ShouldBe(0);
        Directory.Exists(Path.Combine(_root, "sources")).ShouldBeFalse();
    }

    [Fact]
    public void Run_WhenNoHashIsGiven_ShouldRefuseToDownloadAndSaySo()
    {
        // Arrange
        var server = new FakeServer(MakeArchive("toolkit.tar", ("src/core/core.f", Core)));
        var variable = Variable("toolkit.tar", [], strip: 0);
        variable.Sha256 = null;

        // Act
        var result = Translator.Run(Settings(variable), new TranslationOptions { HttpHandler = server });

        // Assert
        result.Succeeded.ShouldBeFalse();
        result.Messages.ShouldHaveSingleItem().Text.ShouldBe(
            "The variable Sources says where to download from and not what the archive's SHA-256 hash is; it is not downloaded without one.");
        server.Requests.ShouldBe(0);
    }

    [Fact]
    public void Run_WhenTheArchivesHashDiffers_ShouldRefuseItAndLeaveNothingBehind()
    {
        // Arrange
        var archive = MakeArchive("toolkit.tar", ("src/core/core.f", Core));
        var server = new FakeServer(archive);
        var settings = Settings(Variable("toolkit.tar", MakeArchive("toolkit.tar", ("src/core/other.f", Core)), strip: 0));

        // Act
        var result = Translator.Run(settings, new TranslationOptions { HttpHandler = server });

        // Assert
        result.Succeeded.ShouldBeFalse();
        result.Messages.ShouldHaveSingleItem().Text.ShouldContain($"has the SHA-256 hash {Convert.ToHexStringLower(SHA256.HashData(archive))} and not ");
        Directory.GetFileSystemEntries(_root).ShouldBeEmpty();
    }

    [Theory]
    [InlineData("../outside.f")]
    [InlineData("src/../../outside.f")]
    public void Run_WhenAnEntryWouldBeWrittenOutsideTheFolder_ShouldRefuseTheArchive(string entry)
    {
        // Arrange
        var archive = MakeArchive("toolkit.tar", ("src/core/core.f", Core), (entry, "escaped"));
        var server = new FakeServer(archive);

        // Act
        var result = Translator.Run(Settings(Variable("toolkit.tar", archive, strip: 0)), new TranslationOptions { HttpHandler = server });

        // Assert
        result.Succeeded.ShouldBeFalse();
        result.Messages.ShouldHaveSingleItem().Text.ShouldContain("would be written outside the folder");
        File.Exists(Path.Combine(_root, "outside.f")).ShouldBeFalse();
        Directory.GetFileSystemEntries(_root).ShouldBeEmpty();
    }

    [Fact]
    public void Run_WhenTheArchiveIsOfAKindThatCannotBeExpanded_ShouldSaySo()
    {
        // Arrange
        var server = new FakeServer([1, 2, 3]);

        // Act
        var result = Translator.Run(Settings(Variable("toolkit.tar.Z", [1, 2, 3], strip: 0)), new TranslationOptions { HttpHandler = server });

        // Assert
        result.Messages.ShouldHaveSingleItem().Text.ShouldContain("is of a kind that cannot be expanded");
        server.Requests.ShouldBe(0);
    }

    [Fact]
    public void Run_WhenTheServerRefuses_ShouldSaySoAndLeaveNothingBehind()
    {
        // Arrange
        var server = new FakeServer([], HttpStatusCode.NotFound);

        // Act
        var result = Translator.Run(Settings(Variable("toolkit.tar", [], strip: 0)), new TranslationOptions { HttpHandler = server });

        // Assert
        result.Messages.ShouldHaveSingleItem().Text.ShouldContain("could not be fetched");
        Directory.GetFileSystemEntries(_root).ShouldBeEmpty();
    }

    [Fact]
    public void Run_WhenForced_ShouldWriteEveryFileAgain()
    {
        // Arrange
        Put("sources/src/core/core.f", Core);
        var settings = Settings(new VariableSettings { Path = "sources" });
        Translator.Run(settings).Succeeded.ShouldBeTrue();

        // Act
        var unforced = Translator.Run(settings);
        var forced = Translator.Run(settings, new TranslationOptions { Force = true });

        // Assert
        unforced.Written.ShouldBeEmpty();
        forced.Written.Select(Path.GetFileName).Order(StringComparer.Ordinal).ShouldBe(["CPUT.g.cs", "Core.g.cs"]);
    }

    [Fact]
    public void Parse_WhenAVariableIsAStringOrAnObject_ShouldReadBoth()
    {
        // Act
        var settings = TranslationSettings.Parse(
            """
            {
              "variables": {
                "Plain": "somewhere",
                "Fetched": { "path": "sources", "url": "https://example.org/toolkit.tar", "sha256": "ab12", "strip": 1 }
              },
              "sources": []
            }
            """);

        // Assert
        settings.Variables["plain"].Path.ShouldBe("somewhere");
        settings.Variables["plain"].Url.ShouldBeNull();
        var fetched = settings.Variables["fetched"];
        (fetched.Path, fetched.Url?.ToString(), fetched.Sha256, fetched.Strip).ShouldBe(("sources", "https://example.org/toolkit.tar", "ab12", 1));
    }

    [Fact]
    public void Restore_WhenTheSourcesAreNotThere_ShouldFetchThemAndTranslateNothing()
    {
        // Arrange
        var archive = MakeArchive("toolkit.tar", ("toolkit/src/core/core.f", Core));
        var server = new FakeServer(archive);
        var settings = Settings(Variable("toolkit.tar", archive, strip: 1));

        // Act
        var result = Translator.Restore([settings], new TranslationOptions { HttpHandler = server }, onlyWhereNotTranslated: false);

        // Assert
        result.Succeeded.ShouldBeTrue(string.Join('\n', result.Messages));
        server.Requests.ShouldBe(1);
        result.Fetched.ShouldBe([Path.Combine(_root, "sources")]);
        result.Written.ShouldBeEmpty();
        Directory.Exists(Path.Combine(_root, "out")).ShouldBeFalse();
    }

    [Fact]
    public void Restore_WhenOnlyWhereNotTranslatedAndThereIsATranslation_ShouldFetchNothing()
    {
        // Arrange: translated once from a download, then the sources go.
        var archive = MakeArchive("toolkit.tar", ("toolkit/src/core/core.f", Core));
        var server = new FakeServer(archive);
        var settings = Settings(Variable("toolkit.tar", archive, strip: 1));
        Translator.Run(settings, new TranslationOptions { HttpHandler = server }).Succeeded.ShouldBeTrue();
        Directory.Delete(Path.Combine(_root, "sources"), recursive: true);

        // Act
        var needed = Translator.Restore([settings], new TranslationOptions { HttpHandler = server }, onlyWhereNotTranslated: true);
        var offline = Translator.Run(settings, new TranslationOptions { HttpHandler = server, Offline = true });

        // Assert: neither reaches the server again, and offline the
        // project is read from what was translated.
        needed.Succeeded.ShouldBeTrue();
        offline.Succeeded.ShouldBeTrue(string.Join('\n', offline.Messages));
        server.Requests.ShouldBe(1);
        offline.Units.ShouldBe(0);
    }

    [Fact]
    public void Run_WhenAnotherFetchOfTheSameSourcesFinishesFirst_ShouldTakeItsFolderAndSucceed()
    {
        // Arrange: while this download is under way, the folder appears,
        // as another build's fetch of the same archive puts it there.
        var archive = MakeArchive("toolkit.tar", ("toolkit/src/core/core.f", Core));
        var server = new FakeServer(archive)
        {
            WhileSending = () =>
            {
                Directory.CreateDirectory(Path.Combine(_root, "sources/src/core"));
                File.WriteAllText(Path.Combine(_root, "sources/src/core/core.f"), Core);
            },
        };
        var settings = Settings(Variable("toolkit.tar", archive, strip: 1));

        // Act
        var result = Translator.Run(settings, new TranslationOptions { HttpHandler = server });

        // Assert
        result.Succeeded.ShouldBeTrue(string.Join('\n', result.Messages));
        Directory.GetFileSystemEntries(_root).Select(Path.GetFileName).Order(StringComparer.Ordinal).ShouldBe(["out", "sources"]);
    }

    [Fact]
    public void Run_WhenTheTranslationWhoseSourcesAreToBeFetchedIsNotToBeMade_ShouldFetchNothing()
    {
        // Arrange: a second translation of the run, made, with sources of
        // its own on disk; the first, whose sources are to be downloaded,
        // says not to translate it and has no summary.
        var archive = MakeArchive("toolkit.tar", ("toolkit/src/core/core.f", Core));
        var server = new FakeServer(archive);
        var settings = Settings(Variable("toolkit.tar", archive, strip: 1));
        settings.Translate = false;
        var mine = new TranslationSettings
        {
            Folder = _root,
            Name = "Example.Mine",
            Output = "out/Example.Mine",
            Sources = [new SourceSettings { Name = "Own", Files = ["own/*.f"] }],
        };
        Put("own/own.f", "      SUBROUTINE OWN ( N )\n      INTEGER N\n      N = 1\n      END\n");

        // Act
        var result = Translator.Run([settings, mine], new TranslationOptions { HttpHandler = server });

        // Assert: nothing is fetched for it, and with no summary to read it
        // from the run says so.
        server.Requests.ShouldBe(0);
        Directory.Exists(Path.Combine(_root, "sources")).ShouldBeFalse();
        result.Succeeded.ShouldBeFalse();
        result.Messages.ShouldHaveSingleItem().Text.ShouldContain("say not to translate it, and it has no summary");
    }

    private static VariableSettings Variable(string name, byte[] archive, int strip) => new()
    {
        Path = "sources",
        Url = new Uri("https://example.org/packages/" + name),
        Sha256 = Convert.ToHexString(SHA256.HashData(archive)),
        Strip = strip,
    };

    private TranslationSettings Settings(VariableSettings sources)
    {
        var settings = new TranslationSettings
        {
            Folder = _root,
            Name = "Example.Toolkit",
            Output = "out/Example.Toolkit",
            Sources = [new SourceSettings { Name = "Core", Files = ["$(Sources)/src/core/*.f"] }],
        };
        settings.Variables["Sources"] = sources;
        return settings;
    }

    // An archive of the kind its name says, holding the files given.
    private static byte[] MakeArchive(string name, params (string Path, string Text)[] files)
    {
        using var stream = new MemoryStream();
        if (name.EndsWith(".zip", StringComparison.Ordinal))
        {
            using var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true);
            foreach (var (path, text) in files)
            {
                using var writer = new StreamWriter(zip.CreateEntry(path).Open());
                writer.Write(text);
            }
        }
        else
        {
            using var compressed = name.EndsWith(".tar", StringComparison.Ordinal) ? stream : (Stream)new GZipStream(stream, CompressionLevel.Fastest, leaveOpen: true);
            using (var tar = new TarWriter(compressed, leaveOpen: true))
            {
                foreach (var (path, text) in files)
                {
                    tar.WriteEntry(new PaxTarEntry(TarEntryType.RegularFile, path) { DataStream = new MemoryStream(Encoding.UTF8.GetBytes(text)) });
                }
            }
        }

        return stream.ToArray();
    }

    private void Put(string path, string text)
    {
        var full = Path.Combine(_root, path);
        Directory.CreateDirectory(Path.GetDirectoryName(full) ?? _root);
        File.WriteAllText(full, text);
    }

    // Answers every request with the archive, or with a refusal.
    private sealed class FakeServer(byte[] archive, HttpStatusCode status = HttpStatusCode.OK) : HttpMessageHandler
    {
        public int Requests { get; private set; }

        public string? UserAgent { get; private set; }

        // Done as the answer is given, for what happens elsewhere meanwhile.
        public Action? WhileSending { get; init; }

        protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests++;
            WhileSending?.Invoke();
            UserAgent = request.Headers.UserAgent.ToString();
            return new HttpResponseMessage(status) { Content = new ByteArrayContent(archive) };
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(Send(request, cancellationToken));
    }
}
