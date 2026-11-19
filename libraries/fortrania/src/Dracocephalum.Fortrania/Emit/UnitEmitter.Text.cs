using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Dracocephalum.Fortrania.Analysis;
using Dracocephalum.Fortrania.Syntax;

namespace Dracocephalum.Fortrania.Emit;

/// <content>
/// Writes what makes a character value that was not there before:
/// concatenation and the result of a character function. Such a value needs
/// somewhere to lie. Assigned straight to a variable it lies there; anywhere
/// else it lies in a local of the method set aside for it, which is as long
/// as the value can be at most, a length known when translating.
/// </content>
/// <remarks>
/// Constants are joined when translating, and not each time the code runs:
/// a concatenation of constants alone is one literal, which needs no local
/// at all, and constants side by side among other values are one piece.
/// The bytes are the same either way.
/// </remarks>
internal sealed partial class UnitEmitter
{
    // The most bytes each such local has to hold, over all the statements
    // that use it, and how many are in use in the statement being written.
    private List<int> _buffers = [];
    private int _buffersInUse;

    // Names a local for a character value of at most the given length.
    private string TakeBuffer(int length)
    {
        if (length > StackLimit)
        {
            throw Unsupported(string.Create(
                CultureInfo.InvariantCulture,
                $"a character value of more than {StackLimit} bytes that needs a place of its own"));
        }

        if (_buffersInUse == _buffers.Count)
        {
            _buffers.Add(0);
        }

        _buffers[_buffersInUse] = Math.Max(_buffers[_buffersInUse], Math.Max(length, 1));
        _buffersInUse++;
        return string.Create(CultureInfo.InvariantCulture, $"_s{_buffersInUse}");
    }

    // The values a concatenation joins, in order, however it is grouped.
    private static IEnumerable<Expression> Pieces(Expression expression) => expression switch
    {
        Expression.Binary { Operator: BinaryOperator.Concatenate } joined => Pieces(joined.Left).Concat(Pieces(joined.Right)),
        Expression.Parenthesized { Inner: var inner } when IsConcatenation(inner) => Pieces(inner),
        _ => [expression],
    };

    // Whether a character expression is a constant, and its value if it is:
    // literals and named constants, and concatenations of them, however
    // they are grouped.
    private bool TryFold(Expression expression, [NotNullWhen(true)] out string? text)
    {
        text = (_evaluator.TryEvaluate(expression, _line) as Constant.Character)?.Value;
        return text is not null;
    }

    private static bool IsConcatenation(Expression expression) => expression switch
    {
        Expression.Binary { Operator: BinaryOperator.Concatenate } => true,
        Expression.Parenthesized parenthesized => IsConcatenation(parenthesized.Inner),
        _ => false,
    };

    // The greatest length a character value can have, where that is known
    // when translating. A substring is no longer than what it is cut from;
    // only a dummy argument of assumed length has no such limit.
    private int? Bound(Expression expression)
    {
        switch (expression)
        {
            case Expression.Parenthesized parenthesized:
                return Bound(parenthesized.Inner);

            case Expression.Binary { Operator: BinaryOperator.Concatenate } joined:
                return Bound(joined.Left) + Bound(joined.Right);

            case Expression.Literal literal:
                return (_evaluator.TryEvaluate(literal, _line) as Constant.Character)?.Value.Length;

            case Expression.Substring substring:
                var whole = Bound(substring.Target);
                var last = substring.Last is null ? whole : Known(substring.Last) ?? whole;
                var first = substring.First is null ? 1 : Known(substring.First) ?? 1;
                return last - first + 1;

            case Expression.Name name:
                var symbol = _table.Find(name.Identifier)!;
                return symbol.Kind == SymbolKind.Constant
                    ? (_evaluator.ValueOf(symbol) as Constant.Character)?.Value.Length
                    : DeclaredLength(symbol);

            case Expression.Reference reference:
                var referenced = _table.Find(reference.Identifier)!;

                // CHAR is the one intrinsic that gives a character value.
                return referenced.Kind == SymbolKind.IntrinsicFunction ? 1 : DeclaredLength(referenced);

            default:
                return null;
        }
    }

    private int? DeclaredLength(Symbol symbol) => symbol.Type?.Length switch
    {
        null => 1,
        { IsAssumed: true } => null,
        var length => Known(length.Value!),
    };

    // A concatenation that is not assigned straight to a variable: joined
    // in a local of its own, whose address is the value. Its length is
    // stored on the way, in a local the length is then read from; wherever
    // the two are written, the address comes first.
    private Code Joined(Expression concatenation)
    {
        if (TryFold(concatenation, out var folded))
        {
            return Text(folded);
        }

        var capacity = Bound(concatenation)
            ?? throw Unsupported("a concatenation with an operand of assumed length, outside an assignment");

        return Joined(concatenation, capacity);
    }

    private Code Joined(Expression concatenation, int capacity)
    {
        var buffer = TakeBuffer(capacity);
        var length = TakeSlot(DataType.Integer);
        var capacityText = capacity.ToString(CultureInfo.InvariantCulture);
        return new Code(
            $"{Joining(buffer, capacityText, concatenation)}.End(&{length})",
            DataType.Character,
            Level.Primary,
            length);
    }

    private string Joining(string target, string capacity, Expression concatenation)
    {
        var text = $"{Runtime("Concat")}({target}, {capacity})";
        foreach (var piece in JoinedPieces(concatenation))
        {
            text += $".Add({piece.Text}, {piece.Length})";
        }

        return text;
    }

    // The values a concatenation joins, with each run of constants side by
    // side joined into one. A constant's value has nothing to do when it is
    // written, so joining a run moves no other piece's work before or after
    // another's.
    private List<Code> JoinedPieces(Expression concatenation)
    {
        var pieces = new List<Code>();
        string? run = null;
        foreach (var piece in Pieces(concatenation))
        {
            if (TryFold(piece, out var constant))
            {
                run += constant;
                continue;
            }

            if (run is not null)
            {
                pieces.Add(Text(run));
                run = null;
            }

            pieces.Add(Value(piece));
        }

        if (run is not null)
        {
            pieces.Add(Text(run));
        }

        return pieces;
    }

    // The sum of the lengths of what a concatenation joins, without joining.
    private string JoinedLength(Expression concatenation) =>
        TryFold(concatenation, out var folded)
            ? folded.Length.ToString(CultureInfo.InvariantCulture)
            : string.Join(" + ", JoinedPieces(concatenation).Select(piece => Grouped(piece.Length!)));

    // An assignment to a character variable, element or substring.
    private void AssignText(Code target, Statement.Assignment assignment)
    {
        // A constant is copied as one, padded or cut to the variable's length.
        if (!IsConcatenation(assignment.Value) || TryFold(assignment.Value, out _))
        {
            var value = Value(assignment.Value);
            _body.Line($"{Runtime("Assign")}({target.Text}, {target.Length}, {value.Text}, {value.Length});");
            return;
        }

        // The pieces are joined where they are assigned to, and nothing is
        // copied twice, unless one of them is part of that place: then what
        // is written first would be read afterwards, so they are joined
        // elsewhere and assigned as a whole. Of that only as much as the
        // variable holds is ever needed.
        var root = Root(assignment.Target);
        var overlaps = Pieces(assignment.Value).SelectMany(NameWalker.NamesIn).Contains(root, StringComparer.Ordinal);
        if (!overlaps)
        {
            _body.Line($"{Joining(target.Text, target.Length!, assignment.Value)}.Pad();");
            return;
        }

        var capacity = int.TryParse(target.Length, NumberStyles.None, CultureInfo.InvariantCulture, out var held)
            ? held
            : Bound(assignment.Value)
                ?? throw Unsupported("a concatenation of assumed length assigned to part of itself");
        var joined = Joined(assignment.Value, capacity);
        _body.Line($"{Runtime("Assign")}({target.Text}, {target.Length}, {joined.Text}, {joined.Length});");
    }

    private static string Root(Expression target) => target switch
    {
        Expression.Name name => name.Identifier,
        Expression.Reference reference => reference.Identifier,
        Expression.Substring substring => Root(substring.Target),
        _ => throw new ArgumentOutOfRangeException(nameof(target)),
    };

    // A reference to a character function. The caller supplies the place
    // for the result, as long as its own declaration of the function says,
    // and the function hands the address back, so that the reference is
    // the value.
    private Code TextCall(Symbol function, IReadOnlyList<Expression> arguments)
    {
        var length = DeclaredLength(function)
            ?? throw Unsupported("a CHARACTER function whose length the calling unit does not fix");
        var lengthText = length.ToString(CultureInfo.InvariantCulture);
        var buffer = TakeBuffer(length);

        return new Code(Invocation(function.Name, arguments, [buffer, lengthText]), DataType.Character, Level.Primary, lengthText);
    }
}
