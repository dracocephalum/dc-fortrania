// SPDX-License-Identifier: Apache-2.0
// Part of the Fortrania runtime, which code translated from FORTRAN 77 calls.

#nullable enable

using System;
using System.Runtime.CompilerServices;

namespace Dracocephalum.Fortrania.Runtime;

/// <summary>
/// What translated code needs and C# has no operator or library call for:
/// the places where FORTRAN 77 arithmetic is defined differently, and the
/// passing of a value where a procedure expects an address.
/// </summary>
#if FORTRANIA_RUNTIME_INTERNAL
internal
#else
public
#endif
static unsafe class Fortran
{
    /// <summary>
    /// Stores a value that is not a variable where a procedure can be given
    /// its address, as an expression, so that it can stand in an argument
    /// list.
    /// </summary>
    /// <param name="slot">A local variable of the caller set aside for this argument.</param>
    /// <param name="value">The value to pass.</param>
    /// <returns><paramref name="slot"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int* Arg(int* slot, int value)
    {
        *slot = value;
        return slot;
    }

    /// <inheritdoc cref="Arg(int*, int)"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float* Arg(float* slot, float value)
    {
        *slot = value;
        return slot;
    }

    /// <inheritdoc cref="Arg(int*, int)"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double* Arg(double* slot, double value)
    {
        *slot = value;
        return slot;
    }

    /// <summary>Raises an integer to an integer power, as <c>I ** J</c> does.</summary>
    /// <param name="value">The base.</param>
    /// <param name="exponent">The exponent. A negative one gives zero, unless the base is one or minus one.</param>
    /// <returns>The power, wrapped to 32 bits where it does not fit.</returns>
    /// <exception cref="DivideByZeroException">Zero is raised to a negative power.</exception>
    public static int Pow(int value, int exponent)
    {
        if (exponent < 0)
        {
            return value switch
            {
                1 => 1,
                -1 => (exponent & 1) == 0 ? 1 : -1,
                0 => throw new DivideByZeroException(),
                _ => 0,
            };
        }

        var result = 1;
        var remaining = (uint)exponent;
        while (remaining != 0)
        {
            if ((remaining & 1) != 0)
            {
                result *= value;
            }

            value *= value;
            remaining >>= 1;
        }

        return result;
    }

    /// <summary>
    /// Raises a double precision value to an integer power by repeated
    /// multiplication, in the order the reference compiler multiplies, so
    /// that the result agrees with it to the last bit.
    /// </summary>
    /// <param name="value">The base.</param>
    /// <param name="exponent">The exponent; a negative one gives the reciprocal of the positive power.</param>
    /// <returns>The power.</returns>
    public static double Pow(double value, int exponent)
    {
        var remaining = (uint)Math.Abs((long)exponent);
        var result = (remaining & 1) != 0 ? value : 1.0;
        while ((remaining >>= 1) != 0)
        {
            value *= value;
            if ((remaining & 1) != 0)
            {
                result *= value;
            }
        }

        return exponent < 0 ? 1.0 / result : result;
    }

    /// <inheritdoc cref="Pow(double, int)"/>
    public static float Pow(float value, int exponent)
    {
        var remaining = (uint)Math.Abs((long)exponent);
        var result = (remaining & 1) != 0 ? value : 1.0f;
        while ((remaining >>= 1) != 0)
        {
            value *= value;
            if ((remaining & 1) != 0)
            {
                result *= value;
            }
        }

        return exponent < 0 ? 1.0f / result : result;
    }

    /// <summary>Rounds to the nearest integer, a half away from zero, as <c>NINT</c> does.</summary>
    /// <param name="value">The value to round.</param>
    /// <returns>The nearest integer.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Nint(double value) => (int)Math.Round(value, MidpointRounding.AwayFromZero);

    /// <inheritdoc cref="Nint(double)"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Nint(float value) => (int)MathF.Round(value, MidpointRounding.AwayFromZero);

    /// <summary>Gives the magnitude of one integer the sign of another, as <c>SIGN</c> does.</summary>
    /// <param name="magnitude">The value whose magnitude is taken.</param>
    /// <param name="sign">The value whose sign is taken; zero counts as positive.</param>
    /// <returns>The magnitude of <paramref name="magnitude"/>, negated when <paramref name="sign"/> is negative.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Sign(int magnitude, int sign)
    {
        var absolute = magnitude < 0 ? -magnitude : magnitude;
        return sign < 0 ? -absolute : absolute;
    }

    /// <summary>Gives the positive difference, as <c>DIM</c> does.</summary>
    /// <param name="left">The value subtracted from.</param>
    /// <param name="right">The value subtracted.</param>
    /// <returns>The difference when <paramref name="left"/> is the greater, and zero otherwise.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Dim(int left, int right) => left > right ? left - right : 0;

    /// <inheritdoc cref="Dim(int, int)"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Dim(float left, float right) => left > right ? left - right : 0.0f;

    /// <inheritdoc cref="Dim(int, int)"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double Dim(double left, double right) => left > right ? left - right : 0.0;
}
