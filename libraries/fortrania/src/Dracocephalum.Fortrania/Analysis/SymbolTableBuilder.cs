using Dracocephalum.Fortrania.Syntax;

namespace Dracocephalum.Fortrania.Analysis;

/// <summary>
/// Reads a program unit, and the include files it names, into a
/// <see cref="SymbolTable"/>: what it declares about each name, and what
/// each name turns out to stand for once every use of it has been seen.
/// </summary>
public static partial class SymbolTableBuilder
{
    // Include files may name include files; this many deep is a loop.
    private const int MaximumIncludeDepth = 16;

    /// <summary>Builds the symbol table of <paramref name="unit"/>.</summary>
    /// <param name="unit">A program unit.</param>
    /// <param name="resolveInclude">
    /// Finds an include file by the name written in the <c>INCLUDE</c>
    /// statement, and returns <see langword="null"/> when there is none.
    /// </param>
    /// <returns>The unit's names.</returns>
    /// <exception cref="FortranAnalysisException">
    /// The declarations contradict each other or stand where they may not, a
    /// name is used against what it is or has no type where it needs one, an
    /// include file is not found, or a construct is not supported yet.
    /// </exception>
    public static SymbolTable Build(ProgramUnit unit, Func<string, IncludeFile?> resolveInclude)
    {
        ArgumentNullException.ThrowIfNull(unit);
        ArgumentNullException.ThrowIfNull(resolveInclude);

        var builder = new Builder(resolveInclude);
        return builder.Build(unit);
    }

    // Where a statement being read stands, for error messages.
    private readonly record struct Location(int Line, string? IncludeFile)
    {
        public FortranAnalysisException Error(string message) => new(Line, IncludeFile, message);
    }

    private sealed class Draft(string name, Location firstSeenAt)
    {
        public string Name { get; } = name;

        public Location FirstSeenAt { get; } = firstSeenAt;

        public TypeSpecification? DeclaredType { get; set; }

        public IReadOnlyList<DimensionBound> Dimensions { get; set; } = [];

        public Expression? ConstantValue { get; set; }

        public bool IsDummyArgument { get; set; }

        public bool IsEntryPoint { get; set; }

        public bool IsSaved { get; set; }

        public bool IsExternal { get; set; }

        public bool IsIntrinsic { get; set; }

        public bool IsStatementFunction { get; set; }

        // Where the name is first used in each way, or null when it never is.
        public Location? CalledAt { get; set; }

        public Location? ReferencedAt { get; set; }

        public Location? UsedAsValueAt { get; set; }

        public Location? AssignedAt { get; set; }

        public Symbol ToSymbol(SymbolKind kind, TypeSpecification? type) => new(
            Name,
            kind,
            type,
            type is not null && DeclaredType is null,
            Dimensions,
            ConstantValue,
            IsDummyArgument,
            IsEntryPoint,
            IsSaved,
            IsExternal,
            IsIntrinsic,
            IsStatementFunction,
            FirstSeenAt.IncludeFile);
    }

    private sealed partial class Builder(Func<string, IncludeFile?> resolveInclude)
    {
        private readonly List<Draft> _entries = [];
        private readonly Dictionary<string, Draft> _byName = new(StringComparer.Ordinal);
        private readonly List<ParsedStatement> _entryPoints = [];
        private readonly List<DataGroup> _data = [];
        private readonly List<IReadOnlyList<Expression>> _equivalences = [];
        private readonly List<ParsedStatement> _statementFunctions = [];
        private readonly Stack<string> _includes = new();
        private bool _isImplicitNone;
        private bool _savesEverything;
        private bool _executableSeen;
        private bool _isFunction;

        public SymbolTable Build(ProgramUnit unit)
        {
            if (unit.Header is { } header)
            {
                var location = new Location(header.Source.Line, null);
                switch (header.Statement)
                {
                    case Statement.Subroutine subroutine:
                        AddEntryPoint(header, subroutine.Name, subroutine.Parameters, location);
                        break;
                    case Statement.Function function:
                        _isFunction = true;
                        AddEntryPoint(header, function.Name, function.Parameters, location);
                        if (function.ReturnType is not null)
                        {
                            SetType(Get(function.Name, location), function.ReturnType, location);
                        }

                        break;
                }
            }

            Visit(unit.Body, isNested: false);

            return new SymbolTable(
                unit,
                _isImplicitNone,
                _savesEverything,
                [.. _entries.Select(Finish)],
                _entryPoints,
                _data,
                _equivalences,
                _statementFunctions);
        }

        private void Visit(IReadOnlyList<Construct> body, bool isNested)
        {
            foreach (var construct in body)
            {
                switch (construct)
                {
                    case Construct.Simple simple:
                        Visit(simple.Statement, new Location(simple.Statement.Source.Line, null), isNested);
                        break;
                    case Construct.If block:
                        _executableSeen = true;
                        foreach (var branch in block.Branches)
                        {
                            Use(branch.Header.Statement, new Location(branch.Header.Source.Line, null));
                            Visit(branch.Body, isNested: true);
                        }

                        break;
                    case Construct.Loop loop:
                        _executableSeen = true;
                        Use(loop.Header.Statement, new Location(loop.Header.Source.Line, null));
                        Visit(loop.Body, isNested: true);
                        break;
                }
            }
        }

        private void Visit(ParsedStatement parsed, Location location, bool isNested)
        {
            var statement = parsed.Statement;

            // DATA may stand anywhere after the specification statements, and
            // ENTRY anywhere outside a block; neither is bound by the order
            // checked below.
            switch (statement)
            {
                case Statement.Data data:
                    _data.AddRange(data.Groups);
                    Use(data, location);
                    return;
                case Statement.Entry entry:
                    if (location.IncludeFile is not null)
                    {
                        throw location.Error("An ENTRY in an include file is not supported yet.");
                    }

                    AddEntryPoint(parsed, entry.Name, entry.Parameters, location);
                    return;
            }

            // Written like an assignment to an array element, but the name is no
            // array and nothing has executed yet: a statement function. The
            // standard wants every declaration before the first of these; the
            // reference sources declare the next one's type after it, and
            // compilers accept that, so it does not end the declarations.
            if (!isNested
                && !_executableSeen
                && statement is Statement.Assignment { Target: Expression.Reference target }
                && target.Arguments.All(argument => argument is Expression.Name)
                && !(_byName.TryGetValue(target.Identifier, out var known) && known.Dimensions.Count > 0))
            {
                AddStatementFunction(parsed, target, location);
                return;
            }

            if (!IsSpecification(statement))
            {
                if (location.IncludeFile is not null)
                {
                    throw location.Error("An include file that holds anything but declarations and DATA is not supported yet.");
                }

                _executableSeen = true;
                Use(statement, location);
                return;
            }

            if (isNested)
            {
                throw location.Error("A declaration cannot stand inside a block IF or a DO.");
            }

            if (_executableSeen)
            {
                throw location.Error("A declaration cannot follow the first executable statement of its program unit.");
            }

            switch (statement)
            {
                case Statement.TypeDeclaration declaration:
                    foreach (var entity in declaration.Entities)
                    {
                        var entry = Get(entity.Name, location);
                        SetType(entry, declaration.Type with { Length = entity.Length ?? declaration.Type.Length }, location);
                        SetDimensions(entry, entity.Dimensions, location);
                        Use(entry.DeclaredType?.Length?.Value, location);
                    }

                    break;
                case Statement.Dimension dimension:
                    foreach (var entity in dimension.Entities)
                    {
                        SetDimensions(Get(entity.Name, location), entity.Dimensions, location);
                    }

                    break;
                case Statement.Parameter parameter:
                    foreach (var constant in parameter.Constants)
                    {
                        var entry = Get(constant.Name, location);
                        if (entry.ConstantValue is not null)
                        {
                            throw location.Error($"{entry.Name} is given a value by PARAMETER twice.");
                        }

                        entry.ConstantValue = constant.Value;
                        Use(constant.Value, location);
                        Check(entry, location);
                    }

                    break;
                case Statement.ImplicitNone:
                    _isImplicitNone = true;
                    break;
                case Statement.Save save:
                    _savesEverything |= save.Names.Count == 0;
                    foreach (var name in save.Names)
                    {
                        var entry = Get(name, location);
                        entry.IsSaved = true;
                        Check(entry, location);
                    }

                    break;
                case Statement.External external:
                    foreach (var name in external.Names)
                    {
                        var entry = Get(name, location);
                        entry.IsExternal = true;
                        Check(entry, location);
                    }

                    break;
                case Statement.Intrinsic intrinsic:
                    foreach (var name in intrinsic.Names)
                    {
                        var entry = Get(name, location);
                        entry.IsIntrinsic = true;
                        Check(entry, location);
                    }

                    break;
                case Statement.Equivalence equivalence:
                    _equivalences.AddRange(equivalence.Sets);
                    foreach (var item in equivalence.Sets.SelectMany(set => set))
                    {
                        Use(item, location);
                    }

                    break;
                case Statement.Include include:
                    VisitInclude(include.FileName, location);
                    break;
            }
        }

        private static bool IsSpecification(Statement statement) => statement is
            Statement.TypeDeclaration or Statement.Dimension or Statement.Parameter or Statement.ImplicitNone
            or Statement.Save or Statement.External or Statement.Intrinsic or Statement.Equivalence or Statement.Include;

        private void VisitInclude(string fileName, Location location)
        {
            if (_includes.Count == MaximumIncludeDepth)
            {
                throw location.Error($"Include files are nested more than {MaximumIncludeDepth} deep, which is taken to be a file that includes itself.");
            }

            var file = resolveInclude(fileName) ?? throw location.Error($"The include file '{fileName}' was not found.");

            _includes.Push(fileName);
            foreach (var construct in file.Body)
            {
                if (construct is not Construct.Simple simple)
                {
                    var line = construct is Construct.If block ? block.Branches[0].Header.Source.Line : ((Construct.Loop)construct).Header.Source.Line;
                    throw new Location(line, fileName).Error("An include file that holds anything but declarations and DATA is not supported yet.");
                }

                Visit(simple.Statement, new Location(simple.Statement.Source.Line, fileName), isNested: false);
            }

            _includes.Pop();
        }

        private void AddEntryPoint(ParsedStatement statement, string name, IReadOnlyList<string> parameters, Location location)
        {
            _entryPoints.Add(statement);

            var entry = Get(name, location);
            if (entry.IsEntryPoint)
            {
                throw location.Error($"{name} names two entries of the same program unit.");
            }

            entry.IsEntryPoint = true;
            Check(entry, location);

            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var parameter in parameters)
            {
                if (!seen.Add(parameter))
                {
                    throw location.Error($"{parameter} appears twice in the argument list of {name}.");
                }

                var argument = Get(parameter, location);
                argument.IsDummyArgument = true;
                Check(argument, location);
            }
        }

        private void AddStatementFunction(ParsedStatement statement, Expression.Reference target, Location location)
        {
            var entry = Get(target.Identifier, location);
            if (entry.IsStatementFunction)
            {
                throw location.Error($"The statement function {entry.Name} is defined twice.");
            }

            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var argument in target.Arguments)
            {
                var name = ((Expression.Name)argument).Identifier;
                if (!seen.Add(name))
                {
                    throw location.Error($"{name} appears twice in the argument list of the statement function {entry.Name}.");
                }

                // Its type is that of the unit's variable of the same name.
                Get(name, location).UsedAsValueAt ??= location;
            }

            entry.IsStatementFunction = true;
            Use(((Statement.Assignment)statement.Statement).Value, location);
            Check(entry, location);
            _statementFunctions.Add(statement);
        }

        private Draft Get(string name, Location location)
        {
            if (!_byName.TryGetValue(name, out var entry))
            {
                entry = new Draft(name, location);
                _byName.Add(name, entry);
                _entries.Add(entry);
            }

            return entry;
        }

        private static void SetType(Draft entry, TypeSpecification type, Location location)
        {
            if (entry.DeclaredType is not null)
            {
                throw location.Error($"{entry.Name} is given a type twice.");
            }

            entry.DeclaredType = type;
        }

        private void SetDimensions(Draft entry, IReadOnlyList<DimensionBound> dimensions, Location location)
        {
            if (dimensions.Count == 0)
            {
                return;
            }

            if (entry.Dimensions.Count > 0)
            {
                throw location.Error($"{entry.Name} is given dimensions twice.");
            }

            entry.Dimensions = dimensions;
            foreach (var bound in dimensions)
            {
                Use(bound.Lower, location);
                Use(bound.Upper, location);
            }

            Check(entry, location);
        }

        // Each of these pairs excludes the other; FORTRAN 77 allows none.
        private static void Check(Draft entry, Location location)
        {
            var isArray = entry.Dimensions.Count > 0;
            var isConstant = entry.ConstantValue is not null;

            string? conflict = null;
            if (isConstant && entry.IsDummyArgument)
            {
                conflict = "a named constant and a dummy argument";
            }
            else if (isConstant && isArray)
            {
                conflict = "a named constant and an array";
            }
            else if (isConstant && entry.IsSaved)
            {
                conflict = "a named constant and saved";
            }
            else if (isConstant && (entry.IsExternal || entry.IsIntrinsic || entry.IsEntryPoint))
            {
                conflict = "a named constant and a procedure";
            }
            else if (entry.IsExternal && entry.IsIntrinsic)
            {
                conflict = "external and intrinsic";
            }
            else if (isArray && (entry.IsExternal || entry.IsIntrinsic || entry.IsEntryPoint))
            {
                conflict = "an array and a procedure";
            }
            else if (entry.IsSaved && entry.IsDummyArgument)
            {
                conflict = "saved and a dummy argument";
            }
            else if (entry.IsSaved && (entry.IsExternal || entry.IsIntrinsic || entry.IsEntryPoint))
            {
                conflict = "saved and a procedure";
            }
            else if (entry.IsEntryPoint && (entry.IsDummyArgument || entry.IsExternal || entry.IsIntrinsic))
            {
                conflict = "an entry of this program unit and something else";
            }
            else if (entry.IsIntrinsic && entry.IsDummyArgument)
            {
                conflict = "intrinsic and a dummy argument";
            }
            else if (entry.IsStatementFunction
                && (isArray || isConstant || entry.IsSaved || entry.IsDummyArgument || entry.IsEntryPoint || entry.IsExternal || entry.IsIntrinsic))
            {
                conflict = "a statement function and something else";
            }

            if (conflict is not null)
            {
                throw location.Error($"{entry.Name} cannot be both {conflict}.");
            }
        }
    }
}
