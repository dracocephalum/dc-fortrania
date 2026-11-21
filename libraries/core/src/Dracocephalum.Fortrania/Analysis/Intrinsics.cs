using System.Collections.Frozen;
using Dracocephalum.Fortrania.Syntax;

namespace Dracocephalum.Fortrania.Analysis;

/// <summary>
/// The functions the language supplies, generic and specific names alike:
/// those of FORTRAN 77, and the bit functions of MIL-STD-1753, which every
/// compiler since has carried and the reference sources use.
/// </summary>
internal static class Intrinsics
{
    private const int Any = int.MaxValue;

    private static readonly DataType[] I = [DataType.Integer];
    private static readonly DataType[] R = [DataType.Real];
    private static readonly DataType[] D = [DataType.DoublePrecision];
    private static readonly DataType[] C = [DataType.Complex];
    private static readonly DataType[] Text = [DataType.Character];
    private static readonly DataType[] RD = [DataType.Real, DataType.DoublePrecision];
    private static readonly DataType[] IRD = [DataType.Integer, DataType.Real, DataType.DoublePrecision];
    private static readonly DataType[] RDC = [DataType.Real, DataType.DoublePrecision, DataType.Complex];
    private static readonly DataType[] IRDC = [DataType.Integer, DataType.Real, DataType.DoublePrecision, DataType.Complex];

    // Name, fewest and most arguments, the types the arguments may have, and
    // the result; a null result is the type of the arguments.
    private static readonly FrozenDictionary<string, Intrinsic> Table = new Intrinsic[]
    {
        // Conversion.
        new("INT", 1, 1, IRDC, DataType.Integer),
        new("IFIX", 1, 1, R, DataType.Integer),
        new("IDINT", 1, 1, D, DataType.Integer),
        new("REAL", 1, 1, IRDC, DataType.Real),
        new("FLOAT", 1, 1, I, DataType.Real),
        new("SNGL", 1, 1, D, DataType.Real),
        new("DBLE", 1, 1, IRDC, DataType.DoublePrecision),
        new("CMPLX", 1, 2, IRDC, DataType.Complex),
        new("ICHAR", 1, 1, Text, DataType.Integer),
        new("CHAR", 1, 1, I, DataType.Character),

        // Truncation and rounding.
        new("AINT", 1, 1, RD, null),
        new("DINT", 1, 1, D, null),
        new("ANINT", 1, 1, RD, null),
        new("DNINT", 1, 1, D, null),
        new("NINT", 1, 1, RD, DataType.Integer),
        new("IDNINT", 1, 1, D, DataType.Integer),

        // Arithmetic. ABS of a complex value is real, which TypeOf knows.
        new("ABS", 1, 1, IRDC, null),
        new("IABS", 1, 1, I, null),
        new("DABS", 1, 1, D, null),
        new("CABS", 1, 1, C, DataType.Real),
        new("MOD", 2, 2, IRD, null),
        new("AMOD", 2, 2, R, null),
        new("DMOD", 2, 2, D, null),
        new("SIGN", 2, 2, IRD, null),
        new("ISIGN", 2, 2, I, null),
        new("DSIGN", 2, 2, D, null),
        new("DIM", 2, 2, IRD, null),
        new("IDIM", 2, 2, I, null),
        new("DDIM", 2, 2, D, null),
        new("DPROD", 2, 2, R, DataType.DoublePrecision),
        new("MAX", 2, Any, IRD, null),
        new("MAX0", 2, Any, I, null),
        new("AMAX1", 2, Any, R, null),
        new("DMAX1", 2, Any, D, null),
        new("AMAX0", 2, Any, I, DataType.Real),
        new("MAX1", 2, Any, R, DataType.Integer),
        new("MIN", 2, Any, IRD, null),
        new("MIN0", 2, Any, I, null),
        new("AMIN1", 2, Any, R, null),
        new("DMIN1", 2, Any, D, null),
        new("AMIN0", 2, Any, I, DataType.Real),
        new("MIN1", 2, Any, R, DataType.Integer),
        new("AIMAG", 1, 1, C, DataType.Real),
        new("CONJG", 1, 1, C, null),

        // Character.
        new("LEN", 1, 1, Text, DataType.Integer),
        new("INDEX", 2, 2, Text, DataType.Integer),
        new("LGE", 2, 2, Text, DataType.Logical),
        new("LGT", 2, 2, Text, DataType.Logical),
        new("LLE", 2, 2, Text, DataType.Logical),
        new("LLT", 2, 2, Text, DataType.Logical),

        // Mathematical.
        new("SQRT", 1, 1, RDC, null),
        new("DSQRT", 1, 1, D, null),
        new("CSQRT", 1, 1, C, null),
        new("EXP", 1, 1, RDC, null),
        new("DEXP", 1, 1, D, null),
        new("CEXP", 1, 1, C, null),
        new("LOG", 1, 1, RDC, null),
        new("ALOG", 1, 1, R, null),
        new("DLOG", 1, 1, D, null),
        new("CLOG", 1, 1, C, null),
        new("LOG10", 1, 1, RD, null),
        new("ALOG10", 1, 1, R, null),
        new("DLOG10", 1, 1, D, null),
        new("SIN", 1, 1, RDC, null),
        new("DSIN", 1, 1, D, null),
        new("CSIN", 1, 1, C, null),
        new("COS", 1, 1, RDC, null),
        new("DCOS", 1, 1, D, null),
        new("CCOS", 1, 1, C, null),
        new("TAN", 1, 1, RD, null),
        new("DTAN", 1, 1, D, null),
        new("ASIN", 1, 1, RD, null),
        new("DASIN", 1, 1, D, null),
        new("ACOS", 1, 1, RD, null),
        new("DACOS", 1, 1, D, null),
        new("ATAN", 1, 1, RD, null),
        new("DATAN", 1, 1, D, null),
        new("ATAN2", 2, 2, RD, null),
        new("DATAN2", 2, 2, D, null),
        new("SINH", 1, 1, RD, null),
        new("DSINH", 1, 1, D, null),
        new("COSH", 1, 1, RD, null),
        new("DCOSH", 1, 1, D, null),
        new("TANH", 1, 1, RD, null),
        new("DTANH", 1, 1, D, null),

        // Bits, from MIL-STD-1753.
        new("IOR", 2, 2, I, null),
        new("IAND", 2, 2, I, null),
        new("IEOR", 2, 2, I, null),
        new("NOT", 1, 1, I, null),
        new("ISHFT", 2, 2, I, null),
        new("ISHFTC", 2, 3, I, null),
        new("IBITS", 3, 3, I, null),
        new("BTEST", 2, 2, I, DataType.Logical),
        new("IBSET", 2, 2, I, null),
        new("IBCLR", 2, 2, I, null),
    }.ToFrozenDictionary(intrinsic => intrinsic.Name, StringComparer.Ordinal);

    public static bool Contains(string name) => Table.ContainsKey(name);

    public static Intrinsic? Find(string name) => Table.GetValueOrDefault(name);
}
