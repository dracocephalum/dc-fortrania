using Dracocephalum.Fortrania.Syntax;

namespace Dracocephalum.Fortrania.Analysis;

/// <content>Decides what each name stands for, once every declaration and use has been read.</content>
public static partial class SymbolTableBuilder
{
    private sealed partial class Builder
    {
        private static readonly TypeSpecification ImplicitInteger = new(DataType.Integer, null);
        private static readonly TypeSpecification ImplicitReal = new(DataType.Real, null);

        private Symbol Finish(Draft entry)
        {
            var kind = Classify(entry);

            if (kind != SymbolKind.Variable && entry.AssignedAt is { } assigned)
            {
                throw assigned.Error($"{entry.Name} is assigned to, and it is {Describe(kind)}.");
            }

            return entry.ToSymbol(kind, TypeOf(entry, kind));
        }

        // The order is the order of certainty: what a declaration settles
        // comes before what only a use can show.
        private SymbolKind Classify(Draft entry)
        {
            if (entry.ConstantValue is not null)
            {
                Reject(entry, entry.CalledAt, "called", SymbolKind.Constant);
                Reject(entry, entry.ReferencedAt, "given arguments or subscripts", SymbolKind.Constant);
                return SymbolKind.Constant;
            }

            if (entry.IsStatementFunction)
            {
                Reject(entry, entry.CalledAt, "called", SymbolKind.StatementFunction);
                Reject(entry, entry.UsedAsValueAt, "used without arguments", SymbolKind.StatementFunction);
                return SymbolKind.StatementFunction;
            }

            if (entry.Dimensions.Count > 0)
            {
                Reject(entry, entry.CalledAt, "called", SymbolKind.Variable);
                return SymbolKind.Variable;
            }

            if (entry.IsEntryPoint)
            {
                return ClassifyEntryPoint(entry);
            }

            if (entry.IsIntrinsic)
            {
                if (!Intrinsics.Contains(entry.Name))
                {
                    throw entry.FirstSeenAt.Error($"{entry.Name} is declared INTRINSIC, and no such function is known.");
                }

                Reject(entry, entry.CalledAt, "called", SymbolKind.IntrinsicFunction);
                return SymbolKind.IntrinsicFunction;
            }

            if (entry.CalledAt is { } called)
            {
                Reject(entry, entry.ReferencedAt, "used as a function", SymbolKind.Subroutine);
                if (entry.DeclaredType is not null)
                {
                    throw called.Error($"{entry.Name} is called, and it has a type, which a subroutine cannot have.");
                }

                if (!entry.IsExternal && !entry.IsDummyArgument)
                {
                    Reject(entry, entry.UsedAsValueAt, "used as a value", SymbolKind.Subroutine);
                }

                return SymbolKind.Subroutine;
            }

            if (entry.ReferencedAt is not null)
            {
                // A name with arguments that is no array is a function. It is
                // the language's own unless the unit says otherwise.
                return entry.IsExternal || entry.IsDummyArgument || !Intrinsics.Contains(entry.Name)
                    ? SymbolKind.ExternalFunction
                    : SymbolKind.IntrinsicFunction;
            }

            if (entry.IsExternal)
            {
                return entry.DeclaredType is null ? SymbolKind.ExternalProcedure : SymbolKind.ExternalFunction;
            }

            return SymbolKind.Variable;
        }

        private SymbolKind ClassifyEntryPoint(Draft entry)
        {
            if (_isFunction)
            {
                // Inside the function its name is the variable that holds the
                // result, so a reference with arguments would be a call to
                // itself, which FORTRAN 77 does not have.
                if ((entry.ReferencedAt ?? entry.CalledAt) is { } recursive)
                {
                    throw recursive.Error($"{entry.Name} is an entry of this function and is invoked inside it; FORTRAN 77 has no recursion.");
                }

                return SymbolKind.Variable;
            }

            if (entry.CalledAt is { } called)
            {
                throw called.Error($"{entry.Name} is an entry of this subroutine and is called inside it; FORTRAN 77 has no recursion.");
            }

            if (entry.DeclaredType is not null)
            {
                throw entry.FirstSeenAt.Error($"{entry.Name} is a subroutine and is given a type, which a subroutine cannot have.");
            }

            Reject(entry, entry.ReferencedAt, "used as a function", SymbolKind.Subroutine);
            Reject(entry, entry.UsedAsValueAt, "used as a value", SymbolKind.Subroutine);
            return SymbolKind.Subroutine;
        }

        private TypeSpecification? TypeOf(Draft entry, SymbolKind kind)
        {
            switch (kind)
            {
                case SymbolKind.Subroutine or SymbolKind.ExternalProcedure:
                    return null;
                case SymbolKind.IntrinsicFunction:
                    return entry.DeclaredType;
            }

            if (entry.DeclaredType is not null)
            {
                return entry.DeclaredType;
            }

            if (_isImplicitNone)
            {
                throw entry.FirstSeenAt.Error($"{entry.Name} has no type, and the program unit says IMPLICIT NONE.");
            }

            // The default rule: I to N is an integer, anything else a real.
            return entry.Name[0] is >= 'I' and <= 'N' ? ImplicitInteger : ImplicitReal;
        }

        private static void Reject(Draft entry, Location? use, string how, SymbolKind kind)
        {
            if (use is { } location)
            {
                throw location.Error($"{entry.Name} is {how}, and it is {Describe(kind)}.");
            }
        }

        private static string Describe(SymbolKind kind) => kind switch
        {
            SymbolKind.Variable => "a variable",
            SymbolKind.Constant => "a named constant",
            SymbolKind.StatementFunction => "a statement function",
            SymbolKind.IntrinsicFunction => "an intrinsic function",
            SymbolKind.ExternalFunction => "a function",
            SymbolKind.Subroutine => "a subroutine",
            _ => "an external procedure",
        };
    }
}
