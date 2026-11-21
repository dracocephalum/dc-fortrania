using BenchmarkDotNet.Attributes;
using Shouldly;

namespace Dracocephalum.Fortrania.Benchmarks.Tests;

public class BenchmarkCatalogTests
{
    [Fact]
    public void ListMethods_WhenAskedOfAnAssembly_ShouldNameEachBenchmarkAsClassAndMethod()
    {
        // Act
        var methods = BenchmarkCatalog.ListMethods(typeof(BenchmarkCatalogTests).Assembly);

        // Assert
        methods.ShouldBe(["Sample.Count", "Sample.CountTwice", "Sample.CountThrice", "Sample.Reset", "Sample.ResetTwice"], ignoreOrder: true);
    }

    [Fact]
    public void SetUp_WhenAPatternNamesOneCandidate_ShouldGiveItWithItsBaselineForEachParameter()
    {
        // Act
        var cases = BenchmarkCatalog.SetUp(typeof(BenchmarkCatalogTests).Assembly, ["sample.CountTwice"]);

        // Assert
        cases.Select(entry => entry.Case.Parameters).ShouldBe(["Step=1", "Step=5"]);
        foreach (var (instance, benchmark) in cases)
        {
            var sample = instance.ShouldBeOfType<Sample>();
            sample.IsSetUp.ShouldBeTrue();
            benchmark.ClassName.ShouldBe("Sample");
            benchmark.Group.ShouldBe("counting");
            benchmark.Baseline.Name.ShouldBe("Count");
            benchmark.Candidates.ShouldHaveSingleItem().Name.ShouldBe("CountTwice");
            benchmark.OperationsPerInvoke.ShouldBe(4);

            benchmark.Baseline.Run();
            benchmark.Candidates[0].Run();
            sample.Total.ShouldBe(3 * sample.Step);

            BenchmarkCatalog.Release(instance);
            sample.IsSetUp.ShouldBeFalse();
        }
    }

    [Fact]
    public void SetUp_WhenAPatternTakesInEveryGroup_ShouldGiveEachGroupItsOwnBaseline()
    {
        // Act
        var cases = BenchmarkCatalog.SetUp(typeof(BenchmarkCatalogTests).Assembly, ["*"]);

        // Assert
        cases.Select(entry => (entry.Case.Group, entry.Case.Baseline.Name, entry.Case.Candidates.Count))
            .Distinct()
            .ShouldBe([("counting", "Count", 2), ("resetting", "Reset", 1)], ignoreOrder: true);
    }

    [Fact]
    public void SetUp_WhenOnlyABaselineMatches_ShouldGiveNothing()
    {
        // Act
        var cases = BenchmarkCatalog.SetUp(typeof(BenchmarkCatalogTests).Assembly, ["*.Count"]);

        // Assert
        cases.ShouldBeEmpty();
    }

    // A benchmark class as one is written for the library, with a
    // parameter, two groups and the methods that set up and let go.
    public sealed class Sample
    {
        [Params(1, 5)]
        public int Step { get; set; }

        public int Total { get; private set; }

        public bool IsSetUp { get; private set; }

        [GlobalSetup]
        public void SetUp() => IsSetUp = true;

        [GlobalCleanup]
        public void LetGo() => IsSetUp = false;

        [Benchmark(Baseline = true, OperationsPerInvoke = 4)]
        [BenchmarkCategory("counting")]
        public void Count() => Total += Step;

        [Benchmark(OperationsPerInvoke = 4)]
        [BenchmarkCategory("counting")]
        public int CountTwice() => Total += 2 * Step;

        [Benchmark(OperationsPerInvoke = 4)]
        [BenchmarkCategory("counting")]
        public int CountThrice() => Total += 3 * Step;

        [Benchmark(Baseline = true)]
        [BenchmarkCategory("resetting")]
        public void Reset() => Total = 0;

        [Benchmark]
        [BenchmarkCategory("resetting")]
        public void ResetTwice() => Total = 0;
    }
}
