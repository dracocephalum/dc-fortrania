using Dracocephalum.Fortrania.Analysis;
using Dracocephalum.Fortrania.Syntax;

namespace Dracocephalum.Fortrania.Emit;

/// <content>
/// Writes calls of the procedures a FORTRAN program takes from its
/// compiler's library and no standard defines. The program's context
/// supplies them, under names of its own, and a call of one is written on
/// the context. A procedure of the program that has one of these names is
/// the program's, and is found first.
/// </content>
internal sealed partial class UnitEmitter
{
    // Each by the name the FORTRAN calls it by: the context's method, and
    // what it takes, as far as a caller's arguments are to be fitted to it.
    private static readonly Dictionary<string, (string Method, ProcedureInterface Interface)> SystemProcedures = new(StringComparer.Ordinal)
    {
        ["EXIT"] = ("Exit", Takes("EXIT", null, DataType.Integer)),
        ["IARGC"] = ("ArgumentCount", Takes("IARGC", DataType.Integer)),
        ["GETARG"] = ("GetArgument", Takes("GETARG", null, DataType.Integer, DataType.Character)),
        ["GETENV"] = ("GetEnvironment", Takes("GETENV", null, DataType.Character, DataType.Character)),
        ["DATE_AND_TIME"] = ("DateAndTime", Takes("DATE_AND_TIME", null, DataType.Character, DataType.Character, DataType.Character, DataType.Integer)),
        ["SYSTEM"] = ("RunCommand", Takes("SYSTEM", null, DataType.Character, DataType.Integer)),
    };

    // The context's method for a name, where the name is one of these and
    // no library of the program defines a procedure of it.
    private string? SystemMethod(string name) =>
        _findProcedure(name) is null && OwnerOf(name) is null && SystemProcedures.TryGetValue(name, out var procedure)
            ? procedure.Method
            : null;

    private static ProcedureInterface Takes(string name, DataType? result, params DataType[] arguments) => new(
        name,
        result,
        [.. arguments.Select((type, index) => new DummyArgument($"A{index + 1}", DummyArgumentKind.Variable, type, 0, ArgumentUses.Read | ArgumentUses.Written, []))],
        0);
}
