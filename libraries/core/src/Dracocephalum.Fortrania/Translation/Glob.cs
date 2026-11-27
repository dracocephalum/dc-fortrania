using System.IO.Enumeration;

namespace Dracocephalum.Fortrania.Translation;

/// <summary>
/// A path some of whose parts may have wildcards: <c>*</c> for any run of
/// characters and <c>?</c> for any one, within a part, and <c>**</c> as a
/// whole part for any number of folders, none among them. It is what an
/// entry of a source's <c>paths</c> is, and an entry of its
/// <c>ignores</c> that is not a regular expression.
/// </summary>
/// <remarks>
/// <c>**</c> as the last part is every file at any depth. Folders a link
/// leads to are not followed beneath <c>**</c>, which could lead in a
/// circle. A name is compared as the file system compares it: without
/// regard to case on Windows and macOS, with regard to it elsewhere.
/// </remarks>
internal static class Glob
{
    private const string AnyFolders = "**";

    // As the file systems most used on each compare names.
    private static readonly bool IgnoresCase = OperatingSystem.IsWindows() || OperatingSystem.IsMacOS();

    /// <summary>
    /// Finds the files a path names: the folder before its first part with a
    /// wildcard is its root, and what lies beneath the root is matched part
    /// by part.
    /// </summary>
    /// <param name="pattern">The path, in full.</param>
    /// <returns>Each file, in full, with its path from the root, parts joined by <c>/</c>; in ordinal order of the full path.</returns>
    public static List<(string Path, string Relative)> Find(string pattern)
    {
        var (root, parts) = Split(pattern);
        var found = new SortedDictionary<string, string>(StringComparer.Ordinal);
        if (Directory.Exists(root))
        {
            Walk(root, string.Empty, parts, 0, found);
        }

        return [.. found.Select(pair => (pair.Key, pair.Value))];
    }

    /// <summary>Tells whether a path from a root matches a pattern of parts, as <see cref="Find"/> matches them.</summary>
    /// <param name="pattern">The pattern, parts joined by <c>/</c>.</param>
    /// <param name="relative">The path, parts joined by <c>/</c>.</param>
    /// <param name="ignoreCase">Whether case is ignored.</param>
    public static bool IsMatch(string pattern, string relative, bool ignoreCase) =>
        IsMatch(SplitParts(pattern), 0, relative.Split('/'), 0, ignoreCase);

    // The folder a pattern starts from, and its parts after it. A path
    // with no wildcard is its folder and its name.
    private static (string Root, string[] Parts) Split(string pattern)
    {
        var parts = SplitParts(pattern);
        var first = Array.FindIndex(parts, HasWildcard);
        if (first < 0)
        {
            first = parts.Length - 1;
        }

        var root = string.Join(Path.DirectorySeparatorChar, parts[..first]);

        // The root of a drive, or of the file system, keeps its separator.
        if (root.Length == 0 || root.EndsWith(':'))
        {
            root += Path.DirectorySeparatorChar;
        }

        string[] rest = parts[first..];
        return (root, rest[^1] == AnyFolders ? [.. rest, "*"] : rest);
    }

    private static string[] SplitParts(string path) => path.Split('/', '\\');

    private static bool HasWildcard(string part) => part.Contains('*', StringComparison.Ordinal) || part.Contains('?', StringComparison.Ordinal);

    private static void Walk(string folder, string relative, string[] parts, int index, SortedDictionary<string, string> found)
    {
        var part = parts[index];
        if (part == AnyFolders)
        {
            // No folder, then each folder beneath, still under the wildcard.
            Walk(folder, relative, parts, index + 1, found);
            foreach (var child in Directory.EnumerateDirectories(folder))
            {
                if (!File.GetAttributes(child).HasFlag(FileAttributes.ReparsePoint))
                {
                    Walk(child, Join(relative, Path.GetFileName(child)), parts, index, found);
                }
            }

            return;
        }

        var isLast = index == parts.Length - 1;
        var entries = isLast ? Directory.EnumerateFiles(folder) : Directory.EnumerateDirectories(folder);
        foreach (var entry in entries)
        {
            var name = Path.GetFileName(entry);
            if (!FileSystemName.MatchesSimpleExpression(part, name, IgnoresCase))
            {
                continue;
            }

            if (isLast)
            {
                found.TryAdd(entry, Join(relative, name));
            }
            else
            {
                Walk(entry, Join(relative, name), parts, index + 1, found);
            }
        }
    }

    private static string Join(string relative, string name) => relative.Length == 0 ? name : relative + "/" + name;

    private static bool IsMatch(string[] pattern, int index, string[] path, int at, bool ignoreCase)
    {
        if (index == pattern.Length)
        {
            return at == path.Length;
        }

        if (pattern[index] == AnyFolders)
        {
            // As the last part, every file beneath; otherwise none or more
            // folders, then the rest.
            if (index == pattern.Length - 1)
            {
                return at < path.Length;
            }

            for (var skip = at; skip < path.Length; skip++)
            {
                if (IsMatch(pattern, index + 1, path, skip, ignoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        return at < path.Length
            && FileSystemName.MatchesSimpleExpression(pattern[index], path[at], ignoreCase)
            && IsMatch(pattern, index + 1, path, at + 1, ignoreCase);
    }
}
