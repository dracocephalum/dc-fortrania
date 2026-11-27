using System.Text.RegularExpressions;

namespace Dracocephalum.Fortrania.Translation;

/// <summary>
/// The ignores of an entry of a source's files, made ready to test a file
/// against: each a glob, or a regular expression after <c>regex:</c>. A
/// file is tested by its path from the root of the path that found it,
/// parts joined by <c>/</c>, and is ignored when any of them matches.
/// </summary>
/// <remarks>
/// Both kinds ignore case, so that one settings file ignores the same files
/// on every system; a regular expression may turn it back on with
/// <c>(?-i)</c>. A glob matches the whole path, so <c>**/test_*.f</c> is a
/// file of that name at any depth; a regular expression matches anywhere in
/// it, unless it says <c>^</c> or <c>$</c>. A regular expression is run
/// without backtracking, so that no expression can take time without end.
/// </remarks>
internal sealed class IgnoreRules
{
    /// <summary>What says that an ignore is a regular expression.</summary>
    public const string RegexPrefix = "regex:";

    /// <summary>What may say that an ignore is a glob, which it is without it.</summary>
    public const string GlobPrefix = "glob:";

    private readonly List<string> _globs = [];
    private readonly List<Regex> _expressions = [];

    private IgnoreRules()
    {
    }

    /// <summary>Gets the rules of no ignores, which ignore nothing.</summary>
    public static IgnoreRules None { get; } = new();

    /// <summary>Reads the ignores of an entry.</summary>
    /// <param name="ignores">The ignores as the settings give them.</param>
    /// <returns>The rules.</returns>
    /// <exception cref="ArgumentException">An ignore is <c>regex:</c> and something that is not a regular expression run without backtracking.</exception>
    public static IgnoreRules Parse(IEnumerable<string> ignores)
    {
        var rules = new IgnoreRules();
        foreach (var ignore in ignores)
        {
            if (ignore.StartsWith(RegexPrefix, StringComparison.Ordinal))
            {
                var expression = ignore[RegexPrefix.Length..];
                try
                {
                    rules._expressions.Add(new Regex(expression, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.NonBacktracking));
                }
                catch (Exception exception) when (exception is ArgumentException or NotSupportedException)
                {
                    throw new ArgumentException($"'{expression}' is not a regular expression that can be run without backtracking: {exception.Message}", nameof(ignores), exception);
                }
            }
            else
            {
                rules._globs.Add(ignore.StartsWith(GlobPrefix, StringComparison.Ordinal) ? ignore[GlobPrefix.Length..] : ignore);
            }
        }

        return rules;
    }

    /// <summary>Tells whether a file is ignored.</summary>
    /// <param name="relative">Its path from the root of the path that found it, parts joined by <c>/</c>.</param>
    public bool IsIgnored(string relative) =>
        _globs.Any(glob => Glob.IsMatch(glob, relative, ignoreCase: true))
        || _expressions.Any(expression => expression.IsMatch(relative));
}
