using System.Formats.Tar;
using System.IO.Compression;
using System.Security.Cryptography;

namespace Dracocephalum.Fortrania.Translation;

/// <summary>
/// Fills a folder from an archive on the network: downloads it, checks its
/// hash, and expands it. Nothing in the archive is run, and nothing is
/// written outside the folder: an entry whose path would leave it is
/// refused, and links are passed over. The folder appears whole or not at
/// all, since everything is done beside it and moved into place at the end.
/// </summary>
internal sealed class SourceFetcher(TranslationOptions options)
{
    private const string ProductName = "Fortrania";

    /// <summary>Fills a folder that is not there.</summary>
    /// <param name="variable">What to fill it from.</param>
    /// <param name="folder">The folder, which does not exist.</param>
    /// <returns>Nothing when the folder was filled, and what went wrong otherwise.</returns>
    public string? Fill(VariableSettings variable, string folder)
    {
        if (variable.Url is not { } url || variable.Sha256 is not { } expected)
        {
            return "A variable to download has to give both a url and a sha256.";
        }

        var format = DetectFormat(url);
        if (format is null)
        {
            return $"The archive {url} is of a kind that cannot be expanded: a .zip, .tar, .tar.gz or .tgz can.";
        }

        var parent = Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(folder)) ?? folder;
        Directory.CreateDirectory(parent);
        var archive = Path.Combine(parent, "." + Path.GetFileName(folder) + ".download");
        var partial = Path.Combine(parent, "." + Path.GetFileName(folder) + ".partial-" + Guid.NewGuid().ToString("N"));

        try
        {
            var actual = Download(url, archive);
            if (!string.Equals(actual, expected.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return $"The archive {url} has the SHA-256 hash {actual} and not {expected.Trim()}; it was not expanded.";
            }

            Directory.CreateDirectory(partial);
            using (var stream = File.OpenRead(archive))
            {
                if (ExpandInto(stream, format, partial, variable.Strip) is { } refused)
                {
                    return $"The archive {url} cannot be expanded: {refused}";
                }
            }

            Directory.Move(partial, folder);
            return null;
        }
        catch (Exception exception) when (exception is HttpRequestException or OperationCanceledException or IOException or InvalidDataException or UnauthorizedAccessException)
        {
            return $"The archive {url} could not be fetched: {exception.Message}";
        }
        finally
        {
            File.Delete(archive);
            if (Directory.Exists(partial))
            {
                Directory.Delete(partial, recursive: true);
            }
        }
    }

    private static string? DetectFormat(Uri url)
    {
        var name = url.AbsolutePath.ToUpperInvariant();
        if (name.EndsWith(".ZIP", StringComparison.Ordinal))
        {
            return "zip";
        }

        if (name.EndsWith(".TAR.GZ", StringComparison.Ordinal) || name.EndsWith(".TGZ", StringComparison.Ordinal))
        {
            return "tar.gz";
        }

        return name.EndsWith(".TAR", StringComparison.Ordinal) ? "tar" : null;
    }

    // Writes the archive to a file and gives the hash of what was written.
    private string Download(Uri url, string archive)
    {
        using var client = options.HttpHandler is { } handler
            ? new HttpClient(handler, disposeHandler: false)
            : new HttpClient();
        client.Timeout = options.DownloadTimeout;
        client.DefaultRequestHeaders.UserAgent.ParseAdd(ProductName);

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        using var response = client.Send(request, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();

        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        using var source = response.Content.ReadAsStream();
        using var target = File.Create(archive);
        var buffer = new byte[81920];
        int read;
        while ((read = source.Read(buffer)) > 0)
        {
            hash.AppendData(buffer, 0, read);
            target.Write(buffer, 0, read);
        }

        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }

    // Expands an archive into a folder; gives what was wrong with it, or
    // nothing.
    private static string? ExpandInto(Stream stream, string format, string folder, int strip)
    {
        if (format == "zip")
        {
            using var zip = new ZipArchive(stream, ZipArchiveMode.Read);
            foreach (var entry in zip.Entries)
            {
                var isFolder = entry.FullName.EndsWith('/');
                if (ResolveTarget(entry.FullName, folder, strip, out var target) is { } refused)
                {
                    return refused;
                }

                if (target is null)
                {
                    continue;
                }

                if (isFolder)
                {
                    Directory.CreateDirectory(target);
                    continue;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(target) ?? folder);
                entry.ExtractToFile(target, overwrite: true);
            }

            return null;
        }

        using var decompressed = format == "tar.gz" ? new GZipStream(stream, CompressionMode.Decompress, leaveOpen: true) : stream;
        using var reader = new TarReader(decompressed, leaveOpen: true);
        while (reader.GetNextEntry() is { } entry)
        {
            if (entry.EntryType is not (TarEntryType.Directory or TarEntryType.RegularFile or TarEntryType.V7RegularFile))
            {
                // Links and the like could point anywhere on the machine.
                continue;
            }

            if (ResolveTarget(entry.Name, folder, strip, out var target) is { } refused)
            {
                return refused;
            }

            if (target is null)
            {
                continue;
            }

            if (entry.EntryType == TarEntryType.Directory)
            {
                Directory.CreateDirectory(target);
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(target) ?? folder);
            using var file = File.Create(target);
            entry.DataStream?.CopyTo(file);
        }

        return null;
    }

    // Where an entry of the archive goes: nowhere when stripping leaves
    // nothing of its path, and refused when its path would leave the folder.
    private static string? ResolveTarget(string name, string folder, int strip, out string? target)
    {
        target = null;
        var parts = name.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Any(part => part == ".." || part.Contains(':', StringComparison.Ordinal)) || name.StartsWith('/'))
        {
            return $"its entry '{name}' would be written outside the folder.";
        }

        if (parts.Length <= strip)
        {
            return null;
        }

        var root = Path.GetFullPath(folder) + Path.DirectorySeparatorChar;
        var full = Path.GetFullPath(Path.Combine([folder, .. parts[strip..]]));
        if (!full.StartsWith(root, StringComparison.Ordinal))
        {
            return $"its entry '{name}' would be written outside the folder.";
        }

        target = full;
        return null;
    }
}
