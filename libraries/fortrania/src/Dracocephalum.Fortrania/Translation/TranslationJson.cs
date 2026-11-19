using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Dracocephalum.Fortrania.Translation;

/// <summary>How the translator reads and writes JSON: one definition, used wherever it does.</summary>
public static class TranslationJson
{
    /// <summary>Gets the options every use of JSON in the translator goes through.</summary>
    public static JsonSerializerOptions Default { get; } = Configure(new JsonSerializerOptions());

    /// <summary>
    /// Applies the translator's settings to a set of options: camel-case
    /// names read without regard to case, enumerations by name, and
    /// characters outside ASCII as themselves. A settings file is written
    /// by hand, so it may hold comments and trailing commas, and a property
    /// the settings do not have is an error and not ignored.
    /// </summary>
    /// <param name="options">The options to set.</param>
    /// <returns>The same options.</returns>
    public static JsonSerializerOptions Configure(JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        options.WriteIndented = true;
        options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.PropertyNameCaseInsensitive = true;
        options.NumberHandling = JsonNumberHandling.AllowReadingFromString;
        options.Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping;
        options.ReadCommentHandling = JsonCommentHandling.Skip;
        options.AllowTrailingCommas = true;
        options.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow;
        if (!options.Converters.OfType<JsonStringEnumConverter>().Any())
        {
            options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        }

        return options;
    }
}
