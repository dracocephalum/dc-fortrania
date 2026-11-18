using System.Collections.Frozen;
using System.Globalization;
using Dracocephalum.Fortrania.Analysis;
using Dracocephalum.Fortrania.Syntax;

namespace Dracocephalum.Fortrania.Emit;

/// <summary>How names, types and constant values are spelled in C#.</summary>
internal static class CSharpText
{
    // A FORTRAN name is written in lower case, where it can meet none of
    // the emitted class's own members, which keep the FORTRAN spelling; a
    // name that is a C# keyword once in lower case takes the escape.
    private static readonly FrozenSet<string> Keywords = new[]
    {
        "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char", "checked", "class", "const",
        "continue", "decimal", "default", "delegate", "do", "double", "else", "enum", "event", "explicit", "extern",
        "false", "finally", "fixed", "float", "for", "foreach", "goto", "if", "implicit", "in", "int", "interface",
        "internal", "is", "lock", "long", "namespace", "new", "null", "object", "operator", "out", "override",
        "params", "private", "protected", "public", "readonly", "ref", "return", "sbyte", "sealed", "short",
        "sizeof", "stackalloc", "static", "string", "struct", "switch", "this", "throw", "true", "try", "typeof",
        "uint", "ulong", "unchecked", "unsafe", "ushort", "using", "virtual", "void", "volatile", "while",
    }.ToFrozenSet(StringComparer.Ordinal);

    /// <summary>Spells the FORTRAN name of a variable, an argument or a constant.</summary>
    public static string Local(string name)
    {
        var lower = name.ToLowerInvariant();
        return Keywords.Contains(lower) ? "@" + lower : lower;
    }

    /// <summary>
    /// Names the C# type a value of <paramref name="type"/> is stored as.
    /// A logical value takes the room of an integer, as the language
    /// requires, so that the two can share storage.
    /// </summary>
    public static string Storage(DataType type) => type switch
    {
        DataType.Integer or DataType.Logical => "int",
        DataType.Real => "float",
        DataType.DoublePrecision => "double",
        _ => throw new ArgumentOutOfRangeException(nameof(type)),
    };

    /// <summary>Gives the number of bytes a value of <paramref name="type"/> is stored in.</summary>
    public static int Size(DataType type) => type == DataType.DoublePrecision ? 8 : 4;

    /// <summary>Spells a constant value; a logical one as a C# <see langword="bool"/>.</summary>
    public static string Literal(Constant constant) => constant switch
    {
        Constant.Integer integer => integer.Value.ToString(CultureInfo.InvariantCulture),
        Constant.Real real => real.Value.ToString("R", CultureInfo.InvariantCulture) + "F",
        Constant.DoublePrecision number => Double(number.Value),
        Constant.Logical logical => logical.Value ? "true" : "false",
        _ => throw new ArgumentOutOfRangeException(nameof(constant)),
    };

    // The shortest text that reads back as the same value, made to look
    // like a double where it would otherwise look like an integer.
    private static string Double(double value)
    {
        var text = value.ToString("R", CultureInfo.InvariantCulture);
        return text.Contains('.', StringComparison.Ordinal) || text.Contains('E', StringComparison.Ordinal) ? text : text + ".0";
    }
}
