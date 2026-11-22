using Dracocephalum.Fortrania.Emit;
using Shouldly;

namespace Dracocephalum.Fortrania.Tests.Emit;

public class CSharpStatementWriterTests
{
    private static readonly TreeExpression I = new TreeExpression.Local("i");
    private static readonly TreeExpression N = new TreeExpression.Local("n");
    private static readonly TreeExpression One = new TreeExpression.Integer(1);

    [Fact]
    public void Write_WhenGivenStatementsOfOneLine_ShouldEndEachWithASemicolon()
    {
        // Arrange
        IReadOnlyList<TreeStatement> statements =
        [
            new TreeStatement.Declare("int", "_last1", N),
            new TreeStatement.Assign(I, One),
            new TreeStatement.Evaluate(new TreeExpression.Call("SUB", [I])),
            new TreeStatement.Advance(I, One),
            new TreeStatement.Advance(I, N, IsSubtracted: true),
            new TreeStatement.Throw(new TreeExpression.New("FortranStopException", [One])),
            new TreeStatement.Return(I),
            new TreeStatement.Return(null),
        ];

        // Act
        var text = Written(statements);

        // Assert
        text.ShouldBe(
            """
            int _last1 = n;
            i = 1;
            SUB(i);
            i += 1;
            i -= n;
            throw new FortranStopException(1);
            return i;
            return;

            """,
            StringCompareShould.IgnoreLineEndings);
    }

    [Fact]
    public void Write_WhenGivenBranches_ShouldWriteTheFirstAsIfTheNextAsElseIfAndTheOneWithoutAConditionAsElse()
    {
        // Arrange
        var less = new TreeExpression.Binary(TreeExpression.Operation.Less, I, N);
        var more = new TreeExpression.Binary(TreeExpression.Operation.Greater, I, N);
        var branched = new TreeStatement.If(
        [
            new TreeStatement.Branch(less, [new TreeStatement.Assign(I, N)]),
            new TreeStatement.Branch(more, [new TreeStatement.Return(null)]),
            new TreeStatement.Branch(null, []),
        ]);

        // Act
        var text = Written([branched]);

        // Assert
        text.ShouldBe(
            """
            if (i < n)
            {
                i = n;
            }
            else if (i > n)
            {
                return;
            }
            else
            {
            }

            """,
            StringCompareShould.IgnoreLineEndings);
    }

    [Fact]
    public void Write_WhenGivenLoops_ShouldWriteTheirHeadingsOnOneLineAndTheirBodiesMovedIn()
    {
        // Arrange
        var count = new TreeExpression.Local("_count1");
        var counted = new TreeStatement.For(
            new TreeStatement.Declare("int", "_count1", N),
            new TreeExpression.Binary(TreeExpression.Operation.Greater, count, new TreeExpression.Integer(0)),
            [new TreeStatement.Decrement(count), new TreeStatement.Advance(I, One)],
            [new TreeStatement.While(new TreeExpression.Binary(TreeExpression.Operation.Less, I, N), [new TreeStatement.Advance(I, One)])]);

        // Act
        var text = Written([counted]);

        // Assert
        text.ShouldBe(
            """
            for (int _count1 = n; _count1 > 0; _count1--, i += 1)
            {
                while (i < n)
                {
                    i += 1;
                }
            }

            """,
            StringCompareShould.IgnoreLineEndings);
    }

    [Fact]
    public void Write_WhenGivenTextOfAPartNotMoved_ShouldWriteItAsItStands()
    {
        // Arrange
        IReadOnlyList<TreeStatement> statements =
        [
            new TreeStatement.Text("// A comment."),
            new TreeStatement.Blank(),
            new TreeStatement.TextBlock("fixed (byte* p = a)", [new TreeStatement.Text("_io.Write(p, 4);")]),
        ];

        // Act
        var text = Written(statements);

        // Assert
        text.ShouldBe(
            """
            // A comment.

            fixed (byte* p = a)
            {
                _io.Write(p, 4);
            }

            """,
            StringCompareShould.IgnoreLineEndings);
    }

    private static string Written(IReadOnlyList<TreeStatement> statements)
    {
        var text = new CodeWriter();
        CSharpStatementWriter.Write(text, statements);
        return text.ToString();
    }
}
