using Dracocephalum.Fortrania.Emit;
using Shouldly;

namespace Dracocephalum.Fortrania.Tests.Emit;

public class StatementBuilderTests
{
    private static readonly TreeStatement Returned = new TreeStatement.Return(null);

    [Fact]
    public void End_WhenABlockWasBegun_ShouldGiveWhatWasAddedToItAndGoOnWithTheBlockAround()
    {
        // Arrange
        var sut = new StatementBuilder();
        sut.Add(Returned);

        // Act
        sut.Begin();
        sut.Line("// Inside.");
        var inner = sut.End();
        sut.Add(new TreeStatement.While(new TreeExpression.Local("more"), inner));

        // Assert
        inner.ShouldBe([new TreeStatement.Text("// Inside.")]);
        sut.Statements.Count.ShouldBe(2);
        sut.Statements[1].ShouldBeOfType<TreeStatement.While>().Body.ShouldBeSameAs(inner);
    }

    [Fact]
    public void Close_WhenABlockWasOpenedByItsHeading_ShouldAddItAsTextWithItsBody()
    {
        // Arrange
        var sut = new StatementBuilder();

        // Act
        sut.Open("fixed (byte* p = a)");
        sut.Add(Returned);
        sut.Close();

        // Assert
        var block = sut.Statements.ShouldHaveSingleItem().ShouldBeOfType<TreeStatement.TextBlock>();
        block.Heading.ShouldBe("fixed (byte* p = a)");
        block.Body.ShouldBe([Returned]);
    }

    [Fact]
    public void LastLine_WhenAskedAsStatementsAreAdded_ShouldSayWhatSourceTextWouldEndOn()
    {
        // Arrange
        var sut = new StatementBuilder();

        // Act & Assert
        sut.IsEmpty.ShouldBeTrue();
        sut.LastLine.ShouldBe(string.Empty);

        sut.Begin();
        sut.IsEmpty.ShouldBeFalse();
        sut.LastLine.ShouldBe("{");

        sut.Line("// A comment.");
        sut.LastLine.ShouldBe("// A comment.");

        sut.Blank();
        sut.LastLine.ShouldBe(string.Empty);

        sut.Add(Returned);
        sut.LastLine.ShouldBe("return;");

        sut.Add(new TreeStatement.If([new TreeStatement.Branch(null, sut.End())]));
        sut.LastLine.ShouldBe("}");
    }

    [Fact]
    public void StartsWithComment_WhenTheFirstThingIsAComment_ShouldSaySo()
    {
        // Arrange
        var commented = new StatementBuilder();
        var plain = new StatementBuilder();

        // Act
        commented.Line("// First.");
        commented.Add(Returned);
        plain.Add(Returned);
        plain.Line("// Last.");

        // Assert
        commented.StartsWithComment.ShouldBeTrue();
        plain.StartsWithComment.ShouldBeFalse();
        new StatementBuilder().StartsWithComment.ShouldBeFalse();
    }

    [Fact]
    public void Statements_WhenABlockIsStillOpen_ShouldRefuse()
    {
        // Arrange
        var sut = new StatementBuilder();
        sut.Begin();

        // Act & Assert
        Should.Throw<InvalidOperationException>(() => sut.Statements);
        Should.Throw<InvalidOperationException>(sut.Close);
    }
}
