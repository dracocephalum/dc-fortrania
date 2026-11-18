namespace Dracocephalum.Fortrania.Emit;

/// <summary>
/// The shape emitted procedures present to whatever calls them: what each
/// is named and how its result comes back. The translation inside a
/// procedure is the same under every convention. Derive from this type to
/// match the output of another translator or the habits of a code base.
/// </summary>
public abstract class EmitConvention
{
    /// <summary>
    /// Gets Fortrania's own convention: a procedure keeps the name FORTRAN
    /// gives it, in upper case, and a logical function returns a
    /// <see langword="bool"/>.
    /// </summary>
    public static EmitConvention Fortrania { get; } = new FortraniaConvention();

    /// <summary>
    /// Gets the convention of the translator <c>f2c</c>: a procedure's name
    /// is in lower case with an underscore after it, two when the name
    /// already holds one, and a logical function returns an integer. Code
    /// emitted under it can be called by the names and with the results
    /// that code written against that translator's output expects.
    /// </summary>
    public static EmitConvention F2c { get; } = new F2cConvention();

    /// <summary>
    /// Gets a value indicating whether a logical function returns an
    /// integer, zero for false, and not a <see langword="bool"/>.
    /// </summary>
    public virtual bool ReturnsLogicalAsInteger => false;

    /// <summary>Names the method a subroutine, a function or an entry of one becomes.</summary>
    /// <param name="name">The procedure's FORTRAN name, in upper case.</param>
    /// <returns>
    /// A C# identifier, different for every FORTRAN name, that is not a
    /// keyword of the language.
    /// </returns>
    public abstract string ProcedureName(string name);

    private sealed class FortraniaConvention : EmitConvention
    {
        public override string ProcedureName(string name) => name;
    }

    private sealed class F2cConvention : EmitConvention
    {
        public override bool ReturnsLogicalAsInteger => true;

        public override string ProcedureName(string name)
        {
            ArgumentNullException.ThrowIfNull(name);

            return name.ToLowerInvariant() + (name.Contains('_', StringComparison.Ordinal) ? "__" : "_");
        }
    }
}
