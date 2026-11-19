using System.Text.Json;
using System.Text.Json.Serialization;

namespace Dracocephalum.Fortrania.Translation;

/// <summary>
/// A variable of the settings: a value that a path names as <c>$(Name)</c>,
/// and where it is a folder, optionally an archive to fill it from when it
/// is not there. In the settings file a variable that is only a value is
/// written as a string.
/// </summary>
[JsonConverter(typeof(VariableSettingsConverter))]
public sealed class VariableSettings
{
    /// <summary>Gets or sets the value, a path taken from the settings file's folder where it is used as one.</summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets an archive that fills the folder <see cref="Path"/>
    /// names when there is no such folder: a <c>.zip</c>, a <c>.tar</c>, or
    /// a <c>.tar.gz</c> or <c>.tgz</c>. It is fetched only for that path, and
    /// never where the local settings file or the environment gives the
    /// variable another value.
    /// </summary>
    public Uri? Url { get; set; }

    /// <summary>
    /// Gets or sets the SHA-256 hash of the archive, in hexadecimal. It is
    /// required with <see cref="Url"/>: an archive whose hash differs is
    /// refused, so a changed or tampered archive is never translated.
    /// </summary>
    public string? Sha256 { get; set; }

    /// <summary>Gets or sets how many leading folders of each path in the archive are dropped, as a package's own top folder is.</summary>
    public int Strip { get; set; }

    private sealed class VariableSettingsConverter : JsonConverter<VariableSettings>
    {
        public override VariableSettings? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.String)
            {
                return new VariableSettings { Path = reader.GetString() ?? string.Empty };
            }

            var declared = JsonSerializer.Deserialize<Declared>(ref reader, options)
                ?? throw new JsonException("A variable is a string or an object.");
            return new VariableSettings { Path = declared.Path, Url = declared.Url, Sha256 = declared.Sha256, Strip = declared.Strip };
        }

        public override void Write(Utf8JsonWriter writer, VariableSettings value, JsonSerializerOptions options)
        {
            if (value.Url is null && value.Sha256 is null && value.Strip == 0)
            {
                writer.WriteStringValue(value.Path);
                return;
            }

            JsonSerializer.Serialize(writer, new Declared { Path = value.Path, Url = value.Url, Sha256 = value.Sha256, Strip = value.Strip }, options);
        }
    }

    // The object form, read without this type's converter.
    private sealed class Declared
    {
        public string Path { get; set; } = string.Empty;

        public Uri? Url { get; set; }

        public string? Sha256 { get; set; }

        public int Strip { get; set; }
    }
}
