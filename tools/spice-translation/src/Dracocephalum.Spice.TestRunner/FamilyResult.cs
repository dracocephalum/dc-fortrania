namespace Dracocephalum.Spice.TestRunner;

/// <summary>How one test family went.</summary>
/// <param name="Name">The family's name, which is its routine's.</param>
/// <param name="Passed">Whether every case of it passed.</param>
/// <param name="Seconds">How long it ran, by the clock.</param>
/// <param name="Output">What it wrote to the screen.</param>
internal sealed record FamilyResult(string Name, bool Passed, double Seconds, string Output);
