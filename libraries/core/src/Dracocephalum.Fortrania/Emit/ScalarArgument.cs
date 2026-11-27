namespace Dracocephalum.Fortrania.Emit;

/// <summary>
/// An argument of a method that is one number or logical value of the
/// FORTRAN procedure's, passed by its address as every argument is: one
/// <see cref="ScalarLocalPass"/> may keep in a local while the method runs.
/// </summary>
/// <param name="Position">Its place among the procedure's arguments, counted from zero.</param>
/// <param name="Name">The parameter's name.</param>
/// <param name="Type">The type its address points at, as the target language names it.</param>
internal sealed record ScalarArgument(int Position, string Name, string Type);
