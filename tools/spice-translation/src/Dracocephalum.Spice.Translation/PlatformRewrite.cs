using System.Globalization;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Dracocephalum.Spice.Translation;

/// <summary>
/// Makes the translated <c>ZZPLATFM</c> report the platform it runs on. The
/// routine is a table of facts about a platform, stored once, and the
/// sources translated hold the facts of the platform they were written for:
/// Linux, little-endian, lines ending in a line feed. Each of those stores
/// becomes a choice made when the routine runs, from the machine and from
/// the program's context, the line ending it writes above all, so that the
/// toolkit's own check on text files agrees with what the program writes.
/// </summary>
/// <remarks>
/// The values chosen between are the toolkit's own names, as its platform
/// variants have them. What this knows of the toolkit stays here, in a tool,
/// and what it writes is machine-made like the rest of the translation.
/// </remarks>
internal static class PlatformRewrite
{
    /// <summary>The routine's file in the translation.</summary>
    public const string FileName = "ZZPLATFM.g.cs";

    /// <summary>What the rewrite changes, in words, for the file's header.</summary>
    public const string Description = "it reports the platform it runs on, and the line ending its program writes, and not the platform its sources were written for.";

    // Each value the sources store, and what is stored in its place: the
    // first value whose condition holds, the last one otherwise.
    private static readonly IReadOnlyDictionary<string, (string Condition, string Value)[]> Choices = new Dictionary<string, (string, string)[]>(StringComparer.Ordinal)
    {
        ["PC"] = [("OperatingSystem.IsMacOS()", "INTEL MAC"), (string.Empty, "PC")],
        ["LINUX"] =
        [
            ("OperatingSystem.IsWindows()", "MICROSOFT WINDOWS"),
            ("OperatingSystem.IsMacOS()", "MAC OS-X"),
            ("OperatingSystem.IsFreeBSD()", "FREEBSD"),
            (string.Empty, "LINUX"),
        ],
        ["GFORTRAN/64BIT"] = [("Environment.Is64BitProcess", "FORTRANIA/64BIT"), (string.Empty, "FORTRANIA/32BIT")],
        ["LTL-IEEE"] = [("BitConverter.IsLittleEndian", "LTL-IEEE"), (string.Empty, "BIG-IEEE")],
        ["LF"] = [("_io.NewLine == \"\\r\\n\"", "CR-LF"), ("_io.NewLine == \"\\r\"", "CR"), (string.Empty, "LF")],
    };

    /// <summary>Gives the routine's text with every fact chosen when it runs.</summary>
    /// <param name="text">The translated routine.</param>
    /// <returns>The rewritten routine.</returns>
    /// <exception cref="RewriteException">A fact is not stored once, as the sources translated store it.</exception>
    public static string Rewrite(string text)
    {
        var source = SourceText.From(text);
        var root = CSharpSyntaxTree.ParseText(source).GetRoot();
        var changes = new List<TextChange>();

        foreach (var (literal, choices) in Choices)
        {
            var stores = root.DescendantNodes().OfType<ExpressionStatementSyntax>().Where(statement => IsStoreOf(statement, literal)).ToList();
            if (stores.Count != 1)
            {
                throw new RewriteException(string.Create(
                    CultureInfo.InvariantCulture,
                    $"ZZPLATFM stores '{literal}' {stores.Count} times where the sources translated store it once; the rewrite has to be told about the change."));
            }

            var statement = stores[0];
            var arguments = ((InvocationExpressionSyntax)statement.Expression).ArgumentList.Arguments;
            var indent = statement.GetLeadingTrivia().LastOrDefault(trivia => trivia.IsKind(SyntaxKind.WhitespaceTrivia)).ToString();
            changes.Add(new TextChange(statement.Span, WriteChoice(choices, arguments[0].ToString(), arguments[1].ToString(), indent)));
        }

        return source.WithChanges(changes).ToString();
    }

    // Whether a statement is Assign(target, length, AddrOf("literal"u8), n).
    private static bool IsStoreOf(ExpressionStatementSyntax statement, string literal) =>
        statement.Expression is InvocationExpressionSyntax { Expression: IdentifierNameSyntax { Identifier.ValueText: "Assign" } } call
        && call.ArgumentList.Arguments.Count == 4
        && call.ArgumentList.Arguments[2].Expression is InvocationExpressionSyntax { Expression: IdentifierNameSyntax { Identifier.ValueText: "AddrOf" } } address
        && address.ArgumentList.Arguments.Count == 1
        && address.ArgumentList.Arguments[0].Expression is LiteralExpressionSyntax value
        && value.Token.ValueText == literal;

    private static string WriteChoice((string Condition, string Value)[] choices, string target, string length, string indent)
    {
        var text = new StringBuilder();
        for (var index = 0; index < choices.Length; index++)
        {
            var (condition, value) = choices[index];
            var opening = condition.Length == 0 ? "else" : index == 0 ? $"if ({condition})" : $"else if ({condition})";
            text.Append(index == 0 ? string.Empty : "\n" + indent).Append(opening).Append('\n')
                .Append(indent).Append("{\n")
                .Append(indent).Append(CultureInfo.InvariantCulture, $"    Assign({target}, {length}, AddrOf(\"{value}\"u8), {Encoding.ASCII.GetByteCount(value)});\n")
                .Append(indent).Append('}');
        }

        return text.ToString();
    }
}
