using System.Diagnostics.CodeAnalysis;
using Dracocephalum.Fortrania.Syntax;

namespace Dracocephalum.Fortrania.Analysis;

/// <summary>
/// The value of a constant expression. The kinds are nested here and nothing
/// else can derive from this type. There is none for complex values, which
/// the parser does not read yet.
/// </summary>
[SuppressMessage(
    "Naming",
    "CA1720:Identifier contains type name",
    Justification = "Each kind is named for the FORTRAN type it holds, and INTEGER is one of them.")]
[SuppressMessage(
    "Naming",
    "CA1716:Identifiers should not match keywords",
    Justification = "Each kind is named for the FORTRAN type it holds, and INTEGER is one of them.")]
public abstract record Constant
{
    private Constant()
    {
    }

    /// <summary>Gets the FORTRAN type of the value.</summary>
    public abstract DataType Type { get; }

    /// <summary>An integer, which FORTRAN 77 holds in one numeric storage unit: 32 bits.</summary>
    /// <param name="Value">The value.</param>
    public sealed record Integer(int Value) : Constant
    {
        /// <inheritdoc/>
        public override DataType Type => DataType.Integer;
    }

    /// <summary>A real, in single precision.</summary>
    /// <param name="Value">The value.</param>
    public sealed record Real(float Value) : Constant
    {
        /// <inheritdoc/>
        public override DataType Type => DataType.Real;
    }

    /// <summary>A double precision value.</summary>
    /// <param name="Value">The value.</param>
    public sealed record DoublePrecision(double Value) : Constant
    {
        /// <inheritdoc/>
        public override DataType Type => DataType.DoublePrecision;
    }

    /// <summary>A logical value.</summary>
    /// <param name="Value">The value.</param>
    public sealed record Logical(bool Value) : Constant
    {
        /// <inheritdoc/>
        public override DataType Type => DataType.Logical;
    }

    /// <summary>A character value. Its length is the length of the text, trailing blanks included.</summary>
    /// <param name="Value">The characters, without quotes.</param>
    public sealed record Character(string Value) : Constant
    {
        /// <inheritdoc/>
        public override DataType Type => DataType.Character;
    }
}
