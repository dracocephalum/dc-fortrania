namespace Dracocephalum.Spice.TestRunner;

/// <summary>A test family as the translation has it: a routine that takes where to say whether it passed.</summary>
/// <param name="ok">Where the family stores a FORTRAN logical, zero when a case failed.</param>
internal unsafe delegate void Family(int* ok);
