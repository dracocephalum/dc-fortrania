using System.Reflection;
using System.Runtime.Loader;
using Dracocephalum.Fortrania.Analysis;
using Dracocephalum.Fortrania.Emit;
using Dracocephalum.Fortrania.Runtime;
using Dracocephalum.Fortrania.Syntax;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Dracocephalum.Fortrania.Tests.Emit;

/// <summary>
/// FORTRAN source taken the whole way: translated, compiled together with a
/// C# class that calls it, and loaded, so that a test can run it.
/// </summary>
internal sealed class TranslatedProgram
{
    public const string Namespace = "Translated";

    public const string ClassName = "Library";

    private static readonly IReadOnlyList<MetadataReference> References =
    [
        .. ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Select(path => MetadataReference.CreateFromFile(path)),
        MetadataReference.CreateFromFile(typeof(Fortran).Assembly.Location),
    ];

    private readonly Assembly _assembly;

    private TranslatedProgram(Assembly assembly) => _assembly = assembly;

    /// <summary>Translates every program unit of <paramref name="fortran"/>, one C# file each.</summary>
    public static IReadOnlyList<string> Translate(string fortran, EmitOptions? options = null)
    {
        using var reader = new StringReader(fortran);
        var tables = SourceParser.Parse(FixedFormReader.Read(reader)).Units
            .Select(unit => SymbolTableBuilder.Build(unit, _ => null))
            .ToList();

        var catalogue = new ProcedureCatalogue();
        var summaries = new List<UnitSummary>();
        foreach (var table in tables)
        {
            TypeChecker.Check(table);
            summaries.Add(UnitSummarizer.Summarize(table));
            catalogue.Add(summaries[^1]);
        }

        foreach (var summary in summaries)
        {
            CallChecker.Check(summary, catalogue.Find);
        }

        var emitter = new CSharpEmitter(options ?? new EmitOptions(Namespace, ClassName), catalogue.Find);
        return tables.Select(table => emitter.Emit(table)).ToList();
    }

    /// <summary>
    /// Translates <paramref name="fortran"/> and compiles it with
    /// <paramref name="driver"/>, the members of a static class named
    /// <c>Driver</c> in the same namespace as the translated class.
    /// </summary>
    public static TranslatedProgram Compile(string fortran, string driver, EmitOptions? options = null)
    {
        var whole = new CSharpEmitter(options ?? new EmitOptions(Namespace, ClassName), _ => null).EmitClass();
        var sources = Translate(fortran, options).Append(whole).Append(
            $$"""
            using System;
            using Dracocephalum.Fortrania.Runtime;

            namespace {{Namespace}};

            public static unsafe class Driver
            {
            {{driver}}
            }

            // The translated library as a program of its own, which a test
            // can make and dispose in one.
            public sealed class Standalone : {{options?.ClassName ?? ClassName}}, IDisposable
            {
                private readonly FortranContext _own;

                public Standalone()
                    : this(new FortranContext())
                {
                }

                private Standalone(FortranContext context)
                    : base(context) => _own = context;

                public void Dispose() => _own.Dispose();
            }
            """).ToList();

        return Load(sources);
    }

    /// <summary>
    /// Translates several libraries that may call each other, each to a
    /// class of its name in one namespace, one C# file per program unit and
    /// one per class.
    /// </summary>
    public static IReadOnlyList<string> TranslateLibraries(params (string ClassName, string Fortran)[] libraries)
    {
        var parsed = libraries.Select(library =>
        {
            using var reader = new StringReader(library.Fortran);
            var tables = SourceParser.Parse(FixedFormReader.Read(reader)).Units
                .Select(unit => SymbolTableBuilder.Build(unit, _ => null))
                .ToList();

            var catalogue = new ProcedureCatalogue();
            var summaries = new List<UnitSummary>();
            foreach (var table in tables)
            {
                TypeChecker.Check(table);
                summaries.Add(UnitSummarizer.Summarize(table));
                catalogue.Add(summaries[^1]);
            }

            return (library.ClassName, Tables: tables, Summaries: summaries, Catalogue: catalogue);
        }).ToList();

        // A call is checked against whichever library defines what it calls.
        ProcedureInterface? FindAnywhere(string name) =>
            parsed.Select(library => library.Catalogue.Find(name)).FirstOrDefault(found => found is not null);

        var files = new List<string>();
        foreach (var library in parsed)
        {
            foreach (var summary in library.Summaries)
            {
                CallChecker.Check(summary, FindAnywhere);
            }

            var options = new EmitOptions(Namespace, library.ClassName)
            {
                References =
                [
                    .. parsed
                        .Where(other => other.ClassName != library.ClassName)
                        .Select(other => new LibraryReference(Namespace, other.ClassName, other.Catalogue.Find)),
                ],
            };

            var emitter = new CSharpEmitter(options, library.Catalogue.Find);
            files.AddRange(library.Tables.Select(table => emitter.Emit(table)));
            files.Add(emitter.EmitClass());
        }

        return files;
    }

    /// <summary>
    /// Translates several libraries that may call each other and compiles
    /// them with <paramref name="driver"/>, the members of a static class
    /// named <c>Driver</c> in their namespace.
    /// </summary>
    public static TranslatedProgram CompileLibraries(string driver, params (string ClassName, string Fortran)[] libraries) =>
        Load(
        [
            .. TranslateLibraries(libraries),
            $$"""
            using System;
            using Dracocephalum.Fortrania.Runtime;

            namespace {{Namespace}};

            public static unsafe class Driver
            {
            {{driver}}
            }
            """,
        ]);

    /// <summary>Compiles C# sources written elsewhere, a whole translation's among them.</summary>
    public static TranslatedProgram FromSources(IEnumerable<string> sources) => Load([.. sources]);

    /// <summary>Calls the <c>Main</c> of a class that takes nothing and gives the program's exit code.</summary>
    public int RunMain(string typeName)
    {
        var main = _assembly.GetType(typeName)!.GetMethod("Main", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)!;
        return (int)main.Invoke(null, null)!;
    }

    private static TranslatedProgram Load(IReadOnlyList<string> sources)
    {
        var compilation = CSharpCompilation.Create(
            "Translated" + Guid.NewGuid().ToString("N"),
            sources.Select(source => CSharpSyntaxTree.ParseText(source)),
            References,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, optimizationLevel: OptimizationLevel.Release, allowUnsafe: true));

        using var image = new MemoryStream();
        var result = compilation.Emit(image);
        if (!result.Success)
        {
            var errors = result.Diagnostics.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
            throw new InvalidOperationException(
                "The translated code does not compile:\n" + string.Join('\n', errors) + "\n\n" + string.Join("\n\n", sources));
        }

        image.Position = 0;
        return new TranslatedProgram(AssemblyLoadContext.Default.LoadFromStream(image));
    }

    /// <summary>Calls a static method of the driver that takes no arguments.</summary>
    public T Run<T>(string method)
    {
        var driver = _assembly.GetType($"{Namespace}.Driver")!;
        try
        {
            return (T)driver.GetMethod(method)!.Invoke(null, null)!;
        }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
            throw;
        }
    }
}
