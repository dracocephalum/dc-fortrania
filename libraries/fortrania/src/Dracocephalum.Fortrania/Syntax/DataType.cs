using System.Diagnostics.CodeAnalysis;

namespace Dracocephalum.Fortrania.Syntax;

/// <summary>A FORTRAN 77 data type, as a constant has one and a type statement names one.</summary>
[SuppressMessage(
    "Naming",
    "CA1720:Identifier contains type name",
    Justification = "The members are named for the FORTRAN types they stand for, and INTEGER is one of them.")]
public enum DataType
{
    /// <summary><c>INTEGER</c>.</summary>
    Integer,

    /// <summary><c>REAL</c>.</summary>
    Real,

    /// <summary><c>DOUBLE PRECISION</c>.</summary>
    DoublePrecision,

    /// <summary><c>COMPLEX</c>.</summary>
    Complex,

    /// <summary><c>LOGICAL</c>.</summary>
    Logical,

    /// <summary><c>CHARACTER</c>.</summary>
    Character,
}
