using System.Runtime.InteropServices;
using System.Text;

namespace Dracocephalum.Fortrania.Emit;

/// <summary>
/// The platform one variant of a program unit is for: an operating system,
/// an architecture or both, or neither for the base, which runs wherever no
/// other variant does. A unit that has variants is written once for each,
/// under names derived from its own, and a method of its own name calls the
/// one its library chose when it was made.
/// </summary>
/// <param name="OSPlatform">The operating system's name as the settings give it; any when <see langword="null"/>.</param>
/// <param name="ProcessArchitecture">The architecture of the process; any when <see langword="null"/>.</param>
internal sealed record PlatformVariant(string? OSPlatform, Architecture? ProcessArchitecture)
{
    /// <summary>The word that names the base in what is derived from a unit's name.</summary>
    public const string BaseKey = "base";

    /// <summary>Gets the base, which is for no platform in particular.</summary>
    public static PlatformVariant Base { get; } = new(null, null);

    /// <summary>Gets a value indicating whether this is the base.</summary>
    public bool IsBase => OSPlatform is null && ProcessArchitecture is null;

    /// <summary>
    /// Gets how specific the variant is, the lowest the most: one for a
    /// system and an architecture, then one for a system, then one for an
    /// architecture, then the base.
    /// </summary>
    public int Rank => (OSPlatform, ProcessArchitecture) switch
    {
        (not null, not null) => 0,
        (not null, null) => 1,
        (null, not null) => 2,
        _ => 3,
    };

    /// <summary>
    /// Gets the word that names the variant in what is derived from a unit's
    /// name: the system and the architecture in lower case, joined by an
    /// underscore, a character of the system's name that no C# name may
    /// hold being one too; <see cref="BaseKey"/> for the base.
    /// </summary>
    public string Key
    {
        get
        {
            if (IsBase)
            {
                return BaseKey;
            }

            var key = new StringBuilder();
            foreach (var character in OSPlatform?.ToLowerInvariant() ?? string.Empty)
            {
                key.Append(char.IsAsciiLetterOrDigit(character) ? character : '_');
            }

            if (ProcessArchitecture is { } architecture)
            {
                key.Append(key.Length > 0 ? "_" : string.Empty).Append(architecture.ToString().ToLowerInvariant());
            }

            return key.ToString();
        }
    }

    /// <summary>Gets the variant in words, for the header of its file: <c>windows</c>, <c>arm64</c>, <c>macos on arm64</c>.</summary>
    public string Description => (OSPlatform, ProcessArchitecture) switch
    {
        (null, null) => "the base, for every platform no other variant is for",
        ({ } system, null) => system,
        (null, { } architecture) => architecture.ToString().ToLowerInvariant(),
        ({ } system, { } architecture) => $"{system} on {architecture.ToString().ToLowerInvariant()}",
    };

    /// <summary>
    /// Gives the field of a library that holds which variant of a unit its
    /// context chose: zero for the base, which is what a field is before
    /// anything is chosen.
    /// </summary>
    /// <param name="unit">The unit's name.</param>
    public static string GetChoiceField(string unit) => "_variant_" + unit;

    /// <summary>
    /// Gives the condition under which a library's context chooses the
    /// variant, written against the context the constructor is given.
    /// </summary>
    /// <param name="context">The expression of the context.</param>
    public string WriteCondition(string context)
    {
        var parts = new List<string>();
        if (OSPlatform is { } system)
        {
            parts.Add($"{context}.IsOSPlatform({system.ToStringLiteral()})");
        }

        if (ProcessArchitecture is { } architecture)
        {
            parts.Add($"{context}.ProcessArchitecture == global::System.Runtime.InteropServices.Architecture.{architecture}");
        }

        return parts.Count == 0 ? "true" : string.Join(" && ", parts);
    }

    /// <summary>
    /// Gives the name of the method a procedure of the variant becomes: the
    /// procedure's FORTRAN name whole between an underscore and the variant's
    /// word, which no FORTRAN name can be, as <c>_ZZPLATFM_windows</c>.
    /// </summary>
    /// <param name="procedure">The procedure's name, in upper case.</param>
    public string GetMethodName(string procedure) => $"_{procedure}_{Key}";
}
