using Dracocephalum.Fortrania.Syntax;

namespace Dracocephalum.Fortrania.Analysis;

/// <summary>
/// Sets each call of a program unit against the procedure it reaches, and
/// refuses the ones no translation can honour. A call that only bends the
/// rules passes; which those are is said where the check is.
/// </summary>
public static class CallChecker
{
    /// <summary>
    /// Checks every call of <paramref name="unit"/> whose procedure
    /// <paramref name="find"/> knows.
    /// </summary>
    /// <remarks>
    /// Programs written for separate compilation lean on arguments being
    /// passed by address, and such calls pass: a single value where an array
    /// is expected or the reverse, a number or a logical value where a
    /// number of another type is expected, and a function called as a
    /// subroutine for what it does besides returning a value.
    /// </remarks>
    /// <param name="unit">The summary of the calling program unit.</param>
    /// <param name="find">Gives the procedure a name is called by, or <see langword="null"/> when the program does not define it.</param>
    /// <returns>The procedures called that <paramref name="find"/> does not know, each once, in the order first called.</returns>
    /// <exception cref="FortranAnalysisException">
    /// A call gives a number of arguments the procedure does not take, a
    /// procedure where a value is expected or the reverse, a character value
    /// where another type is expected or the reverse; or it references as a
    /// function a subroutine, or a function of another type.
    /// </exception>
    public static IReadOnlyList<string> Check(UnitSummary unit, Func<string, ProcedureInterface?> find)
    {
        ArgumentNullException.ThrowIfNull(unit);
        ArgumentNullException.ThrowIfNull(find);

        var missing = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var call in unit.Calls)
        {
            if (find(call.Name) is { } procedure)
            {
                Check(call, procedure);
            }
            else if (seen.Add(call.Name))
            {
                missing.Add(call.Name);
            }
        }

        return missing;
    }

    private static void Check(CallSite call, ProcedureInterface procedure)
    {
        var at = new SourceLocation(call.Line, null);

        if (call.IsFunctionReference)
        {
            if (!procedure.IsFunction)
            {
                throw at.Error($"{call.Name} is referenced as a function, and it is a subroutine.");
            }

            if (call.ResultType != procedure.ResultType)
            {
                throw at.Error($"{call.Name} is referenced as a function of type {call.ResultType}, and it is of type {procedure.ResultType}.");
            }
        }

        if (call.Arguments.Count != procedure.Arguments.Count)
        {
            throw at.Error($"{call.Name} takes {procedure.Arguments.Count} arguments and is given {call.Arguments.Count}.");
        }

        for (var index = 0; index < call.Arguments.Count; index++)
        {
            var actual = call.Arguments[index];
            var dummy = procedure.Arguments[index];
            var passesProcedure = actual.Kind == ActualArgumentKind.Procedure;
            var takesProcedure = dummy.Kind is DummyArgumentKind.Function or DummyArgumentKind.Subroutine or DummyArgumentKind.Procedure;

            if (passesProcedure != takesProcedure)
            {
                throw at.Error(passesProcedure
                    ? $"Argument {index + 1} of {call.Name} is a procedure, and {dummy.Name} there is not."
                    : $"Argument {index + 1} of {call.Name} is not a procedure, and {dummy.Name} there is.");
            }

            // Characters and numbers are kept in storage of different kinds,
            // which the language never lets stand in for each other.
            if (!passesProcedure && (actual.Type == DataType.Character) != (dummy.Type == DataType.Character))
            {
                throw at.Error($"Argument {index + 1} of {call.Name} is of type {actual.Type}, and {dummy.Name} there is of type {dummy.Type}.");
            }
        }
    }
}
