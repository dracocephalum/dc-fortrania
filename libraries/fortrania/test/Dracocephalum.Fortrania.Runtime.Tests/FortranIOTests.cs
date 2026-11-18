using Shouldly;

namespace Dracocephalum.Fortrania.Runtime.Tests;

/// <summary>
/// The status values expected here are the reference compiler's for the
/// same statements, except where a test says the runtime goes its own way.
/// </summary>
public unsafe class FortranIOTests
{
    [Fact]
    public void Open_WhenTheFileIsToBeOldAndIsNotThere_ShouldSayNoSuchFile()
    {
        // Arrange
        using var io = new FortranIO();

        // Act
        var status = io.Open(11, file: Name(), status: "OLD"u8);

        // Assert
        status.ShouldBe(2);
        io.LastError.ShouldNotBeNull().ShouldContain("does not exist");
    }

    [Fact]
    public void Open_WhenTheFileIsToBeNewAndIsThere_ShouldSayItExists()
    {
        // Arrange
        using var io = new FortranIO();
        var name = Name();
        io.Open(11, file: name, status: "NEW"u8).ShouldBe(0);
        io.Close(11).ShouldBe(0);

        // Act
        var status = io.Open(11, file: name, status: "NEW"u8);

        // Assert
        status.ShouldBe(17);
        io.Open(11, file: name, status: "OLD"u8).ShouldBe(0);
        io.Close(11, status: "DELETE"u8).ShouldBe(0);
    }

    [Theory]
    [InlineData("WRONG", "", "", 8, 5002)]
    [InlineData("", "WRONG", "", 8, 5002)]
    [InlineData("", "", "WRONG", 8, 5002)]
    [InlineData("", "DIRECT", "", null, 5003)]
    [InlineData("", "DIRECT", "", 0, 5002)]
    public void Open_WhenASpecifierIsWrongOrMissing_ShouldRefuse(string status, string access, string form, int? recl, int expected)
    {
        // Arrange
        using var io = new FortranIO();

        // Act & Assert
        io.Open(11, Name(), Bytes(status), Bytes(access), Bytes(form), recl).ShouldBe(expected);
    }

    [Fact]
    public void Open_WhenTheUnitIsNegative_ShouldRefuse()
    {
        // Arrange
        using var io = new FortranIO();

        // Act & Assert
        io.Open(-1, file: Name()).ShouldBe(5002);
    }

    [Fact]
    public void Open_WhenSpecifiersAreInLowerCaseAndPadded_ShouldTakeThemAllTheSame()
    {
        // Arrange
        using var io = new FortranIO();
        var name = Name();

        // Act
        var status = io.Open(11, file: [.. name, (byte)' ', (byte)' '], status: "new "u8, access: "direct  "u8, recl: 16);

        // Assert
        status.ShouldBe(0);
        int opened;
        io.InquireFile(name, opened: &opened).ShouldBe(0);
        opened.ShouldBe(1);
        io.Close(11, status: "delete"u8).ShouldBe(0);
    }

    [Fact]
    public void Open_WhenTheUnitIsConnectedToAnotherFile_ShouldLetTheNewFileTakeTheUnitOver()
    {
        // Arrange
        using var io = new FortranIO();
        var first = Name();
        var second = Name();
        io.Open(11, file: first, status: "NEW"u8).ShouldBe(0);

        // Act
        var status = io.Open(11, file: second, status: "NEW"u8);

        // Assert
        status.ShouldBe(0);
        int opened;
        io.InquireFile(first, opened: &opened);
        opened.ShouldBe(0);
        io.Close(11, status: "DELETE"u8);
        io.Open(11, file: first, status: "OLD"u8).ShouldBe(0);
        io.Close(11, status: "DELETE"u8);
    }

    [Fact]
    public void Open_WhenTheUnitIsConnectedToThatFile_ShouldChangeNothingAndRefuseWhatWouldChangeTheConnection()
    {
        // Arrange
        using var io = new FortranIO();
        var name = Name();
        io.Open(11, file: name, status: "NEW"u8, access: "DIRECT"u8, recl: 32).ShouldBe(0);

        // Act & Assert
        io.Open(11, file: name, status: "OLD"u8, access: "DIRECT"u8, recl: 32).ShouldBe(0);
        io.Open(11).ShouldBe(0);
        io.Open(11, file: name, access: "DIRECT"u8, recl: 16).ShouldBe(5002);
        io.Open(11, status: "SCRATCH"u8).ShouldBe(5002);
        io.Close(11, status: "DELETE"u8);
    }

    [Fact]
    public void Open_WhenTheFileIsScratch_ShouldHaveNoNameAndBeGoneWhenClosed()
    {
        // Arrange
        using var io = new FortranIO();

        // Act
        var status = io.Open(14, status: "SCRATCH"u8, access: "DIRECT"u8, recl: 8);

        // Assert
        status.ShouldBe(0);
        int opened, named;
        io.InquireUnit(14, opened: &opened, named: &named).ShouldBe(0);
        opened.ShouldBe(1);
        named.ShouldBe(0);
        io.Close(14, status: "KEEP"u8).ShouldBe(5002);
        io.Close(14).ShouldBe(0);
        io.Open(15, file: Name(), status: "SCRATCH"u8).ShouldBe(5002);
    }

    [Fact]
    public void Open_WhenTheFileMayOnlyBeRead_ShouldConnectItForReadingAndRefuseAWrite()
    {
        // Arrange
        using var io = new FortranIO();
        var path = Path.Combine(AppContext.BaseDirectory, Guid.NewGuid().ToString("N") + ".bin");
        File.WriteAllBytes(path, [1, 0, 0, 0, 2, 0, 0, 0]);
        File.SetAttributes(path, FileAttributes.ReadOnly);

        try
        {
            // Act
            var status = io.Open(11, file: System.Text.Encoding.UTF8.GetBytes(path), status: "OLD"u8, access: "DIRECT"u8, recl: 4);

            // Assert
            status.ShouldBe(0);
            byte* four = stackalloc byte[4];
            io.ReadDirect(11, 2);
            io.Get(four, 4);
            io.EndRead().ShouldBe(0);
            four[0].ShouldBe((byte)2);

            // The reference compiler's number for this was not measured.
            io.WriteDirect(11, 1);
            io.Put(four, 4);
            io.EndWrite().ShouldBe(13);
            io.Close(11).ShouldBe(0);
        }
        finally
        {
            File.SetAttributes(path, FileAttributes.Normal);
            File.Delete(path);
        }
    }

    [Fact]
    public void Close_WhenTheUnitIsNotConnected_ShouldDoNothing()
    {
        // Arrange
        using var io = new FortranIO();

        // Act & Assert
        io.Close(19).ShouldBe(0);
        io.Close(19, status: "WRONG"u8).ShouldBe(5002);
    }

    [Fact]
    public void Close_WhenAskedToDelete_ShouldLeaveNoFile()
    {
        // Arrange
        using var io = new FortranIO();
        var name = Name();
        io.Open(11, file: name, status: "NEW"u8).ShouldBe(0);

        // Act
        var status = io.Close(11, status: "DELETE"u8);

        // Assert
        status.ShouldBe(0);
        int exist = 1;
        io.InquireFile(name, exist: &exist).ShouldBe(0);
        exist.ShouldBe(0);
    }

    [Fact]
    public void InquireFile_WhenTheFileIsConnected_ShouldSayToWhichUnitAndGiveItsName()
    {
        // Arrange
        using var io = new FortranIO();
        var name = Name();
        io.Open(211, file: name, status: "NEW"u8).ShouldBe(0);
        int exist, opened, number, named;
        byte* found = stackalloc byte[200];

        // Act
        var status = io.InquireFile(name, &exist, &opened, &number, found, 200, &named);

        // Assert
        status.ShouldBe(0);
        (exist, opened, number, named).ShouldBe((1, 1, 211, 1));
        new ReadOnlySpan<byte>(found, 200).TrimEnd((byte)' ').SequenceEqual(name).ShouldBeTrue();
        io.Close(211, status: "DELETE"u8);
    }

    [Fact]
    public void InquireFile_WhenTheFileIsNotThere_ShouldSaySoAndStillGiveTheName()
    {
        // Arrange
        using var io = new FortranIO();
        var name = Name();
        int exist = 1, opened = 1, number = 0, named = 0;
        byte* found = stackalloc byte[200];

        // Act
        var status = io.InquireFile(name, &exist, &opened, &number, found, 200, &named);

        // Assert
        status.ShouldBe(0);
        (exist, opened, number, named).ShouldBe((0, 0, -1, 1));
        new ReadOnlySpan<byte>(found, 200).TrimEnd((byte)' ').SequenceEqual(name).ShouldBeTrue();
    }

    [Fact]
    public void InquireUnit_WhenTheUnitIsNotConnected_ShouldSaySoAndLeaveTheNameAlone()
    {
        // Arrange
        using var io = new FortranIO();
        int exist = 0, opened = 1, number = 0, named = 1;
        byte* found = stackalloc byte[4];
        found[0] = (byte)'x';

        // Act
        var status = io.InquireUnit(21, &exist, &opened, &number, found, 4, &named);

        // Assert
        status.ShouldBe(0);
        (exist, opened, number, named).ShouldBe((1, 0, -1, 0));
        found[0].ShouldBe((byte)'x');
    }

    [Theory]
    [InlineData(0, 1, 1)]
    [InlineData(1, 1, 0)]
    [InlineData(5, 1, 1)]
    [InlineData(6, 1, 1)]
    [InlineData(7, 1, 0)]
    [InlineData(-3, 0, 0)]
    public void InquireUnit_WhenNothingHasBeenOpened_ShouldFindTheStandardUnitsConnected(int unit, int exists, int isOpened)
    {
        // Arrange
        using var io = new FortranIO();
        int exist, opened;

        // Act
        io.InquireUnit(unit, exist: &exist, opened: &opened).ShouldBe(0);

        // Assert
        (exist, opened).ShouldBe((exists, isOpened));
    }

    [Fact]
    public void Rewind_WhenTheUnitIsNotSequential_ShouldRefuse()
    {
        // Arrange
        using var io = new FortranIO();
        var name = Name();
        io.Open(11, file: name, status: "NEW"u8, access: "DIRECT"u8, recl: 8).ShouldBe(0);
        io.Open(12, file: Name(), status: "NEW"u8).ShouldBe(0);

        // Act & Assert
        io.Rewind(11).ShouldBe(5002);
        io.Rewind(12).ShouldBe(0);
        io.Rewind(19).ShouldBe(0);
        io.Close(11, status: "DELETE"u8);
        io.Close(12, status: "DELETE"u8);
    }

    [Fact]
    public void WriteDirect_WhenRecordsAreWrittenOutOfOrder_ShouldReadEachBackAndFindZerosBetween()
    {
        // Arrange
        using var stream = new MemoryStream();
        using var io = new FortranIO();
        io.Register("memory", stream);
        io.Open(11, file: "memory"u8, access: "DIRECT"u8, recl: 16).ShouldBe(0);
        double value = 2.5;
        int number = 7;

        // Act
        io.WriteDirect(11, 3);
        io.Put(&value, 8);
        io.Put(&number, 4);
        io.EndWrite().ShouldBe(0);

        // Assert
        stream.Length.ShouldBe(48);
        double readValue = 0;
        int readNumber = 0, rest = -1;
        io.ReadDirect(11, 3);
        io.Get(&readValue, 8);
        io.Get(&readNumber, 4);
        io.Get(&rest, 4);
        io.EndRead().ShouldBe(0);
        (readValue, readNumber, rest).ShouldBe((2.5, 7, 0));

        long* hole = stackalloc long[2];
        hole[0] = hole[1] = -1;
        io.ReadDirect(11, 2);
        io.Get(hole, 16);
        io.EndRead().ShouldBe(0);
        (hole[0], hole[1]).ShouldBe((0L, 0L));
    }

    [Fact]
    public void ReadDirect_WhenTheStatementCannotBeDone_ShouldGiveTheStatusAndLeaveTheItemsAlone()
    {
        // Arrange
        using var stream = new MemoryStream(new byte[32]);
        using var io = new FortranIO();
        io.Register("memory", stream);
        io.Open(11, file: "memory"u8, access: "DIRECT"u8, recl: 16).ShouldBe(0);
        io.Open(12, file: Name(), status: "NEW"u8).ShouldBe(0);
        long* items = stackalloc long[3];
        items[0] = 99;

        // Act & Assert
        Read(io, 19, 1, items, 8).ShouldBe(5001);
        Read(io, 12, 1, items, 8).ShouldBe(5001);
        Read(io, 11, 0, items, 8).ShouldBe(5002);
        Read(io, 11, 3, items, 8).ShouldBe(5002);
        Read(io, 11, 1, items, 24).ShouldBe(5016);
        items[0].ShouldBe(99);
        Read(io, 11, 2, items, 16).ShouldBe(0);
        items[0].ShouldBe(0);
        io.Close(12, status: "DELETE"u8);
    }

    [Fact]
    public void WriteDirect_WhenTheItemsDoNotFitTheRecord_ShouldWriteNothing()
    {
        // Arrange
        using var stream = new MemoryStream();
        using var io = new FortranIO();
        io.Register("memory", stream);
        io.Open(11, file: "memory"u8, access: "DIRECT"u8, recl: 8).ShouldBe(0);
        long* items = stackalloc long[2];

        // Act
        io.WriteDirect(11, 1);
        io.Put(items, 16);
        var status = io.EndWrite();

        // Assert
        status.ShouldBe(5015);
        stream.Length.ShouldBe(0);
    }

    [Fact]
    public void ReadDirect_WhenTheFileEndsInsideTheLastRecord_ShouldReadWhatIsThereFollowedByZeros()
    {
        // Arrange
        using var stream = new MemoryStream([1, 2, 3, 4, 5, 6, 7, 8, 9, 10]);
        using var io = new FortranIO();
        io.Register("memory", stream);
        io.Open(11, file: "memory"u8, access: "DIRECT"u8, recl: 8).ShouldBe(0);
        byte* record = stackalloc byte[8];
        new Span<byte>(record, 8).Fill(0xff);

        // Act
        io.ReadDirect(11, 2);
        io.Get(record, 8);
        var status = io.EndRead();

        // Assert
        status.ShouldBe(0);
        new ReadOnlySpan<byte>(record, 8).ToArray().ShouldBe([9, 10, 0, 0, 0, 0, 0, 0]);
    }

    [Fact]
    public void Open_WhenRecordLengthsCountWords_ShouldMakeARecordFourTimesAsLong()
    {
        // Arrange
        using var stream = new MemoryStream();
        using var io = new FortranIO { RecordLengthUnit = 4 };
        io.Register("memory", stream);
        io.Open(11, file: "memory"u8, access: "DIRECT"u8, recl: 4).ShouldBe(0);
        long* items = stackalloc long[2];

        // Act
        io.WriteDirect(11, 2);
        io.Put(items, 16);
        var status = io.EndWrite();

        // Assert
        status.ShouldBe(0);
        stream.Length.ShouldBe(32);
    }

    [Fact]
    public void Register_WhenAStreamHasAName_ShouldLetItBeInquiredAboutOpenedAndLeftOpenOnClose()
    {
        // Arrange
        using var stream = new MemoryStream(new byte[8]);
        using var io = new FortranIO();
        io.Register("kernel.bsp", stream);
        int exist, opened;

        // Act & Assert
        io.InquireFile("kernel.bsp"u8, exist: &exist, opened: &opened).ShouldBe(0);
        (exist, opened).ShouldBe((1, 0));
        io.Open(11, file: "kernel.bsp"u8, status: "NEW"u8).ShouldBe(17);
        io.Open(11, file: "kernel.bsp"u8, status: "OLD"u8, access: "DIRECT"u8, recl: 8).ShouldBe(0);
        io.Close(11).ShouldBe(0);
        stream.CanRead.ShouldBeTrue();
        io.Unregister("kernel.bsp").ShouldBeTrue();
        io.Unregister("kernel.bsp").ShouldBeFalse();
    }

    [Fact]
    public void Register_WhenBytesHaveAName_ShouldLetThemBeReadAndNotWritten()
    {
        // Arrange
        byte[] bytes = [1, 2, 3, 4, 5, 6, 7, 8];
        using var io = new FortranIO();
        io.Register("kernel.bsp", bytes);
        int exist;
        byte* record = stackalloc byte[4];

        // Act & Assert
        io.InquireFile("kernel.bsp"u8, exist: &exist).ShouldBe(0);
        exist.ShouldBe(1);
        io.Open(11, file: "kernel.bsp"u8, status: "NEW"u8).ShouldBe(17);
        io.Open(11, file: "kernel.bsp"u8, status: "REPLACE"u8).ShouldBe(13);
        io.Open(11, file: "kernel.bsp"u8, status: "OLD"u8, access: "DIRECT"u8, recl: 4).ShouldBe(0);
        Read(io, 11, 2, record, 4).ShouldBe(0);
        new ReadOnlySpan<byte>(record, 4).ToArray().ShouldBe([5, 6, 7, 8]);
        Read(io, 11, 3, record, 4).ShouldBe(5002);
        io.WriteDirect(11, 1);
        io.Put(record, 4);
        io.EndWrite().ShouldBe(13);
        io.Close(11).ShouldBe(0);
        bytes.ShouldBe([1, 2, 3, 4, 5, 6, 7, 8]);
    }

    [Fact]
    public void Register_WhenTheSameBytesAreGivenToManyInstances_ShouldLetAllReadThemAtOnce()
    {
        // Arrange
        const int Records = 4096;
        var bytes = new byte[Records * sizeof(int)];
        for (var record = 0; record < Records; record++)
        {
            BitConverter.TryWriteBytes(bytes.AsSpan(record * sizeof(int)), record + 1);
        }

        // Act
        var wrong = 0;
        Parallel.For(0, 8, _ =>
        {
            using var io = new FortranIO();
            io.Register("shared", bytes);
            io.Open(11, file: "shared"u8, status: "OLD"u8, access: "DIRECT"u8, recl: sizeof(int)).ShouldBe(0);
            for (var record = 1; record <= Records; record++)
            {
                int value;
                if (Read(io, 11, record, &value, sizeof(int)) != 0 || value != record)
                {
                    Interlocked.Increment(ref wrong);
                }
            }
        });

        // Assert
        wrong.ShouldBe(0);
    }

    [Fact]
    public void Register_WhenTheStreamCannotSeek_ShouldThrow()
    {
        // Arrange
        using var io = new FortranIO();
        using var stream = new System.IO.Compression.DeflateStream(new MemoryStream(), System.IO.Compression.CompressionMode.Decompress);

        // Act & Assert
        Should.Throw<ArgumentException>(() => io.Register("x", stream));
    }

    [Fact]
    public void Require_WhenTheStatusIsNotZero_ShouldThrowWithItAndWhatWentWrong()
    {
        // Arrange
        using var io = new FortranIO();
        var status = io.Open(11, file: Name(), status: "OLD"u8);

        // Act
        var exception = Should.Throw<FortranIOException>(() => io.Require(status));

        // Assert
        exception.Status.ShouldBe(2);
        exception.Message.ShouldContain("does not exist");
        Should.NotThrow(() => io.Require(0));
    }

    [Fact]
    public void Dispose_WhenUnitsAreConnected_ShouldCloseThemAndRefuseWhatComesAfter()
    {
        // Arrange
        var io = new FortranIO();
        var name = Name();
        io.Open(11, file: name, status: "NEW"u8).ShouldBe(0);

        // Act
        io.Dispose();
        io.Dispose();

        // Assert
        Should.Throw<ObjectDisposedException>(() => io.Open(12));
        File.Delete(System.Text.Encoding.UTF8.GetString(name));
    }

    private static int Read(FortranIO io, int unit, int record, void* address, int bytes)
    {
        io.ReadDirect(unit, record);
        io.Get(address, bytes);
        return io.EndRead();
    }

    private static byte[] Name() =>
        System.Text.Encoding.UTF8.GetBytes(Path.Combine(AppContext.BaseDirectory, Guid.NewGuid().ToString("N") + ".bin"));

    private static byte[] Bytes(string text) => System.Text.Encoding.ASCII.GetBytes(text);
}
