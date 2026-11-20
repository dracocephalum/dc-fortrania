using System.Globalization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Dracocephalum.Spice.Translation;

/// <summary>
/// Makes the translated test family <c>F_ZZASC2</c> expect what the platform
/// it runs on does with text kernels. The family writes kernels whose lines
/// end in each of the three ways and expects <c>FURNSH</c> to refuse those
/// that do not end the platform's way. Which those are is fixed in the
/// sources, one copy of the family for each platform, and the copy
/// translated is Linux's. The expectations become a choice made when the
/// family runs, from the line ending its program writes, which is also what
/// the rewritten <c>ZZPLATFM</c> reports.
/// </summary>
/// <remarks>
/// The family's first nine kernels are three sizes of three kinds, in the
/// order CR, LF, CR-LF; the rest mix the kinds and are expected to load
/// everywhere. The rewrite holds that to be so only where the sources
/// expect exactly the refusals a platform whose lines end in a line feed
/// has, and refuses otherwise.
/// </remarks>
internal static class LineEndingTestRewrite
{
    /// <summary>The family's file in the translation.</summary>
    public const string FileName = "F_ZZASC2.g.cs";

    /// <summary>What the rewrite changes, in words, for the file's header.</summary>
    public const string Description = "the kernels it expects FURNSH to refuse are those whose lines do not end the way its program writes them, and not the way the platform its sources were written for does.";

    private const string Flags = "myfnef";
    private const string Messages = "myfnem";
    private const string Refusal = "SPICE(INCOMPATIBLEEOL)";
    private const int MessageLength = 32;

    // The kernels a platform whose lines end in a line feed refuses, by
    // index from 0: the CR kernels and the CR-LF kernels of each size.
    private static readonly int[] RefusedWhereLinesEndInALineFeed = [0, 2, 3, 5, 6, 8];

    /// <summary>Gives the family's text with the refusals it expects chosen when it runs.</summary>
    /// <param name="text">The translated family.</param>
    /// <returns>The rewritten family.</returns>
    /// <exception cref="RewriteException">The family does not expect the refusals the sources translated expect.</exception>
    public static string Rewrite(string text)
    {
        var source = SourceText.From(text);
        var root = CSharpSyntaxTree.ParseText(source).GetRoot();
        var flags = new List<(ExpressionStatementSyntax Statement, int Index)>();
        var messages = new List<(ExpressionStatementSyntax Statement, int Index)>();
        foreach (var statement in root.DescendantNodes().OfType<ExpressionStatementSyntax>())
        {
            if (ToFlagIndex(statement) is { } flag)
            {
                flags.Add((statement, flag));
            }
            else if (ToMessageIndex(statement) is { } message)
            {
                messages.Add((statement, message));
            }
        }

        if (!flags.Select(flag => flag.Index).SequenceEqual(RefusedWhereLinesEndInALineFeed)
            || !messages.Select(message => message.Index).SequenceEqual(RefusedWhereLinesEndInALineFeed))
        {
            throw new RewriteException(string.Create(
                CultureInfo.InvariantCulture,
                $"F_ZZASC2 expects refusals of the kernels {Describe(flags.Select(flag => flag.Index))} with messages for {Describe(messages.Select(message => message.Index))}, where the sources translated expect {Describe(RefusedWhereLinesEndInALineFeed)} for both; the rewrite has to be told about the change."));
        }

        var first = flags[0].Statement;
        var indent = first.GetLeadingTrivia().LastOrDefault(trivia => trivia.IsKind(SyntaxKind.WhitespaceTrivia)).ToString();
        var changes = new List<TextChange> { new(first.Span, WriteChoice(indent)) };
        changes.AddRange(flags.Skip(1).Concat(messages).Select(store => new TextChange(store.Statement.FullSpan, string.Empty)));

        return source.WithChanges(changes).ToString();
    }

    // The index of a statement myfnef[index] = 1, or null.
    private static int? ToFlagIndex(ExpressionStatementSyntax statement) =>
        statement.Expression is AssignmentExpressionSyntax
        {
            Left: ElementAccessExpressionSyntax { Expression: IdentifierNameSyntax { Identifier.ValueText: Flags } } access,
            Right: LiteralExpressionSyntax { Token.ValueText: "1" },
        }
        && access.ArgumentList.Arguments.Count == 1
        && access.ArgumentList.Arguments[0].Expression is LiteralExpressionSyntax { Token.Value: int index }
            ? index
            : null;

    // The index of a statement Assign(myfnem + index * 32, 32,
    // AddrOf("SPICE(INCOMPATIBLEEOL)"u8), 22), or null; the first is
    // written without the offset.
    private static int? ToMessageIndex(ExpressionStatementSyntax statement)
    {
        if (statement.Expression is not InvocationExpressionSyntax { Expression: IdentifierNameSyntax { Identifier.ValueText: "Assign" } } call
            || call.ArgumentList.Arguments.Count != 4
            || call.ArgumentList.Arguments[2].Expression is not InvocationExpressionSyntax { Expression: IdentifierNameSyntax { Identifier.ValueText: "AddrOf" } } address
            || address.ArgumentList.Arguments.Count != 1
            || address.ArgumentList.Arguments[0].Expression is not LiteralExpressionSyntax { Token.ValueText: Refusal })
        {
            return null;
        }

        var target = call.ArgumentList.Arguments[0].Expression;
        if (target is IdentifierNameSyntax { Identifier.ValueText: Messages })
        {
            return 0;
        }

        return target is BinaryExpressionSyntax { Left: IdentifierNameSyntax { Identifier.ValueText: Messages }, OperatorToken.ValueText: "+", Right: BinaryExpressionSyntax offset }
            && offset is { Left: LiteralExpressionSyntax { Token.Value: int index }, OperatorToken.ValueText: "*", Right: LiteralExpressionSyntax { Token.Value: MessageLength } }
                ? index
                : null;
    }

    // The kernels are CR, LF and CR-LF in turn, so a kernel's kind is its
    // index modulo 3, and the platform's own kind is the one its program
    // writes. The names begin with an underscore, which no FORTRAN name
    // does, and are scoped to the block.
    private static string WriteChoice(string indent)
    {
        string[] lines =
        [
            "{",
            "    // Refused: each kernel whose lines do not end the way this program writes them.",
            "    var _native = _io.NewLine == \"\\r\\n\" ? 2 : _io.NewLine == \"\\r\" ? 0 : 1;",
            "    for (var _kernel = 0; _kernel < 9; _kernel++)",
            "    {",
            "        if (_kernel % 3 != _native)",
            "        {",
            $"            {Flags}[_kernel] = 1;",
            string.Create(CultureInfo.InvariantCulture, $"            Assign({Messages} + _kernel * {MessageLength}, {MessageLength}, AddrOf(\"{Refusal}\"u8), {Refusal.Length});"),
            "        }",
            "    }",
            "}",
        ];

        return string.Join("\n" + indent, lines);
    }

    private static string Describe(IEnumerable<int> indices) => "[" + string.Join(", ", indices) + "]";
}
