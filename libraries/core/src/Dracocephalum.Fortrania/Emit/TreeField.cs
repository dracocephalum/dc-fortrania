namespace Dracocephalum.Fortrania.Emit;

/// <summary>
/// A field of the instance, kept from its other code: where a unit's block
/// of kept variables lies once it has been handed out.
/// </summary>
/// <param name="Type">Its type, as the target language names it.</param>
/// <param name="Name">Its name.</param>
internal sealed record TreeField(string Type, string Name) : TreeMember;
