using Shouldly;

namespace Dracocephalum.Fortrania.Tests.Emit;

/// <summary>
/// Runs the translated character sample. Every expected value is what the
/// reference compiler's program printed for the same source and arguments.
/// </summary>
public class CSharpEmitterCharacterTests(CharacterSampleFixture sample) : IClassFixture<CharacterSampleFixture>
{
    [Fact]
    public void Run_WhenCharacterValuesAreAssigned_ShouldCutPadAndStoreIntoSubstrings() =>
        sample.Program.Run<string>("Assign").ShouldBe(">b         LONG TAIL A'B\"C\\D  ");

    [Fact]
    public void Run_WhenCharacterValuesAreCompared_ShouldTakeTheShorterAsFollowedByBlanks() =>
        sample.Program.Run<int[]>("Compare").ShouldBe([873, 910, 370, 882, 882]);

    [Fact]
    public void Run_WhenCharacterIntrinsicsAreUsed_ShouldMeasureSearchAndConvert() =>
        sample.Program.Run<int[]>("Intrinsics").ShouldBe([11, 3, 5, 4, 0, 69, 32065, 4, 51, 76]);

    [Fact]
    public void Run_WhenCharacterValuesArePassed_ShouldPassVariablesArraysElementsSubstringsAndConstants() =>
        sample.Program.Run<string>("Calls").ShouldBe("xyzQ??a??   x??   abc456783|3703613");
}
