using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.RegularExpressions;
using BenchmarkDotNet.Attributes;

namespace Dracocephalum.Fortrania.Benchmarks;

/// <summary>
/// Finds the benchmark classes of an assembly and sets them up for the
/// paired runner. The classes are written for BenchmarkDotNet, with its
/// attributes, and are read here by the same attributes, so that one class
/// serves both ways of measuring.
/// </summary>
internal static class BenchmarkCatalog
{
    /// <summary>Lists every benchmark method as <c>Class.Method</c>.</summary>
    public static IReadOnlyList<string> ListMethods(Assembly assembly) =>
        [.. FindClasses(assembly).SelectMany(type => FindMethods(type).Select(method => $"{type.Name}.{method.Name}"))];

    /// <summary>
    /// Sets up the cases whose methods a pattern names. A class is set up
    /// once for each choice of its parameters, and is to be given to
    /// <see cref="Release"/> when it has been measured.
    /// </summary>
    /// <param name="assembly">Where the benchmark classes are.</param>
    /// <param name="patterns">Patterns over <c>Class.Method</c>, <c>*</c> standing for anything; a candidate is kept when one matches it, and a baseline whenever one of its candidates is.</param>
    /// <returns>The cases, each with the instance it was made over.</returns>
    public static IReadOnlyList<(object Instance, BenchmarkCase Case)> SetUp(Assembly assembly, IReadOnlyList<string> patterns)
    {
        var filters = patterns.Select(ToRegex).ToArray();
        var cases = new List<(object, BenchmarkCase)>();
        foreach (var type in FindClasses(assembly))
        {
            bool IsAskedFor(MethodInfo method) => filters.Any(filter => filter.IsMatch($"{type.Name}.{method.Name}"));

            var groups = FindMethods(type)
                .GroupBy(method => method.GetCustomAttribute<BenchmarkCategoryAttribute>()?.Categories.FirstOrDefault() ?? string.Empty)
                .Select(group => (
                    Name: group.Key,
                    Baseline: group.Single(method => method.GetCustomAttribute<BenchmarkAttribute>()!.Baseline),
                    Candidates: group.Where(method => !method.GetCustomAttribute<BenchmarkAttribute>()!.Baseline).Where(IsAskedFor).ToArray()))
                .Where(group => group.Candidates.Length > 0)
                .ToArray();
            if (groups.Length == 0)
            {
                continue;
            }

            foreach (var choice in ChooseParameters(type))
            {
                var instance = Activator.CreateInstance(type)!;
                foreach (var (property, value) in choice)
                {
                    property.SetValue(instance, value);
                }

                Call<GlobalSetupAttribute>(instance);
                var parameters = string.Join(", ", choice.Select(pair => string.Create(CultureInfo.InvariantCulture, $"{pair.Property.Name}={pair.Value}")));
                foreach (var group in groups)
                {
                    cases.Add((instance, new BenchmarkCase(
                        type.Name,
                        parameters,
                        group.Name,
                        Bind(instance, group.Baseline),
                        [.. group.Candidates.Select(method => Bind(instance, method))],
                        group.Baseline.GetCustomAttribute<BenchmarkAttribute>()!.OperationsPerInvoke)));
                }
            }
        }

        return cases;
    }

    /// <summary>Lets go of what an instance took when it was set up.</summary>
    public static void Release(object instance) => Call<GlobalCleanupAttribute>(instance);

    private static IEnumerable<Type> FindClasses(Assembly assembly) =>
        assembly.GetTypes().Where(type => type is { IsClass: true, IsAbstract: false } && FindMethods(type).Any()).OrderBy(type => type.Name, StringComparer.Ordinal);

    private static IEnumerable<MethodInfo> FindMethods(Type type) =>
        type.GetMethods(BindingFlags.Public | BindingFlags.Instance).Where(method => method.GetCustomAttribute<BenchmarkAttribute>() is not null);

    // Every combination of the values the class's parameters may take, one
    // choice with nothing in it where it has none.
    private static IEnumerable<IReadOnlyList<(PropertyInfo Property, object? Value)>> ChooseParameters(Type type)
    {
        IEnumerable<IReadOnlyList<(PropertyInfo, object?)>> choices = [[]];
        foreach (var property in type.GetProperties().Where(property => property.GetCustomAttribute<ParamsAttribute>() is not null))
        {
            var values = property.GetCustomAttribute<ParamsAttribute>()!.Values;
            choices = [.. choices.SelectMany(choice => values.Select(value => (IReadOnlyList<(PropertyInfo, object?)>)[.. choice, (property, value)]))];
        }

        return choices;
    }

    // A delegate that calls the method and lets its result go, compiled so
    // that calling it costs what calling a method costs.
    private static BenchmarkCase.Method Bind(object instance, MethodInfo method) =>
        new(method.Name, Expression.Lambda<Action>(Expression.Call(Expression.Constant(instance), method)).Compile());

    private static void Call<TAttribute>(object instance)
        where TAttribute : Attribute
    {
        foreach (var method in instance.GetType().GetMethods().Where(method => method.GetCustomAttribute<TAttribute>() is not null))
        {
            method.Invoke(instance, null);
        }
    }

    private static Regex ToRegex(string pattern) =>
        new("^" + Regex.Escape(pattern).Replace("\\*", ".*", StringComparison.Ordinal) + "$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
}
