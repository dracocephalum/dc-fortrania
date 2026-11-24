namespace Dracocephalum.Fortrania.Cli;

/// <summary>
/// Tells which of a list of words one typed at a prompt stands for, where
/// it may be any start of a word that no other word of the list shares, as
/// the consoles of network switches and <c>diskpart</c> take it.
/// </summary>
/// <remarks>
/// A word given in full is always that word, even where it is the start of
/// another. A shortening that is unambiguous today can become ambiguous
/// when a word is added, so it is for people typing; whatever calls the
/// command from a file spells every word in full.
/// </remarks>
internal static class UniquePrefix
{
    /// <summary>Finds the word a typed one stands for.</summary>
    /// <param name="typed">What was typed.</param>
    /// <param name="words">The words it may stand for.</param>
    /// <param name="candidates">Every word it is the start of, where it is not one word's alone; empty where it is the start of none.</param>
    /// <returns>The word it stands for, or nothing where it stands for none or for several.</returns>
    public static string? Resolve(string typed, IReadOnlyList<string> words, out IReadOnlyList<string> candidates)
    {
        candidates = [];
        if (typed.Length == 0)
        {
            return null;
        }

        if (words.Contains(typed, StringComparer.Ordinal))
        {
            return typed;
        }

        var matches = words.Where(word => word.StartsWith(typed, StringComparison.Ordinal)).ToList();
        if (matches.Count == 1)
        {
            return matches[0];
        }

        candidates = matches;
        return null;
    }
}
