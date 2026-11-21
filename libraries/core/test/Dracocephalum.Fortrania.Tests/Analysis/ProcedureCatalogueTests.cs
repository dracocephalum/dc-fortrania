using Dracocephalum.Fortrania.Analysis;
using Dracocephalum.Fortrania.Syntax;
using Shouldly;

namespace Dracocephalum.Fortrania.Tests.Analysis;

public class ProcedureCatalogueTests
{
    private static readonly ProcedureInterface First = new("FIRST", null, [], 1);
    private static readonly ProcedureInterface Second = new("SECOND", DataType.Integer, [], 5);

    [Fact]
    public void Find_WhenUnitWasAdded_ShouldGiveEachOfItsEntryPoints()
    {
        // Arrange
        var catalogue = new ProcedureCatalogue();
        catalogue.Add(new UnitSummary(UnitKind.Subroutine, "FIRST", [First, Second], []));

        // Act & Assert
        catalogue.Count.ShouldBe(2);
        catalogue.Find("FIRST").ShouldBeSameAs(First);
        catalogue.Find("SECOND").ShouldBeSameAs(Second);
        catalogue.Find("THIRD").ShouldBeNull();
    }

    [Fact]
    public void Add_WhenUnitIsAMainProgram_ShouldAddNothing()
    {
        // Arrange
        var catalogue = new ProcedureCatalogue();

        // Act
        catalogue.Add(new UnitSummary(UnitKind.MainProgram, "MAIN", [], []));

        // Assert
        catalogue.Count.ShouldBe(0);
        catalogue.Find("MAIN").ShouldBeNull();
    }

    [Fact]
    public void Add_WhenNameIsDefinedAlready_ShouldThrowWithTheLineOfTheSecond()
    {
        // Arrange
        var catalogue = new ProcedureCatalogue();
        catalogue.Add(new UnitSummary(UnitKind.Subroutine, "FIRST", [First], []));

        // Act & Assert
        var exception = Should.Throw<FortranAnalysisException>(
            () => catalogue.Add(new UnitSummary(UnitKind.Function, "SECOND", [Second, First with { Line = 9 }], [])));
        exception.Message.ShouldBe("Line 9: FIRST is defined by another program unit as well.");
    }
}
