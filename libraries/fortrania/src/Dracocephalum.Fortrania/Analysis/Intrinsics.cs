using System.Collections.Frozen;

namespace Dracocephalum.Fortrania.Analysis;

/// <summary>
/// The functions the language supplies, generic and specific names alike:
/// those of FORTRAN 77, and the bit functions of MIL-STD-1753, which every
/// compiler since has carried and the reference sources use.
/// </summary>
internal static class Intrinsics
{
    private static readonly FrozenSet<string> Names = FrozenSet.ToFrozenSet(
    [
        "INT", "IFIX", "IDINT", "REAL", "FLOAT", "SNGL", "DBLE", "CMPLX", "ICHAR", "CHAR",
        "AINT", "DINT", "ANINT", "DNINT", "NINT", "IDNINT",
        "ABS", "IABS", "DABS", "CABS", "MOD", "AMOD", "DMOD", "SIGN", "ISIGN", "DSIGN",
        "DIM", "IDIM", "DDIM", "DPROD",
        "MAX", "MAX0", "AMAX1", "DMAX1", "AMAX0", "MAX1", "MIN", "MIN0", "AMIN1", "DMIN1", "AMIN0", "MIN1",
        "LEN", "INDEX", "AIMAG", "CONJG",
        "SQRT", "DSQRT", "CSQRT", "EXP", "DEXP", "CEXP", "LOG", "ALOG", "DLOG", "CLOG", "LOG10", "ALOG10", "DLOG10",
        "SIN", "DSIN", "CSIN", "COS", "DCOS", "CCOS", "TAN", "DTAN",
        "ASIN", "DASIN", "ACOS", "DACOS", "ATAN", "DATAN", "ATAN2", "DATAN2",
        "SINH", "DSINH", "COSH", "DCOSH", "TANH", "DTANH",
        "LGE", "LGT", "LLE", "LLT",
        "IOR", "IAND", "NOT", "IEOR", "ISHFT", "ISHFTC", "IBITS", "BTEST", "IBSET", "IBCLR",
    ],
    StringComparer.Ordinal);

    public static bool Contains(string name) => Names.Contains(name);
}
