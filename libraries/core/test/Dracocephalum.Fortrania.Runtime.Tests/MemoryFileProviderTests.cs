using Shouldly;

namespace Dracocephalum.Fortrania.Runtime.Tests;

public class MemoryFileProviderTests
{
    [Fact]
    public void Open_WhenAFileWasWrittenAndClosed_ShouldGiveItBackAsItWasLeft()
    {
        // Arrange
        var provider = new MemoryFileProvider();
        using (var written = provider.Open("a.dat", FileMode.CreateNew))
        {
            written.Write(0, [1, 2, 3]);
        }

        // Act
        using var file = provider.Open("a.dat", FileMode.Open);
        var buffer = new byte[8];
        var read = file.Read(0, buffer);

        // Assert
        read.ShouldBe(3);
        buffer[..3].ShouldBe([1, 2, 3]);
        file.Length.ShouldBe(3);
        file.CanWrite.ShouldBeTrue();
    }

    [Fact]
    public void Open_WhenTheFileIsToBeThereAndIsNot_ShouldSayItIsNotFound()
    {
        // Arrange
        var provider = new MemoryFileProvider();

        // Act & Assert
        Should.Throw<FileNotFoundException>(() => provider.Open("a.dat", FileMode.Open));
        provider.Exists("a.dat").ShouldBeFalse();
    }

    [Fact]
    public void Open_WhenTheFileIsToBeNewAndIsThere_ShouldRefuse()
    {
        // Arrange
        var provider = new MemoryFileProvider();
        provider.Add("a.dat", [1]);

        // Act & Assert
        Should.Throw<IOException>(() => provider.Open("a.dat", FileMode.CreateNew));
    }

    [Fact]
    public void Open_WhenTheFileIsReplacedWhileAnotherConnectionHoldsIt_ShouldEmptyTheSameFile()
    {
        // Arrange
        var provider = new MemoryFileProvider();
        provider.Add("a.dat", [1, 2, 3]);
        using var held = provider.Open("a.dat", FileMode.Open);

        // Act
        using var replaced = provider.Open("a.dat", FileMode.Create);
        replaced.Write(0, [9]);

        // Assert
        held.Length.ShouldBe(1);
        var buffer = new byte[1];
        held.Read(0, buffer).ShouldBe(1);
        buffer[0].ShouldBe((byte)9);
    }

    [Fact]
    public void Write_WhenItStartsPastTheEnd_ShouldFillWhatLiesBetweenWithZeros()
    {
        // Arrange
        var provider = new MemoryFileProvider();
        using var file = provider.Open("a.dat", FileMode.OpenOrCreate);
        file.Write(0, [7, 7, 7, 7]);
        file.Truncate(1);

        // Act
        file.Write(3, [5]);

        // Assert
        provider.TryGetContents("a.dat", out var bytes).ShouldBeTrue();
        bytes.ToArray().ShouldBe([7, 0, 0, 5]);
    }

    [Fact]
    public void Write_WhenTheFileGrowsARecordAtATime_ShouldKeepEveryRecord()
    {
        // Arrange
        var provider = new MemoryFileProvider();
        using var file = provider.Open("a.dat", FileMode.OpenOrCreate);
        var record = new byte[100];

        // Act
        for (var index = 0; index < 50; index++)
        {
            record.AsSpan().Fill((byte)index);
            file.Write(index * 100, record);
        }

        // Assert
        file.Length.ShouldBe(5000);
        var last = new byte[100];
        file.Read(4900, last).ShouldBe(100);
        last.ShouldAllBe(value => value == 49);
        file.Read(5000, last).ShouldBe(0);
    }

    [Fact]
    public void Delete_WhenAConnectionStillHoldsTheFile_ShouldTakeTheNameAndLeaveTheConnection()
    {
        // Arrange
        var provider = new MemoryFileProvider();
        provider.Add("a.dat", [4]);
        using var held = provider.Open("a.dat", FileMode.Open);

        // Act
        provider.Delete("a.dat");

        // Assert
        provider.Exists("a.dat").ShouldBeFalse();
        provider.Count.ShouldBe(0);
        held.Length.ShouldBe(1);
    }

    [Fact]
    public void OpenScratch_WhenCalled_ShouldGiveAFileWithNoName()
    {
        // Arrange
        var provider = new MemoryFileProvider();

        // Act
        using var scratch = provider.OpenScratch();
        scratch.Write(0, [1]);

        // Assert
        provider.Count.ShouldBe(0);
        scratch.Length.ShouldBe(1);
    }

    [Fact]
    public void Open_WhenAProgramWritesAFileAndReadsItBack_ShouldNeverTouchADisk()
    {
        // Arrange
        var provider = new MemoryFileProvider();
        using var io = new FortranIO(provider);
        var name = "only-in-memory.txt"u8;

        // Act
        io.Open(11, file: name, status: "NEW"u8).ShouldBe(0);
        io.Close(11).ShouldBe(0);
        var again = io.Open(11, file: name, status: "NEW"u8);
        var old = io.Open(12, file: name, status: "OLD"u8);
        io.Close(12, status: "DELETE"u8).ShouldBe(0);

        // Assert
        again.ShouldBe(17);
        old.ShouldBe(0);
        provider.Exists("only-in-memory.txt").ShouldBeFalse();
        File.Exists("only-in-memory.txt").ShouldBeFalse();
    }
}
