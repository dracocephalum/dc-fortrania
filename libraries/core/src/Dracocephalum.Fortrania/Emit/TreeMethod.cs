namespace Dracocephalum.Fortrania.Emit;

/// <summary>
/// A method of the program tree: what a subroutine, a function, a main
/// program or one entry point of either has become, or one the translator
/// adds beside them, the loader of a unit's kept variables and the
/// forwarder to a procedure that is passed as an argument.
/// </summary>
/// <param name="Comments">The comments that stood before the procedure's heading, which are about it.</param>
/// <param name="ReturnType">What it gives back, as the target language names the type.</param>
/// <param name="Name">Its name.</param>
/// <param name="Parameters">Its parameters, each its type and its name as the target language declares one.</param>
/// <param name="Preamble">The comments that stood among the procedure's declarations, before anything it does.</param>
/// <param name="Declarations">What stands at the top: constants, kept variables, locals, what is taken from the stack, local functions.</param>
/// <param name="Body">Its statements, with the comments among them.</param>
internal sealed record TreeMethod(
    IReadOnlyList<TreeStatement> Comments,
    string ReturnType,
    string Name,
    IReadOnlyList<string> Parameters,
    IReadOnlyList<TreeStatement> Preamble,
    IReadOnlyList<TreeStatement> Declarations,
    IReadOnlyList<TreeStatement> Body) : TreeMember
{
    /// <summary>Gets the FORTRAN procedure the method is, by its name as the program has it; nothing for a method the translator adds.</summary>
    public string? Procedure { get; init; }

    /// <summary>Gets a value indicating whether the program's own code may call it; one the translator adds is the class's alone.</summary>
    public bool IsPublic { get; init; } = true;

    /// <summary>Gets a value indicating whether it needs nothing of the instance.</summary>
    public bool IsStatic { get; init; }

    /// <summary>Gets a value indicating whether it is never to be compiled into a method that calls it.</summary>
    public bool IsKeptOutOfLine { get; init; }

    /// <summary>
    /// Gets a value indicating whether it is one expression and nothing
    /// more, to be written as that: its body is then a single statement
    /// that evaluates or returns it.
    /// </summary>
    public bool IsOneExpression { get; init; }
}
