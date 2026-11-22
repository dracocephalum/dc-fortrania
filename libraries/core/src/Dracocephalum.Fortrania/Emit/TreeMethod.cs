namespace Dracocephalum.Fortrania.Emit;

/// <summary>
/// A method of the program tree: what a subroutine, a function, a main
/// program or one entry point of either has become.
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
    IReadOnlyList<TreeStatement> Body);
