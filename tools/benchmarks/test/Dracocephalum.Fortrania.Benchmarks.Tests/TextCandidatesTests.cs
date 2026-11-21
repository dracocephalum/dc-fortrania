using System.Text;
using Dracocephalum.Fortrania.Runtime;
using Shouldly;

namespace Dracocephalum.Fortrania.Benchmarks.Tests;

// A candidate is only worth measuring if it gives what the runtime gives,
// so each is held to the runtime's own answer here.
public unsafe class TextCandidatesTests
{
    [Theory]
    [InlineData(8, "EARTH")]
    [InlineData(5, "EARTH")]
    [InlineData(3, "EARTH")]
    [InlineData(8, "")]
    [InlineData(0, "EARTH")]
    public void Assign_WhenTheValuesDoNotOverlap_ShouldStoreWhatTheRuntimeStores(int targetLength, string source)
    {
        // Arrange
        var bytes = Encoding.ASCII.GetBytes(source);
        var expected = stackalloc byte[8];
        var actual = stackalloc byte[8];
        var control = stackalloc byte[8];
        new Span<byte>(expected, 8).Fill((byte)'?');
        new Span<byte>(actual, 8).Fill((byte)'?');
        new Span<byte>(control, 8).Fill((byte)'?');

        fixed (byte* value = bytes)
        {
            // Act
            Fortran.Assign(expected, targetLength, value, bytes.Length);
            TextCandidates.AssignDistinct(actual, targetLength, value, bytes.Length);
            TextCandidates.AssignAllowingOverlap(control, targetLength, value, bytes.Length);
        }

        // Assert
        Encoding.ASCII.GetString(actual, 8).ShouldBe(Encoding.ASCII.GetString(expected, 8));
        Encoding.ASCII.GetString(control, 8).ShouldBe(Encoding.ASCII.GetString(expected, 8));
    }
}
