using System.Globalization;
using System.Text;

namespace Dracocephalum.Fortrania.Emit;

/// <summary>
/// Writes an expression of the program tree as C#. It chooses spelling and
/// parentheses and nothing else: what is computed, and in what order, is
/// the tree's.
/// </summary>
internal static class CSharpExpressionWriter
{
    /// <summary>Writes an expression.</summary>
    public static string Write(TreeExpression node)
    {
        switch (node)
        {
            case TreeExpression.Integer integer:
                return integer.Value.ToString(CultureInfo.InvariantCulture);
            case TreeExpression.Number number:
                return number.Value.ToCSharpLiteral();
            case TreeExpression.Local local:
                return local.Name;
            case TreeExpression.Utf8 text:
                return text.Text.ToUtf8Literal();
            case TreeExpression.Dereference dereference:
                return "*" + WriteAt(dereference.Address, Level.Unary);
            case TreeExpression.Indexed indexed:
                return $"{WriteAt(indexed.Address, Level.Primary)}[{Write(indexed.Offset)}]";
            case TreeExpression.Unary { Operator: TreeExpression.Operation.Not } not:
                return "!" + WriteAt(not.Operand, Level.Unary);
            case TreeExpression.Unary negation:
                // Two minus signs side by side would read as a decrement.
                var negated = WriteAt(negation.Operand, Level.Unary);
                return negated.StartsWith('-') ? $"-({negated})" : "-" + negated;
            case TreeExpression.Binary binary:
                // The right side is held one level tighter, so that what
                // the tree groups to the right stays grouped: a - (b - c).
                var level = LevelOf(binary);
                return $"{WriteAt(binary.Left, level)} {Spell(binary.Operator)} {WriteAt(binary.Right, level + 1)}";
            case TreeExpression.Sum sum:
                return WriteSum(sum);
            case TreeExpression.Product product:
                return string.Join(" * ", product.Factors.Select(factor => WriteAt(factor, Level.Multiplicative)));
            case TreeExpression.Conditional conditional:
                return $"{WriteAt(conditional.Condition, Level.Or)} ? {Write(conditional.WhenTrue)} : {Write(conditional.WhenFalse)}";
            case TreeExpression.Converted converted:
                return $"({converted.Type}){WriteAt(converted.Operand, Level.Unary)}";
            case TreeExpression.Call call:
                return $"{call.Method}({string.Join(", ", call.Arguments.Select(Write))})";
            case TreeExpression.Grouped grouped:
                return $"({Write(grouped.Inner)})";
            case TreeExpression.Raw raw:
                return raw.Text;
            default:
                throw new ArgumentOutOfRangeException(nameof(node));
        }
    }

    /// <summary>Writes an expression where one that binds at least so strongly is needed, in parentheses when it binds less.</summary>
    public static string WriteAt(TreeExpression node, Level needed) => LevelOf(node) >= needed ? Write(node) : $"({Write(node)})";

    /// <summary>Says how strongly an expression binds as it is written.</summary>
    public static Level LevelOf(TreeExpression node) => node switch
    {
        // A negative number is written with its sign, which is an operator.
        TreeExpression.Integer { Value: < 0 } => Level.Unary,
        TreeExpression.Dereference or TreeExpression.Unary or TreeExpression.Converted => Level.Unary,
        TreeExpression.Binary binary => binary.Operator switch
        {
            TreeExpression.Operation.Multiply or TreeExpression.Operation.Divide => Level.Multiplicative,
            TreeExpression.Operation.Add or TreeExpression.Operation.Subtract => Level.Additive,
            TreeExpression.Operation.Equal or TreeExpression.Operation.NotEqual => Level.Equality,
            TreeExpression.Operation.And => Level.And,
            TreeExpression.Operation.Or => Level.Or,
            _ => Level.Relational,
        },
        TreeExpression.Sum => Level.Additive,
        TreeExpression.Product => Level.Multiplicative,
        TreeExpression.Conditional => Level.Conditional,
        TreeExpression.Raw raw => raw.Level,
        _ => Level.Primary,
    };

    // A term that is added may itself be a sum, and one that is taken away
    // may not, or its own signs would turn.
    private static string WriteSum(TreeExpression.Sum sum)
    {
        var text = new StringBuilder(WriteAt(sum.Terms[0].Value, Level.Additive));
        foreach (var term in sum.Terms.Skip(1))
        {
            text.Append(term.IsSubtracted ? " - " : " + ");
            text.Append(WriteAt(term.Value, term.IsSubtracted ? Level.Multiplicative : Level.Additive));
        }

        return text.ToString();
    }

    private static string Spell(TreeExpression.Operation operation) => operation switch
    {
        TreeExpression.Operation.Add => "+",
        TreeExpression.Operation.Subtract => "-",
        TreeExpression.Operation.Multiply => "*",
        TreeExpression.Operation.Divide => "/",
        TreeExpression.Operation.Equal => "==",
        TreeExpression.Operation.NotEqual => "!=",
        TreeExpression.Operation.Less => "<",
        TreeExpression.Operation.LessOrEqual => "<=",
        TreeExpression.Operation.Greater => ">",
        TreeExpression.Operation.GreaterOrEqual => ">=",
        TreeExpression.Operation.And => "&&",
        TreeExpression.Operation.Or => "||",
        _ => throw new ArgumentOutOfRangeException(nameof(operation)),
    };
}
