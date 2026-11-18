using System.Text;
using Shouldly;

namespace Dracocephalum.Fortrania.Runtime.Tests;

public unsafe class FortranTextTests
{
    [Fact]
    public void AddrOf_WhenGivenALiteral_ShouldGiveAnAddressThatReadsBackAsItsBytes()
    {
        // Act
        var address = Fortran.AddrOf("SPKEZ"u8);

        // Assert
        Encoding.ASCII.GetString(address, 5).ShouldBe("SPKEZ");
    }

    [Fact]
    public void AddrOf_WhenGivenTheSameLiteralAgainAfterACollection_ShouldGiveAnAddressThatStillReads()
    {
        // Arrange
        var address = Fortran.AddrOf("UNMOVED"u8);

        // Act
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);

        // Assert
        Encoding.ASCII.GetString(address, 7).ShouldBe("UNMOVED");
    }

    [Fact]
    public void Arg_WhenGivenACharacterCode_ShouldStoreItsByteAndReturnTheSlot()
    {
        // Arrange
        byte slot = 0;

        // Act
        var address = Fortran.Arg(&slot, 65);

        // Assert
        (address == &slot).ShouldBeTrue();
        slot.ShouldBe((byte)'A');
    }

    [Theory]
    [InlineData("#####", "AB", "AB   ")]
    [InlineData("#####", "ABCDE", "ABCDE")]
    [InlineData("#####", "ABCDEFG", "ABCDE")]
    [InlineData("#####", "", "     ")]
    public void Assign_WhenLengthsDiffer_ShouldCutOrPadWithBlanks(string before, string value, string expected)
    {
        // Arrange
        var target = Encoding.ASCII.GetBytes(before);
        var source = Encoding.ASCII.GetBytes(value + "!");

        // Act
        fixed (byte* to = target, from = source)
        {
            Fortran.Assign(to, target.Length, from, value.Length);
        }

        // Assert
        Encoding.ASCII.GetString(target).ShouldBe(expected);
    }

    [Fact]
    public void Assign_WhenTargetHasNoLength_ShouldStoreNothing()
    {
        // Arrange
        var target = Encoding.ASCII.GetBytes("##");

        // Act
        fixed (byte* to = target)
        {
            Fortran.Assign(to, 0, Fortran.AddrOf("AB"u8), 2);
            Fortran.Assign(to, -3, Fortran.AddrOf("AB"u8), 2);
        }

        // Assert
        Encoding.ASCII.GetString(target).ShouldBe("##");
    }

    [Fact]
    public void Assign_WhenSourceLengthIsNegative_ShouldFillWithBlanks()
    {
        // Arrange
        var target = Encoding.ASCII.GetBytes("###");

        // Act
        fixed (byte* to = target)
        {
            Fortran.Assign(to, 3, Fortran.AddrOf("AB"u8), -1);
        }

        // Assert
        Encoding.ASCII.GetString(target).ShouldBe("   ");
    }

    [Theory]
    [InlineData(0, 2, "ABABCDGH")]
    [InlineData(2, 0, "CDEFEFGH")]
    public void Assign_WhenSourceOverlapsTarget_ShouldCopyAsIfThroughACopy(int from, int to, string expected)
    {
        // Arrange
        var text = Encoding.ASCII.GetBytes("ABCDEFGH");

        // Act
        fixed (byte* first = text)
        {
            Fortran.Assign(first + to, 4, first + from, 4);
        }

        // Assert
        Encoding.ASCII.GetString(text).ShouldBe(expected);
    }

    [Theory]
    [InlineData("ABC", "ABC", 0)]
    [InlineData("ABC", "ABC  ", 0)]
    [InlineData("ABC  ", "ABC", 0)]
    [InlineData("", "   ", 0)]
    [InlineData("ABC", "ABD", -1)]
    [InlineData("ABD", "ABC", 1)]
    [InlineData("AB", "ABC", -1)]
    [InlineData("ABC", "AB", 1)]
    [InlineData("AB", "AB\t", 1)]
    [InlineData("AB\t", "AB", -1)]
    [InlineData("b", "ABC", 1)]
    public void Compare_WhenGivenTwoValues_ShouldOrderThemWithTheShorterPaddedByBlanks(string left, string right, int expected)
    {
        // Arrange
        var a = Encoding.ASCII.GetBytes(left + "x");
        var b = Encoding.ASCII.GetBytes(right + "y");

        // Act
        int order;
        fixed (byte* first = a, second = b)
        {
            order = Fortran.Compare(first, left.Length, second, right.Length);
        }

        // Assert
        Math.Sign(order).ShouldBe(expected);
    }

    [Fact]
    public void Compare_WhenALengthIsNegative_ShouldTakeTheValueAsEmpty() =>
        Fortran.Compare(Fortran.AddrOf("A"u8), -2, Fortran.AddrOf("  "u8), 2).ShouldBe(0);

    [Theory]
    [InlineData("HELLO WORLD", "LO", 4)]
    [InlineData("HELLO WORLD", "H", 1)]
    [InlineData("HELLO WORLD", "D", 11)]
    [InlineData("HELLO WORLD", "ZZ", 0)]
    [InlineData("HELLO WORLD", "HELLO WORLD", 1)]
    [InlineData("AB", "ABC", 0)]
    public void Index_WhenGivenTextAndWhatIsSought_ShouldGiveItsFirstPositionOrZero(string text, string sought, int expected)
    {
        // Arrange
        var a = Encoding.ASCII.GetBytes(text);
        var b = Encoding.ASCII.GetBytes(sought);

        // Act
        int position;
        fixed (byte* first = a, second = b)
        {
            position = Fortran.Index(first, a.Length, second, b.Length);
        }

        // Assert
        position.ShouldBe(expected);
    }

    [Fact]
    public void Index_WhenWhatIsSoughtHasNoLength_ShouldGiveZero() =>
        Fortran.Index(Fortran.AddrOf("AB"u8), 2, Fortran.AddrOf("A"u8), 0).ShouldBe(0);
}
