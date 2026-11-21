using Dracocephalum.Fortrania.Analysis;

namespace Dracocephalum.Fortrania.Emit;

/// <summary>
/// Another translated library whose procedures the one being emitted may
/// call, as the libraries a FORTRAN program is linked from call each other.
/// It is a class of its own, made over the same context.
/// </summary>
/// <param name="Namespace">The namespace of the other library's class.</param>
/// <param name="ClassName">The other library's class.</param>
/// <param name="FindProcedure">
/// Gives the procedure of the other library a name is called by, or
/// <see langword="null"/> when it defines none of that name.
/// </param>
public sealed record LibraryReference(string Namespace, string ClassName, Func<string, ProcedureInterface?> FindProcedure)
{
    /// <summary>Gets the other library's class by a name that is the same from any namespace.</summary>
    public string QualifiedName => $"global::{Namespace}.{ClassName}";

    /// <summary>Gets the name of the member of an emitted class that gives the other library's instance.</summary>
    public string Accessor => "_" + ClassName;
}
