using Dracocephalum.Fortrania.Syntax;

namespace Dracocephalum.Fortrania.Analysis;

/// <summary>What analysis asks of a type.</summary>
internal static class DataTypeExtensions
{
    /// <summary>Says whether values of the type are numbers, which convert among themselves on assignment.</summary>
    public static bool IsNumeric(this DataType type) =>
        type is DataType.Integer or DataType.Real or DataType.DoublePrecision or DataType.Complex;
}
