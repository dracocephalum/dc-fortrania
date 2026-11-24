using System.Text.Json;
using System.Text.Json.Serialization;

namespace Dracocephalum.Fortrania.Translation;

/// <summary>
/// What a translation of one project shows to a translation of another
/// that calls into it: how each of its procedures is called and how deep
/// a call of it can go, and the working region the project needs. A
/// translation writes one beside each project it translates, and reads it
/// in place of the project's sources where it is told not to translate
/// that project.
/// </summary>
/// <remarks>
/// It holds nothing of the FORTRAN but what a caller has to know, so that
/// it can be committed and published where the sources cannot. Written in
/// the same order every time, so that a translation that changes nothing a
/// caller sees writes the same bytes.
/// </remarks>
/// <param name="Format">The version of this form; a reader refuses another.</param>
/// <param name="Project">The project's name.</param>
/// <param name="OwnBytes">What the project's own region of working buffers takes.</param>
/// <param name="ReachBytes">What a call into the project can need: its own region and the deepest chain of the projects it refers to.</param>
/// <param name="References">The projects it refers to, by name.</param>
/// <param name="Sources">Its sources, in the order of the settings.</param>
internal sealed record ProjectSummary(
    int Format,
    string Project,
    long OwnBytes,
    long ReachBytes,
    IReadOnlyList<string> References,
    IReadOnlyList<SourceSummary> Sources)
{
    /// <summary>The version of the form written now.</summary>
    public const int CurrentFormat = 1;

    /// <summary>The name of the file, in a project's folder of translated sources.</summary>
    public const string FileName = ".fortrania-summary.json";

    // The settings' own form, leaving out what is not there. A summary is
    // written a procedure to a line, since that of a large library is a few
    // thousand of them and is read by people in a diff as well.
    private static readonly JsonSerializerOptions Json = new(TranslationJson.Default)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private static readonly JsonSerializerOptions Line = new(Json) { WriteIndented = false };

    /// <summary>Reads a summary.</summary>
    /// <param name="path">The file.</param>
    /// <returns>The summary.</returns>
    /// <exception cref="JsonException">The file is not a summary, or one of a form this translator does not read.</exception>
    public static ProjectSummary Load(string path)
    {
        var summary = JsonSerializer.Deserialize<ProjectSummary>(File.ReadAllText(path), Json)
            ?? throw new JsonException("The summary is empty.");
        return summary.Format == CurrentFormat
            ? summary
            : throw new JsonException($"The summary is of form {summary.Format}, and this translator reads form {CurrentFormat}; translate the project again.");
    }

    /// <summary>Gives the summary's text: a procedure to a line, with a line feed at the end of each.</summary>
    /// <returns>The text.</returns>
    public string ToJson()
    {
        string Write<T>(T value) => JsonSerializer.Serialize(value, Line);

        var text = new System.Text.StringBuilder();
        text.Append("{\n");
        text.Append("  \"format\": ").Append(Write(Format)).Append(",\n");
        text.Append("  \"project\": ").Append(Write(Project)).Append(",\n");
        text.Append("  \"ownBytes\": ").Append(Write(OwnBytes)).Append(",\n");
        text.Append("  \"reachBytes\": ").Append(Write(ReachBytes)).Append(",\n");
        text.Append("  \"references\": ").Append(Write(References)).Append(",\n");
        text.Append("  \"sources\": [");
        for (var index = 0; index < Sources.Count; index++)
        {
            var source = Sources[index];
            text.Append(index == 0 ? "\n" : ",\n");
            text.Append("    {\n");
            text.Append("      \"name\": ").Append(Write(source.Name)).Append(",\n");
            text.Append("      \"namespace\": ").Append(Write(source.Namespace)).Append(",\n");
            text.Append("      \"class\": ").Append(Write(source.Class)).Append(",\n");
            text.Append("      \"procedures\": [");
            for (var number = 0; number < source.Procedures.Count; number++)
            {
                text.Append(number == 0 ? "\n" : ",\n");
                text.Append("        ").Append(Write(source.Procedures[number]));
            }

            text.Append(source.Procedures.Count == 0 ? "]\n" : "\n      ]\n");
            text.Append("    }");
        }

        text.Append(Sources.Count == 0 ? "]\n" : "\n  ]\n");
        text.Append("}\n");
        return text.ToString();
    }
}
