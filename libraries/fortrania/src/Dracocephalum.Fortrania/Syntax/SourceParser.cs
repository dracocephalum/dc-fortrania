namespace Dracocephalum.Fortrania.Syntax;

/// <summary>
/// Parses every statement of a file and assembles them into a tree: program
/// units, and within each, every block <c>IF</c> and <c>DO</c> holding what
/// lies between it and its end.
/// </summary>
public static class SourceParser
{
    /// <summary>Parses a file of program units.</summary>
    /// <param name="source">The file, split into statements.</param>
    /// <returns>The program units the file holds.</returns>
    /// <exception cref="FortranSyntaxException">
    /// A statement is malformed or not supported yet, a block is not closed
    /// where it should be, or the last program unit has no <c>END</c>.
    /// </exception>
    public static SourceFile Parse(FixedFormSource source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var assembler = new Assembler(isInclude: false);
        foreach (var statement in source.Statements)
        {
            assembler.Add(statement);
        }

        var units = assembler.FinishFile();
        if (units.Count > 0)
        {
            units[^1] = units[^1] with { TrailingComments = source.TrailingComments };
        }

        return new SourceFile(units, source.TrailingComments);
    }

    /// <summary>Parses a file that other files draw in with <c>INCLUDE</c>.</summary>
    /// <param name="source">The file, split into statements.</param>
    /// <returns>The statements the file holds.</returns>
    /// <exception cref="FortranSyntaxException">
    /// A statement is malformed or not supported yet, a block is not closed
    /// within the file, or the file starts or ends a program unit.
    /// </exception>
    public static IncludeFile ParseInclude(FixedFormSource source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var assembler = new Assembler(isInclude: true);
        foreach (var statement in source.Statements)
        {
            assembler.Add(statement);
        }

        return new IncludeFile(assembler.FinishInclude(), source.TrailingComments);
    }

    private sealed class Assembler(bool isInclude)
    {
        private readonly List<ProgramUnit> _units = [];
        private readonly Stack<Block> _open = new();
        private List<Construct> _body = [];
        private ParsedStatement? _header;
        private bool _unitIsOpen;
        private int _lastLine;

        // Where the next construct goes: the innermost open block, or the
        // unit's own body when none is open.
        private List<Construct> Target => _open.TryPeek(out var block) ? block.Body : _body;

        public void Add(FixedFormStatement source)
        {
            var tokens = Lexer.Tokenize(source);
            var kind = StatementClassifier.Classify(tokens, startsProgramUnit: !isInclude && !_unitIsOpen);
            var parsed = new ParsedStatement(StatementParser.Parse(tokens, kind), source);
            _lastLine = source.Line;

            switch (parsed.Statement)
            {
                case Statement.Program or Statement.Subroutine or Statement.Function or Statement.BlockData:
                    OpenUnit(parsed);
                    return;
                case Statement.End:
                    CloseUnit(parsed);
                    return;
            }

            _unitIsOpen = true;

            switch (parsed.Statement)
            {
                case Statement.BlockIf:
                    _open.Push(new IfBlock(parsed));
                    break;
                case Statement.ElseIf or Statement.Else:
                    InnermostIf(parsed, "has no block IF to belong to").StartBranch(parsed);
                    break;
                case Statement.EndIf:
                    var ifBlock = InnermostIf(parsed, "has no block IF to close");
                    _open.Pop();
                    Target.Add(ifBlock.Close(parsed));
                    break;
                case Statement.Do { EndLabel: not null } or Statement.DoWhile { EndLabel: not null }:
                    throw new FortranSyntaxException(source.Line, "A DO that ends at a labelled statement is not supported yet.");
                case Statement.Do or Statement.DoWhile:
                    _open.Push(new LoopBlock(parsed));
                    break;
                case Statement.EndDo:
                    if (!_open.TryPeek(out var top) || top is not LoopBlock loop)
                    {
                        throw Misplaced(parsed, "END DO has no DO to close");
                    }

                    _open.Pop();
                    Target.Add(new Construct.Loop(loop.Header, loop.Body, parsed));
                    break;
                case Statement.Entry:
                    CheckEntry(parsed);
                    Target.Add(new Construct.Simple(parsed));
                    break;
                default:
                    Target.Add(new Construct.Simple(parsed));
                    break;
            }
        }

        public List<ProgramUnit> FinishFile()
        {
            if (_unitIsOpen)
            {
                throw new FortranSyntaxException(_lastLine, "The file ends before the END of its last program unit.");
            }

            return _units;
        }

        public List<Construct> FinishInclude()
        {
            CheckNothingIsOpen(_lastLine, "The file ends");
            return _body;
        }

        private void OpenUnit(ParsedStatement header)
        {
            if (isInclude)
            {
                throw new FortranSyntaxException(header.Source.Line, "An include file cannot start a program unit.");
            }

            if (_unitIsOpen)
            {
                throw new FortranSyntaxException(header.Source.Line, "A program unit starts before the END of the one before it.");
            }

            _header = header;
            _unitIsOpen = true;
        }

        private void CloseUnit(ParsedStatement end)
        {
            if (isInclude)
            {
                throw new FortranSyntaxException(end.Source.Line, "An include file cannot end a program unit.");
            }

            CheckNothingIsOpen(end.Source.Line, "The program unit ends");
            _units.Add(new ProgramUnit(_header, _body, end));
            _body = [];
            _header = null;
            _unitIsOpen = false;
        }

        private void CheckNothingIsOpen(int line, string what)
        {
            if (_open.TryPeek(out var block))
            {
                var opener = block is IfBlock ? "block IF" : "DO";
                throw new FortranSyntaxException(line, $"{what} with the {opener} on line {block.Line} still open.");
            }
        }

        private void CheckEntry(ParsedStatement entry)
        {
            if (_open.Count > 0)
            {
                throw new FortranSyntaxException(entry.Source.Line, "An ENTRY cannot stand inside a block IF or a DO.");
            }

            if (!isInclude && _header?.Statement is not (Statement.Subroutine or Statement.Function))
            {
                throw new FortranSyntaxException(entry.Source.Line, "An ENTRY can stand only in a subroutine or a function.");
            }
        }

        private IfBlock InnermostIf(ParsedStatement statement, string problem)
        {
            if (_open.TryPeek(out var block) && block is IfBlock ifBlock)
            {
                return ifBlock;
            }

            var word = statement.Statement switch
            {
                Statement.ElseIf => "ELSE IF",
                Statement.Else => "ELSE",
                _ => "END IF",
            };
            throw Misplaced(statement, $"{word} {problem}");
        }

        private FortranSyntaxException Misplaced(ParsedStatement statement, string problem)
        {
            var inside = _open.TryPeek(out var block)
                ? $"; the innermost open block is the {(block is IfBlock ? "block IF" : "DO")} on line {block.Line}"
                : string.Empty;
            return new FortranSyntaxException(statement.Source.Line, $"{problem}{inside}.");
        }
    }

    private abstract class Block(ParsedStatement header)
    {
        public ParsedStatement Header { get; } = header;

        public int Line => Header.Source.Line;

        public abstract List<Construct> Body { get; }
    }

    private sealed class LoopBlock(ParsedStatement header) : Block(header)
    {
        public override List<Construct> Body { get; } = [];
    }

    private sealed class IfBlock(ParsedStatement header) : Block(header)
    {
        private readonly List<IfBranch> _branches = [];
        private ParsedStatement _branchHeader = header;
        private List<Construct> _branchBody = [];

        public override List<Construct> Body => _branchBody;

        public void StartBranch(ParsedStatement branchHeader)
        {
            if (_branchHeader.Statement is Statement.Else)
            {
                throw new FortranSyntaxException(
                    branchHeader.Source.Line,
                    $"Nothing but END IF can follow the ELSE on line {_branchHeader.Source.Line} of this block IF.");
            }

            _branches.Add(new IfBranch(_branchHeader, _branchBody));
            _branchHeader = branchHeader;
            _branchBody = [];
        }

        public Construct.If Close(ParsedStatement end)
        {
            _branches.Add(new IfBranch(_branchHeader, _branchBody));
            return new Construct.If(_branches, end);
        }
    }
}
