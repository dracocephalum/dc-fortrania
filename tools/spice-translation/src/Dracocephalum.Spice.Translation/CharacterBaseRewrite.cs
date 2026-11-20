using System.Globalization;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Dracocephalum.Spice.Translation;

/// <summary>
/// Makes the translated <c>CHBASE</c> return 256. The routine is one
/// constant, the base in which <c>ENCHAR</c> and <c>DECHAR</c> keep a number
/// in characters, and its header defines it: the first number that does not
/// come back from <c>ICHAR(CHAR(n))</c>. The sources translated hold 128,
/// chosen for a compiler that reads a character as a signed number; the
/// runtime reads one as 0 to 255 on every platform, so the definition gives
/// 256 everywhere, which is also what the toolkit's other platforms hold.
/// </summary>
internal static class CharacterBaseRewrite
{
    /// <summary>The routine's file in the translation.</summary>
    public const string FileName = "CHBASE.g.cs";

    /// <summary>What the rewrite changes, in words, for the file's header.</summary>
    public const string Description = "it returns 256, the base its header defines for characters read as 0 to 255, as the runtime reads them, and not the 128 its sources hold for a compiler that reads them as signed.";

    private const string Result = "chbase";
    private const int BaseTranslated = 128;
    private const int BaseOfTheRuntime = 256;

    /// <summary>Gives the routine's text with the runtime's base in place of the one translated.</summary>
    /// <param name="text">The translated routine.</param>
    /// <returns>The rewritten routine.</returns>
    /// <exception cref="RewriteException">The routine does not store the base once, as the sources translated store it.</exception>
    public static string Rewrite(string text)
    {
        var source = SourceText.From(text);
        var root = CSharpSyntaxTree.ParseText(source).GetRoot();
        var stores = new List<LiteralExpressionSyntax>();
        foreach (var assignment in root.DescendantNodes().OfType<AssignmentExpressionSyntax>())
        {
            if (assignment is { Left: IdentifierNameSyntax { Identifier.ValueText: Result }, Right: LiteralExpressionSyntax { Token.Value: BaseTranslated } value }
                && assignment.Parent is ExpressionStatementSyntax)
            {
                stores.Add(value);
            }
        }

        if (stores.Count != 1)
        {
            throw new RewriteException(string.Create(
                CultureInfo.InvariantCulture,
                $"CHBASE stores {BaseTranslated} {stores.Count} times where the sources translated store it once; the rewrite has to be told about the change."));
        }

        return source.WithChanges(new TextChange(stores[0].Span, BaseOfTheRuntime.ToString(CultureInfo.InvariantCulture))).ToString();
    }
}
