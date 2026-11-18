using System.Text;
using Shouldly;

namespace Dracocephalum.Fortrania.Runtime.Tests;

public unsafe class ConcatenationTests
{
    [Fact]
    public void Pad_WhenValuesLeaveRoom_ShouldJoinThemAndFillTheRestWithBlanks()
    {
        // Arrange
        var target = Encoding.ASCII.GetBytes("##########");

        // Act
        fixed (byte* to = target)
        {
            Fortran.Concat(to, 8)
                .Add(Fortran.AddrOf("AB"u8), 2)
                .Add(Fortran.AddrOf("CDE"u8), 3)
                .Pad();
        }

        // Assert: nothing is written past the capacity.
        Encoding.ASCII.GetString(target).ShouldBe("ABCDE   ##");
    }

    [Fact]
    public void Pad_WhenValuesDoNotFit_ShouldDropWhatIsOver()
    {
        // Arrange
        var target = Encoding.ASCII.GetBytes("######");

        // Act
        fixed (byte* to = target)
        {
            Fortran.Concat(to, 4)
                .Add(Fortran.AddrOf("ABC"u8), 3)
                .Add(Fortran.AddrOf("DEF"u8), 3)
                .Add(Fortran.AddrOf("G"u8), 1)
                .Pad();
        }

        // Assert
        Encoding.ASCII.GetString(target).ShouldBe("ABCD##");
    }

    [Fact]
    public void Pad_WhenNothingIsAdded_ShouldFillWithBlanks()
    {
        // Arrange
        var target = Encoding.ASCII.GetBytes("###");

        // Act
        fixed (byte* to = target)
        {
            Fortran.Concat(to, 3).Pad();
        }

        // Assert
        Encoding.ASCII.GetString(target).ShouldBe("   ");
    }

    [Fact]
    public void Add_WhenALengthIsBelowOne_ShouldAddNothing()
    {
        // Arrange
        var target = Encoding.ASCII.GetBytes("####");
        var length = -1;

        // Act
        fixed (byte* to = target)
        {
            Fortran.Concat(to, 4)
                .Add(Fortran.AddrOf("AB"u8), 0)
                .Add(Fortran.AddrOf("AB"u8), -5)
                .Add(Fortran.AddrOf("XY"u8), 2)
                .End(&length);
        }

        // Assert
        length.ShouldBe(2);
        Encoding.ASCII.GetString(target).ShouldBe("XY##");
    }

    [Fact]
    public void End_WhenValuesAreJoined_ShouldGiveTheAddressAndStoreTheLengthWithoutPadding()
    {
        // Arrange
        var target = Encoding.ASCII.GetBytes("########");
        var length = 0;
        bool isTarget;

        // Act
        fixed (byte* to = target)
        {
            var address = Fortran.Concat(to, 8)
                .Add(Fortran.AddrOf("AB"u8), 2)
                .Add(Fortran.AddrOf("C"u8), 1)
                .End(&length);
            isTarget = address == to;
        }

        // Assert
        isTarget.ShouldBeTrue();
        length.ShouldBe(3);
        Encoding.ASCII.GetString(target).ShouldBe("ABC#####");
    }

    [Fact]
    public void End_WhenValuesDoNotFit_ShouldStoreTheCapacityAsTheLength()
    {
        // Arrange
        var target = Encoding.ASCII.GetBytes("###");
        var length = 0;

        // Act
        fixed (byte* to = target)
        {
            Fortran.Concat(to, 2).Add(Fortran.AddrOf("ABCD"u8), 4).End(&length);
        }

        // Assert
        length.ShouldBe(2);
        Encoding.ASCII.GetString(target).ShouldBe("AB#");
    }

    [Fact]
    public void Concat_WhenTheCapacityIsBelowOne_ShouldHoldNothing()
    {
        // Arrange
        var target = Encoding.ASCII.GetBytes("##");
        var length = 7;

        // Act
        fixed (byte* to = target)
        {
            Fortran.Concat(to, -3).Add(Fortran.AddrOf("AB"u8), 2).End(&length);
            Fortran.Concat(to, 0).Add(Fortran.AddrOf("AB"u8), 2).Pad();
        }

        // Assert
        length.ShouldBe(0);
        Encoding.ASCII.GetString(target).ShouldBe("##");
    }
}
