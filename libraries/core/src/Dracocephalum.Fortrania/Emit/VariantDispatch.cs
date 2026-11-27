using System.Globalization;

namespace Dracocephalum.Fortrania.Emit;

/// <summary>
/// Writes the part of a class that calls the variants of a unit written for
/// several platforms: for each procedure of the unit, a method of the
/// procedure's name, which is what the program and other libraries call,
/// and which calls the variant its library chose when it was made.
/// </summary>
/// <remarks>
/// The choice is made once, in the library's constructor, and kept in a
/// field, zero for the base; a call costs the field's load and a test,
/// and the method is small enough to be compiled into its callers. Each
/// variant is a method of its own under a name derived from the
/// procedure's, as <see cref="PlatformVariant.GetMethodName"/> gives it,
/// with the parameters of the base's, which every variant has.
/// </remarks>
internal static class VariantDispatch
{
    /// <summary>Writes the unit's methods that choose.</summary>
    /// <param name="baseFile">The base as the emitter wrote it, whose methods give each procedure's parameters.</param>
    /// <param name="procedures">The unit's procedures, by their FORTRAN names, in the order the base has them.</param>
    /// <param name="variants">The unit's variants other than the base, in the order its library tries them; the first is chosen by the field's value 1.</param>
    /// <param name="convention">What the methods are named.</param>
    /// <param name="takesProcedures">Whether a procedure takes another as an argument, which its method then passes on.</param>
    /// <returns>The file, as a unit it is given to the passes made over every unit.</returns>
    public static EmittedUnit Write(
        TreeFile baseFile,
        IReadOnlyList<string> procedures,
        IReadOnlyList<PlatformVariant> variants,
        EmitConvention convention,
        Func<string, bool> takesProcedures)
    {
        var field = PlatformVariant.GetChoiceField(baseFile.UnitName);
        var members = new List<TreeMember>();
        var methods = new List<EmittedMethod>();
        foreach (var procedure in procedures)
        {
            var called = PlatformVariant.Base.GetMethodName(procedure);
            var heading = baseFile.Members.OfType<TreeMethod>().Single(method => method.Procedure == called);
            var branches = variants
                .Select((variant, index) => new TreeStatement.Branch(
                    new TreeExpression.Local(string.Create(CultureInfo.InvariantCulture, $"{field} == {index + 1}")),
                    heading.ForwardTo(variant.GetMethodName(procedure), isLast: true)))
                .Append(new TreeStatement.Branch(null, heading.ForwardTo(called, isLast: true)));

            members.Add(new TreeMethod(
                [new TreeStatement.Comment($"{procedure} as this library's platform has it: the variant of it its context chose when the library was made.")],
                heading.ReturnType,
                convention.ProcedureName(procedure),
                heading.Parameters,
                [],
                [],
                [new TreeStatement.If([.. branches])])
            {
                Procedure = procedure,
                IsInlined = true,
            });

            // In the program's graph of calls it is a procedure that takes
            // nothing of the stack and calls each variant, so that what a
            // caller can need beneath it is the most any of them can.
            var passesOn = takesProcedures(procedure);
            methods.Add(new EmittedMethod(
                procedure,
                0,
                [.. variants.Prepend(PlatformVariant.Base).Select(variant => new EmittedCall(variant.GetMethodName(procedure), [], passesOn)).OrderBy(call => call.Name, StringComparer.Ordinal)],
                CallsThroughArgument: false));
        }

        // Written by the translator and translated from nothing, so it names
        // no source file.
        var file = baseFile with { SourceFile = null, Members = members, TrailingComments = [], Variant = null };
        return new EmittedUnit(CSharpFileWriter.Write(file), methods, [], []) { File = file };
    }
}
