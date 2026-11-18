using System.Globalization;
using Dracocephalum.Fortrania.Analysis;
using Dracocephalum.Fortrania.Syntax;

namespace Dracocephalum.Fortrania.Emit;

/// <content>
/// Writes variables that <c>EQUIVALENCE</c> makes share storage. Those that
/// share, directly or through one another, are laid out together once, each
/// at the byte the statements put it, and the method has each as a pointer
/// to its place. The bytes lie in the unit's block when one of the
/// variables is kept between calls, and on the stack otherwise.
/// </content>
internal sealed partial class UnitEmitter
{
    // The sets of variables that share storage, and the set of each such
    // variable by its name.
    private readonly List<Overlay> _overlays = [];
    private readonly Dictionary<string, Overlay> _overlayOf = new(StringComparer.Ordinal);

    // Whether the method has a variable as a pointer to a place laid out
    // for it: one kept between calls, or one that shares storage.
    private bool IsPlaced(Symbol symbol) => IsKept(symbol) || _overlayOf.ContainsKey(symbol.Name);

    // Works out which variables share storage and where each begins,
    // counted in bytes from the first byte of the set.
    private void LayOutOverlays()
    {
        foreach (var set in _table.Equivalences)
        {
            Overlay? overlay = null;
            long shared = 0;

            foreach (var item in set)
            {
                var (variable, offset) = Position(item);
                var known = _overlayOf.GetValueOrDefault(variable.Name);

                if (overlay is null)
                {
                    // The first of a list says where in its set the shared
                    // byte is; a variable met for the first time starts one.
                    overlay = known ?? Start(variable);
                    shared = overlay.Starts[variable] + offset;
                }
                else if (known is null)
                {
                    overlay.Starts[variable] = shared - offset;
                    _overlayOf[variable.Name] = overlay;
                }
                else if (known == overlay)
                {
                    if (overlay.Starts[variable] + offset != shared)
                    {
                        throw new SourceLocation(variable.Line, variable.IncludeFile).Error($"EQUIVALENCE puts {variable.Name} in two places.");
                    }
                }
                else
                {
                    // Two sets turn out to be one: the other moves over.
                    var shift = shared - offset - known.Starts[variable];
                    foreach (var (member, start) in known.Starts)
                    {
                        overlay.Starts[member] = start + shift;
                        _overlayOf[member.Name] = overlay;
                    }

                    _overlays.Remove(known);
                }
            }
        }

        for (var index = 0; index < _overlays.Count; index++)
        {
            var overlay = _overlays[index];
            var first = overlay.Starts.Values.Min();
            foreach (var member in overlay.Starts.Keys.ToList())
            {
                overlay.Starts[member] -= first;
            }

            overlay.Size = overlay.Starts.Max(member => member.Value + (ElementCount(member.Key) * ElementSize(member.Key)));
            overlay.Block = string.Create(CultureInfo.InvariantCulture, $"_e{index + 1}");
        }
    }

    private Overlay Start(Symbol variable)
    {
        var overlay = new Overlay();
        overlay.Starts[variable] = 0;
        _overlays.Add(overlay);
        _overlayOf[variable.Name] = overlay;
        return overlay;
    }

    // The variable an item of an EQUIVALENCE list names, and the byte of
    // that variable the item stands for.
    private (Symbol Variable, long Offset) Position(Expression item)
    {
        var whole = item is Expression.Substring substring ? substring.Target : item;
        var (variable, element) = Place(whole, []);
        _line = variable.Line;

        if (variable is not { Kind: SymbolKind.Variable, IsDummyArgument: false, IsEntryPoint: false })
        {
            throw new SourceLocation(variable.Line, variable.IncludeFile).Error($"{variable.Name} is in an EQUIVALENCE list and is not a local variable.");
        }

        var offset = element * ElementSize(variable);
        if (item is Expression.Substring { First: { } first })
        {
            offset += Whole(first, []) - 1;
        }

        return (variable, offset);
    }

    // The lines of a method for the sets that lie on the stack: the bytes
    // of each set the method names a variable of, and a pointer for each
    // variable named.
    private void WriteOverlays(CodeWriter file)
    {
        foreach (var overlay in _overlays.Where(overlay => !overlay.IsKept))
        {
            var named = _table.Symbols.Where(symbol => overlay.Starts.ContainsKey(symbol) && _named.Contains(symbol)).ToList();
            if (named.Count == 0)
            {
                continue;
            }

            file.Line(string.Create(CultureInfo.InvariantCulture, $"byte* {overlay.Block} = stackalloc byte[{overlay.Size}];"));
            foreach (var symbol in named)
            {
                file.Line(PlacedDeclaration(symbol, overlay.Block, overlay.Starts[symbol]));
            }
        }
    }

    private sealed class Overlay
    {
        public Dictionary<Symbol, long> Starts { get; } = [];

        public long Size { get; set; }

        public bool IsKept { get; set; }

        public string Block { get; set; } = string.Empty;
    }
}
