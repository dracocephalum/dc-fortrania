using System.Collections.Frozen;
using System.Globalization;
using System.Text;
using Dracocephalum.Fortrania.Analysis;
using Dracocephalum.Fortrania.Syntax;

namespace Dracocephalum.Fortrania.Emit;

/// <summary>How names, types and constant values are spelled in C#.</summary>
internal static class CSharpSpellingExtensions
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
    public static string ToCSharpLocal(this string name)
    {
        var lower = name.ToLowerInvariant();
        return Keywords.Contains(lower) ? "@" + lower : lower;
    }

    /// <summary>
    /// Names the C# type a value of <paramref name="type"/> is stored as.
    /// A logical value takes the room of an integer, as the language
    /// requires, so that the two can share storage.
    /// </summary>
    public static string ToCSharpStorageType(this DataType type) => type switch
    {
        DataType.Integer or DataType.Logical => "int",
        DataType.Real => "float",
        DataType.DoublePrecision => "double",
        DataType.Character => "byte",
        _ => throw new ArgumentOutOfRangeException(nameof(type)),
    };

    /// <summary>Gives the number of bytes a value of <paramref name="type"/> is stored in.</summary>
    public static int GetStorageSize(this DataType type) => type == DataType.DoublePrecision ? 8 : 4;

    /// <summary>Spells a constant value; a logical one as a C# <see langword="bool"/>.</summary>
    public static string ToCSharpLiteral(this Constant constant) => constant switch
    {
        Constant.Integer integer => integer.Value.ToString(CultureInfo.InvariantCulture),
        Constant.Real real => real.Value.ToString("R", CultureInfo.InvariantCulture) + "F",
        Constant.DoublePrecision number => Double(number.Value),
        Constant.Logical logical => logical.Value ? "true" : "false",
        _ => throw new ArgumentOutOfRangeException(nameof(constant)),
    };

    /// <summary>Spells ASCII text as a literal of bytes.</summary>
    public static string ToUtf8Literal(this string text) => text.ToStringLiteral() + "u8";

    /// <summary>Spells text as a literal of a string, every character that needs it escaped.</summary>
    public static string ToStringLiteral(this string text)
    {
        const char Backslash = (char)92;
        const char Quote = (char)34;
        const char Delete = (char)127;

        var literal = new StringBuilder().Append(Quote);
        foreach (var character in text)
        {
            if (character is Quote or Backslash)
            {
                literal.Append(Backslash).Append(character);
            }
            else if (character == (char)0)
            {
                // The language has no escape in octal, so a digit after this one is a digit.
                literal.Append(Backslash).Append('0');
            }
            else if (character is < ' ' or Delete)
            {
                literal.Append(Backslash).Append('u').Append(((int)character).ToString("x4", CultureInfo.InvariantCulture));
            }
            else
            {
                literal.Append(character);
            }
        }

        return literal.Append(Quote).ToString();
    }

    // The shortest text that reads back as the same value, made to look
    // like a double where it would otherwise look like an integer.
    private static string Double(double value)
    {
        var text = value.ToString("R", CultureInfo.InvariantCulture);
        return text.Contains('.', StringComparison.Ordinal) || text.Contains('E', StringComparison.Ordinal) ? text : text + ".0";
    }
}
