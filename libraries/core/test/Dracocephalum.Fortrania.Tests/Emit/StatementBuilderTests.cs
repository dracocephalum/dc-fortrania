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
        sut.Comment("Inside.");
        var inner = sut.End();
        sut.Add(new TreeStatement.While(new TreeExpression.Local("more"), inner));

        // Assert
        inner.ShouldBe([new TreeStatement.Comment("Inside.")]);
        sut.Statements.Count.ShouldBe(2);
        sut.Statements[1].ShouldBeOfType<TreeStatement.While>().Body.ShouldBeSameAs(inner);
    }

    [Fact]
    public void IsBlankNeedless_WhenAskedAsStatementsAreAdded_ShouldHoldAtAStartAndAfterABlankLine()
    {
        // Arrange
        var sut = new StatementBuilder();

        // Act & Assert
        sut.IsEmpty.ShouldBeTrue();
        sut.IsBlankNeedless.ShouldBeTrue();

        sut.Begin();
        sut.IsEmpty.ShouldBeFalse();
        sut.IsBlankNeedless.ShouldBeTrue();

        sut.Comment("A comment.");
        sut.IsBlankNeedless.ShouldBeFalse();
        sut.EndsOnComment.ShouldBeTrue();

        sut.Blank();
        sut.IsBlankNeedless.ShouldBeTrue();
        sut.EndsOnComment.ShouldBeFalse();

        sut.Add(Returned);
        sut.IsBlankNeedless.ShouldBeFalse();
        sut.EndsOnComment.ShouldBeFalse();

        sut.Add(new TreeStatement.If([new TreeStatement.Branch(null, sut.End())]));
        sut.IsBlankNeedless.ShouldBeFalse();
    }

    [Fact]
    public void Statements_WhenABlockIsStillOpen_ShouldRefuse()
    {
        // Arrange
        var sut = new StatementBuilder();
        sut.Begin();

        // Act & Assert
        Should.Throw<InvalidOperationException>(() => sut.Statements);
        Should.Throw<InvalidOperationException>(() => new StatementBuilder().End());
    }
}
