namespace Dracocephalum.Fortrania.Benchmarks;

/// <summary>
/// One benchmark class set up for one choice of its parameters: a baseline
/// and the candidates measured against it, each ready to be called.
/// </summary>
/// <param name="ClassName">The benchmark class.</param>
/// <param name="Parameters">The parameters chosen, as <c>Length=20</c>, or nothing where the class has none.</param>
/// <param name="Group">The category the methods share, or nothing where the class has one baseline.</param>
/// <param name="Baseline">The method the others are set against.</param>
/// <param name="Candidates">The other methods, in the order they are declared.</param>
/// <param name="OperationsPerInvoke">How many operations a method does when called once.</param>
internal sealed record BenchmarkCase(
    string ClassName,
    string Parameters,
    string Group,
    BenchmarkCase.Method Baseline,
    IReadOnlyList<BenchmarkCase.Method> Candidates,
    int OperationsPerInvoke)
{
    /// <summary>A benchmark method of an instance that is set up.</summary>
    /// <param name="Name">The method's name.</param>
    /// <param name="Run">Calls it once.</param>
    internal sealed record Method(string Name, Action Run);
}
