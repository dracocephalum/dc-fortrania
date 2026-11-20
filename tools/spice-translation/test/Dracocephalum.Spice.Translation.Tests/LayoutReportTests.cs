using Dracocephalum.Fortrania.Translation;
using Shouldly;

namespace Dracocephalum.Spice.Translation.Tests;

public sealed class LayoutReportTests : IDisposable
{
    private readonly string _folder = Path.Combine(AppContext.BaseDirectory, "report-" + Guid.NewGuid().ToString("N"));

    public LayoutReportTests() => Directory.CreateDirectory(_folder);

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    [Fact]
    public void Write_WhenATranslationWasDone_ShouldWriteEachFileAndSumThemUp()
    {
        // Arrange
        var result = new TranslationResult([], [], [], 2)
        {
            StackUse =
            [
                new StackUse("Example.Toolkit", "Core", "LEAF", 1200, 1200, ["LEAF"], TakesProcedures: false, IsRecursive: false),
                new StackUse("Example.Toolkit", "Core", "TOP", 16, 1216, ["TOP", "LEAF"], TakesProcedures: true, IsRecursive: false),
            ],
            UnalignedPlaces =
            [
                new UnalignedPlace("Example.Toolkit", "Core", "SHARED", Path.Combine(_folder, "shared.f"), "D", 4, 8, 2),
                new UnalignedPlace("Example.Toolkit", "Core", "SHARED", Path.Combine(_folder, "shared.f"), "E", 2, 4, 2),
            ],
            WidenedPlaces =
            [
                new WidenedPlace("Example.Toolkit", "Core", "CALLER", Path.Combine(_folder, "widen.f"), "IC", "TAKE", 1, 9),
            ],
        };

        // Act
        var lines = LayoutReport.Write(result, _folder);

        // Assert
        File.ReadAllLines(Path.Combine(_folder, LayoutReport.StackFile)).ShouldBe(
        [
            "project\tsource\tprocedure\town bytes\tdeepest bytes\ttakes procedures\trecursive\tdeepest chain",
            "Example.Toolkit\tCore\tLEAF\t1200\t1200\tFalse\tFalse\tLEAF",
            "Example.Toolkit\tCore\tTOP\t16\t1216\tTrue\tFalse\tTOP > LEAF",
        ]);
        File.ReadAllLines(Path.Combine(_folder, LayoutReport.UnalignedFile)).ShouldBe(
        [
            "project\tsource\tunit\tfile\tline\tvariable\toffset\talignment",
            "Example.Toolkit\tCore\tSHARED\tshared.f\t2\tD\t4\t8",
            "Example.Toolkit\tCore\tSHARED\tshared.f\t2\tE\t2\t4",
        ]);
        File.ReadAllLines(Path.Combine(_folder, LayoutReport.WidenedFile)).ShouldBe(
        [
            "project\tsource\tunit\tfile\tline\tvariable\tprocedure\targument",
            "Example.Toolkit\tCore\tCALLER\twiden.f\t9\tIC\tTAKE\t1",
        ]);
        lines.ShouldBe(
        [
            "Example.Toolkit: 2 procedures, the deepest:",
            "      1,216 bytes  TOP > LEAF",
            "      1,200 bytes  LEAF",
            "Off their alignment: 2 variables, 1 of them off a multiple of 4.",
            "Passed as 8 bytes from a 4-byte number: 1 arguments.",
            "Written: stack-use.tsv, unaligned.tsv and widened.tsv, beside the projects.",
        ]);
    }
}
