using Shouldly;

namespace Dracocephalum.Fortrania.Tests.Emit;

/// <summary>
/// Runs the translated numeric sample. Every expected value is what the
/// reference compiler's program printed for the same source and arguments.
/// </summary>
public class CSharpEmitterNumericTests(NumericSampleFixture sample) : IClassFixture<NumericSampleFixture>
{
    // The sums take in powers, logarithms and the like from the platform's
    // mathematical library, which may differ from the reference compiler's
    // in the last place.
    private const double LibraryTolerance = 1e-13;

    [Fact]
    public void Run_WhenExpressionMixesTypesOperatorsAndIntrinsics_ShouldGiveTheReferenceValue()
    {
        sample.Program.Run<double>("ArithOfTwoAndAHalf").ShouldBe(6.8684053292440296E+01, LibraryTolerance);
        sample.Program.Run<double>("ArithOfThreeTenths").ShouldBe(3.4967888498515158E+00, LibraryTolerance);
    }

    [Fact]
    public void Run_WhenArithmeticIsOnIntegers_ShouldTruncateAndRoundAsTheLanguageDoes() =>
        sample.Program.Run<int[]>("IntegerArith").ShouldBe([70, 6592]);

    [Fact]
    public void Run_WhenArithmeticIsInSinglePrecision_ShouldStayInSinglePrecision() =>
        sample.Program.Run<float>("RealArith").ShouldBe(2.95979080E+01f, 1e-5f);

    [Fact]
    public void Run_WhenLoopsCountUpDownByAVariableAndInReals_ShouldRunAndEndAsTheLanguageDefines() =>
        sample.Program.Run<int[]>("LoopsOfTen").ShouldBe([55, 11, 30, 0, 5, 22, 5, 50]);

    [Fact]
    public void Run_WhenALoopHasNothingToCount_ShouldNotRunItAndShouldLeaveTheFirstValue() =>
        sample.Program.Run<int[]>("LoopsOfNone").ShouldBe([0, 1, 0, 0, 5, 0, 5, 50]);

    [Fact]
    public void Run_WhenArraysHaveSeveralDimensionsAndOtherLowerBounds_ShouldReachTheRightElements() =>
        sample.Program.Run<double[]>("Arrays").ShouldBe([-1119.5, 21.0]);

    [Fact]
    public void Run_WhenProceduresCallOneAnother_ShouldPassVariablesElementsAndValuesByAddress()
    {
        var results = sample.Program.Run<double[]>("Calls");

        results[0].ShouldBe(5.8391199073017769E+03, LibraryTolerance);
        results[1].ShouldBe(4.0);
        results[2].ShouldBe(5.0);
    }

    [Fact]
    public void Run_WhenControlBranchesAndLoopsOnAConditionAndReturnsEarly_ShouldFollowTheSource() =>
        sample.Program.Run<int[]>("Branch").ShouldBe([-1, 0, 1112, 112, 1001]);
}
