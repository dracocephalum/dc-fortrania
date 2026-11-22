using Dracocephalum.Fortrania.Emit;
using Shouldly;

namespace Dracocephalum.Fortrania.Tests.Emit;

public class TextBlockTests
{
    [Fact]
    public void Add_WhenConstantsDiffer_ShouldStartEachOnAMultipleOfEightWithAZeroByteBeforeIt()
    {
        // Arrange
        var block = new TextBlock();

        // Act: seven bytes leave room for one zero, eight for none, so
        // the second is given eight of its own.
        var first = block.Add("1234567");
        var second = block.Add("12345678");
        var third = block.Add("x");

        // Assert
        first.ShouldBe(0);
        second.ShouldBe(8);
        third.ShouldBe(24);
        block.ToString().ShouldBe("1234567\0" + "12345678\0\0\0\0\0\0\0\0" + "x\0\0\0\0\0\0\0");
    }

    [Fact]
    public void Add_WhenAConstantOccursAgain_ShouldGiveThePlaceItHasAndAddNothing()
    {
        // Arrange
        var block = new TextBlock();
        block.Add("one");
        var place = block.Add("two");
        var length = block.ToString().Length;

        // Act
        var again = block.Add("two");

        // Assert
        again.ShouldBe(place);
        block.ToString().Length.ShouldBe(length);
    }

    [Fact]
    public void IsEmpty_WhenNothingHasBeenAdded_ShouldBeTrueAndFalseAfterwards()
    {
        // Arrange
        var block = new TextBlock();

        // Act
        var before = block.IsEmpty;
        block.Add("a");

        // Assert
        before.ShouldBeTrue();
        block.IsEmpty.ShouldBeFalse();
    }
}
