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
    public void AssignDistinct_WhenTheValuesDoNotOverlap_ShouldStoreWhatAssignStores(int targetLength, string source)
    {
        // Arrange
        var bytes = Encoding.ASCII.GetBytes(source);
        var expected = stackalloc byte[8];
        var actual = stackalloc byte[8];
        new Span<byte>(expected, 8).Fill((byte)'?');
        new Span<byte>(actual, 8).Fill((byte)'?');

        fixed (byte* value = bytes)
        {
            // Act
            Fortran.Assign(expected, targetLength, value, bytes.Length);
            TextCandidates.AssignDistinct(actual, targetLength, value, bytes.Length);
        }

        // Assert
        Encoding.ASCII.GetString(actual, 8).ShouldBe(Encoding.ASCII.GetString(expected, 8));
    }

    [Theory]
    [InlineData("EARTH", "EARTH")]
    [InlineData("EARTH", "EARTH   ")]
    [InlineData("EARTH   ", "EARTH")]
    [InlineData("EARTH", "EARTHS")]
    [InlineData("EARTH", "MARS")]
    [InlineData("EARTH", "EARTG")]
    [InlineData("", "   ")]
    [InlineData("", "")]
    [InlineData("A\t", "A")]
    public void AreEqual_WhenGivenTwoValues_ShouldSayWhatCompareSays(string left, string right)
    {
        // Arrange
        var leftBytes = Encoding.ASCII.GetBytes(left);
        var rightBytes = Encoding.ASCII.GetBytes(right);
        bool expected;
        bool actual;

        fixed (byte* first = leftBytes)
        {
            fixed (byte* second = rightBytes)
            {
                // Act
                expected = Fortran.Compare(first, leftBytes.Length, second, rightBytes.Length) == 0;
                actual = TextCandidates.AreEqual(first, leftBytes.Length, second, rightBytes.Length);
            }
        }

        // Assert
        actual.ShouldBe(expected);
    }
}
