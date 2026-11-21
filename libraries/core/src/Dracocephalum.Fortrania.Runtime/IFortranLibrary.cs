// SPDX-License-Identifier: Apache-2.0
// Part of the Fortrania runtime, which code translated from FORTRAN 77 calls.

#nullable enable

namespace Dracocephalum.Fortrania.Runtime;

/// <summary>
/// A translated library, which a context can make for itself the first
/// time it is asked for. Every class the translator emits is one.
/// </summary>
/// <typeparam name="TSelf">The library's own class.</typeparam>
#if FORTRANIA_RUNTIME_INTERNAL
internal
#else
public
#endif
interface IFortranLibrary<TSelf>
    where TSelf : class, IFortranLibrary<TSelf>
{
    /// <summary>Makes the library as part of a program.</summary>
    /// <param name="context">What the libraries of the program share.</param>
    /// <returns>The library, which has added itself to the context.</returns>
    static abstract TSelf Create(FortranContext context);
}
