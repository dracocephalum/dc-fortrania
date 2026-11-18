using Shouldly;

namespace Dracocephalum.Fortrania.Runtime.Tests;

public unsafe class FortranTests
{
    [Fact]
    public void Arg_WhenGivenAnInteger_ShouldStoreItAndReturnTheSlot()
    {
        // Arrange
        var slot = 0;

        // Act
        var address = Fortran.Arg(&slot, 42);

        // Assert
        (address == &slot).ShouldBeTrue();
        slot.ShouldBe(42);
    }

    [Fact]
    public void Arg_WhenGivenAReal_ShouldStoreItAndReturnTheSlot()
    {
        // Arrange
        var slot = 0f;

        // Act
        var address = Fortran.Arg(&slot, 1.5f);

        // Assert
        (address == &slot).ShouldBeTrue();
        slot.ShouldBe(1.5f);
    }

    [Fact]
    public void Arg_WhenGivenADouble_ShouldStoreItAndReturnTheSlot()
    {
        // Arrange
        var slot = 0.0;

        // Act
        var address = Fortran.Arg(&slot, 2.5);

        // Assert
        (address == &slot).ShouldBeTrue();
        slot.ShouldBe(2.5);
    }

    // The expected values are what the reference compiler prints for the
    // same operands.
    [Theory]
    [InlineData(3, 13, 1594323)]
    [InlineData(-2, 7, -128)]
    [InlineData(2, -5, 0)]
    [InlineData(-1, -5, -1)]
    [InlineData(-1, -4, 1)]
    [InlineData(1, -5, 1)]
    [InlineData(7, 0, 1)]
    [InlineData(0, 0, 1)]
    [InlineData(46341, 2, -2147479015)]
    public void Pow_WhenBothAreIntegers_ShouldGiveTheIntegerPower(int value, int exponent, int expected) =>
        Fortran.Pow(value, exponent).ShouldBe(expected);

    [Fact]
    public void Pow_WhenZeroIsRaisedToANegativePower_ShouldThrow() =>
        Should.Throw<DivideByZeroException>(() => Fortran.Pow(0, -1));

    [Theory]
    [InlineData(1.1, 7, 1.94871710000000120E+00)]
    [InlineData(3.7, -5, 1.44208672718229764E-03)]
    [InlineData(0.3, 13, 1.59432299999999971E-07)]
    [InlineData(1.1, 0, 1.0)]
    [InlineData(3.7, 1, 3.7)]
    [InlineData(-0.3, 3, -2.69999999999999997E-02)]
    public void Pow_WhenADoubleIsRaisedToAnInteger_ShouldAgreeWithTheReferenceCompilerToTheLastBit(
        double value, int exponent, double expected) =>
        Fortran.Pow(value, exponent).ShouldBe(expected);

    [Theory]
    [InlineData(1.7f, 7, 4.103387070E+01f)]
    [InlineData(1.7f, -5, 7.042962313E-02f)]
    public void Pow_WhenARealIsRaisedToAnInteger_ShouldAgreeWithTheReferenceCompilerToTheLastBit(
        float value, int exponent, float expected) =>
        Fortran.Pow(value, exponent).ShouldBe(expected);

    [Fact]
    public void Pow_WhenTheExponentIsTheSmallestInteger_ShouldNotOverflowTakingItsMagnitude() =>
        Fortran.Pow(1.0, int.MinValue).ShouldBe(1.0);

    [Theory]
    [InlineData(2.5, 3)]
    [InlineData(-2.5, -3)]
    [InlineData(0.49999999999999994, 0)]
    [InlineData(2.4999, 2)]
    [InlineData(-0.4, 0)]
    public void Nint_WhenGivenADouble_ShouldRoundAHalfAwayFromZero(double value, int expected) =>
        Fortran.Nint(value).ShouldBe(expected);

    [Theory]
    [InlineData(1.5f, 2)]
    [InlineData(-0.5f, -1)]
    [InlineData(0.4f, 0)]
    public void Nint_WhenGivenAReal_ShouldRoundAHalfAwayFromZero(float value, int expected) =>
        Fortran.Nint(value).ShouldBe(expected);

    [Theory]
    [InlineData(5, 3, 5)]
    [InlineData(5, -3, -5)]
    [InlineData(-5, 3, 5)]
    [InlineData(-5, -3, -5)]
    [InlineData(5, 0, 5)]
    [InlineData(0, -3, 0)]
    public void Sign_WhenGivenIntegers_ShouldGiveTheFirstTheSignOfTheSecond(int magnitude, int sign, int expected) =>
        Fortran.Sign(magnitude, sign).ShouldBe(expected);

    [Fact]
    public void Dim_WhenTheFirstIsGreater_ShouldGiveTheDifference()
    {
        Fortran.Dim(7, 3).ShouldBe(4);
        Fortran.Dim(7.5f, 3f).ShouldBe(4.5f);
        Fortran.Dim(7.5, 3.0).ShouldBe(4.5);
    }

    [Fact]
    public void Dim_WhenTheFirstIsNotGreater_ShouldGiveZero()
    {
        Fortran.Dim(3, 7).ShouldBe(0);
        Fortran.Dim(3f, 3f).ShouldBe(0f);
        Fortran.Dim(-1.0, 2.0).ShouldBe(0.0);
    }
}
