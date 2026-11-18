using Dracocephalum.Fortrania.Analysis;
using Dracocephalum.Fortrania.Syntax;
using Shouldly;

namespace Dracocephalum.Fortrania.Tests.Analysis;

public class ArgumentEffectsTests
{
    private const ArgumentUses None = ArgumentUses.None;
    private const ArgumentUses Read = ArgumentUses.Read;
    private const ArgumentUses Written = ArgumentUses.Written;
    private const ArgumentUses Both = ArgumentUses.Read | ArgumentUses.Written;

    [Fact]
    public void Compute_WhenProcedurePassesNothingOn_ShouldGiveWhatItDoesItself()
    {
        // Act
        var effects = Compute(
            "      SUBROUTINE LEAF ( IN, OUT, BOTH, IDLE, PROC )",
            "      INTEGER IN, OUT, BOTH, IDLE",
            "      EXTERNAL PROC",
            "      OUT = IN",
            "      BOTH = BOTH + 1",
            "      END");

        // Assert
        effects.Find("LEAF").ShouldBe([Read, Written, Both, None, None]);
    }

    [Fact]
    public void Compute_WhenArgumentIsPassedDownAChain_ShouldCarryTheEffectToTheTop()
    {
        // Act: TOP is written before the units it depends on, so that the
        // answer cannot come from the order of reading.
        var effects = Compute(
            "      SUBROUTINE TOP ( A, B, C )",
            "      INTEGER A, B, C",
            "      CALL MIDDLE ( B, A )",
            "      CALL MIDDLE ( C, C )",
            "      END",
            "      SUBROUTINE MIDDLE ( P, Q )",
            "      INTEGER P, Q",
            "      CALL BOTTOM ( Q, P )",
            "      END",
            "      SUBROUTINE BOTTOM ( IN, OUT )",
            "      INTEGER IN, OUT",
            "      OUT = IN",
            "      END");

        // Assert
        effects.Find("BOTTOM").ShouldBe([Read, Written]);
        effects.Find("MIDDLE").ShouldBe([Written, Read]);
        effects.Find("TOP").ShouldBe([Read, Written, Both]);
    }

    [Fact]
    public void Compute_WhenArgumentIsBothUsedAndPassedOn_ShouldJoinTheTwo()
    {
        // Act
        var effects = Compute(
            "      SUBROUTINE CALLER ( X )",
            "      INTEGER X, IFUN, J",
            "      J = X + IFUN ( X )",
            "      END",
            "      INTEGER FUNCTION IFUN ( N )",
            "      INTEGER N",
            "      N = 0",
            "      IFUN = 1",
            "      END");

        // Assert
        effects.Find("CALLER").ShouldBe([Both]);
    }

    [Fact]
    public void Compute_WhenEntriesDiffer_ShouldAnswerForEachByItsOwnName()
    {
        // Act
        var effects = Compute(
            "      SUBROUTINE STORE ( X )",
            "      INTEGER X, SAVED",
            "      SAVE SAVED",
            "      RETURN",
            "      ENTRY PUT ( X )",
            "      SAVED = X",
            "      RETURN",
            "      ENTRY GET ( X )",
            "      X = SAVED",
            "      END",
            "      SUBROUTINE SETTER ( V )",
            "      INTEGER V",
            "      CALL PUT ( V )",
            "      END",
            "      SUBROUTINE GETTER ( V )",
            "      INTEGER V",
            "      CALL GET ( V )",
            "      END");

        // Assert
        effects.Find("STORE").ShouldBe([None]);
        effects.Find("SETTER").ShouldBe([Read]);
        effects.Find("GETTER").ShouldBe([Written]);
    }

    [Fact]
    public void Compute_WhenProcedureCalledIsUnknown_ShouldTakeTheArgumentAsReadAndWritten()
    {
        // Act
        var effects = Compute(
            "      SUBROUTINE CALLER ( X, Y )",
            "      INTEGER X, Y",
            "      CALL NOWHERE ( X )",
            "      END");

        // Assert
        effects.Find("CALLER").ShouldBe([Both, None]);
        effects.Find("NOWHERE").ShouldBeNull();
    }

    [Fact]
    public void Compute_WhenProcedureCalledHasNoSuchArgument_ShouldTakeTheArgumentAsReadAndWritten()
    {
        // Act
        var effects = Compute(
            "      SUBROUTINE CALLER ( X, Y )",
            "      INTEGER X, Y",
            "      CALL LEAF ( X, Y )",
            "      END",
            "      SUBROUTINE LEAF ( IN )",
            "      INTEGER IN, J",
            "      J = IN",
            "      END");

        // Assert
        effects.Find("CALLER").ShouldBe([Read, Both]);
    }

    [Fact]
    public void Compute_WhenProcedureIsDefinedElsewhere_ShouldAskThere()
    {
        // Arrange
        var library = Compute(
            "      SUBROUTINE LIBIN ( IN )",
            "      INTEGER IN, J",
            "      J = IN",
            "      END");
        var asked = new List<string>();

        // Act
        var effects = ArgumentEffects.Compute(
            Summarize(
                "      SUBROUTINE CALLER ( X, Y )",
                "      INTEGER X, Y",
                "      CALL LIBIN ( X )",
                "      CALL NOWHERE ( Y )",
                "      END"),
            name =>
            {
                asked.Add(name);
                return library.Find(name);
            });

        // Assert
        effects.Find("CALLER").ShouldBe([Read, Both]);
        effects.Find("LIBIN").ShouldBeNull();
        asked.ShouldContain("LIBIN");
        asked.ShouldContain("NOWHERE");
    }

    [Fact]
    public void Compute_WhenTwoUnitsDefineTheSameName_ShouldThrow()
    {
        // Act & Assert
        var exception = Should.Throw<FortranAnalysisException>(() => Compute(
            "      SUBROUTINE ONE",
            "      END",
            "      SUBROUTINE TWO",
            "      RETURN",
            "      ENTRY ONE",
            "      END"));
        exception.Message.ShouldBe("Line 5: ONE is defined by another program unit as well.");
    }

    private static ArgumentEffects Compute(params string[] lines) => ArgumentEffects.Compute(Summarize(lines));

    private static List<UnitSummary> Summarize(params string[] lines)
    {
        using var reader = new StringReader(string.Join('\n', lines));
        return SourceParser.Parse(FixedFormReader.Read(reader)).Units
            .Select(unit => UnitSummarizer.Summarize(SymbolTableBuilder.Build(unit, _ => null)))
            .ToList();
    }
}
