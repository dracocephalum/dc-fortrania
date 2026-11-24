namespace Dracocephalum.Fortrania.Translation;

/// <summary>One source of a summarized project: the class it became, and its procedures by name.</summary>
/// <param name="Name">The source's name in the settings.</param>
/// <param name="Namespace">The namespace of its class.</param>
/// <param name="Class">The name of its class.</param>
/// <param name="Procedures">Its procedures, every entry point among them, in the order of their names.</param>
internal sealed record SourceSummary(string Name, string Namespace, string Class, IReadOnlyList<ProcedureSummary> Procedures);
