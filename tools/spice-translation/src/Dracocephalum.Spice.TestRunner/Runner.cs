using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using Dracocephalum.Fortrania.Runtime;
using Dracocephalum.Spice.TSpice;

namespace Dracocephalum.Spice.TestRunner;

/// <summary>Chooses the families to run, runs them, and says how they went.</summary>
internal static class Runner
{
    private const string SlowFamiliesFile = "slow-families.txt";

    // The deepest chain of calls in the test program takes some 90 KB of
    // stack in working buffers alone; this leaves room to spare whatever
    // the platform gives a thread by default.
    private const int StackSize = 16 * 1024 * 1024;

    /// <summary>Runs what the options ask for.</summary>
    /// <param name="options">The options.</param>
    /// <returns>The exit code: zero when every family passed, one when one failed, two for a family that does not exist.</returns>
    public static int Run(RunnerOptions options)
    {
        var families = FindFamilies();
        if (options.Names.Count > 0)
        {
            var unknown = options.Names.Where(name => families.All(family => family.Name != name)).ToList();
            if (unknown.Count > 0)
            {
                Console.Error.WriteLine($"No such test family: {string.Join(", ", unknown)}.");
                return 2;
            }

            families = [.. families.Where(family => options.Names.Contains(family.Name))];
        }

        if (options.IsQuick)
        {
            var slow = ReadSlowFamilies();
            families = [.. families.Where(family => !slow.Contains(family.Name))];
        }

        if (options.ListsOnly)
        {
            foreach (var family in families)
            {
                Console.WriteLine(family.Name);
            }

            return 0;
        }

        var started = Stopwatch.GetTimestamp();
        var results = options.UsesDisk ? RunOnDisk(families, options) : RunInMemory(families, options);
        var seconds = Stopwatch.GetElapsedTime(started).TotalSeconds;

        foreach (var failed in results.Where(result => !result.Passed))
        {
            Console.WriteLine();
            Console.WriteLine($"---- {failed.Name} failed ----");
            Console.WriteLine(failed.Output.TrimEnd());
        }

        if (options.TimesFile is { } file)
        {
            File.WriteAllLines(file, results.Select(result => string.Create(
                CultureInfo.InvariantCulture,
                $"{result.Name}\t{(result.Passed ? "passed" : "failed")}\t{result.Seconds:F3}")));
        }

        var failures = results.Count(result => !result.Passed);
        Console.WriteLine();
        Console.WriteLine(string.Create(
            CultureInfo.InvariantCulture,
            $"{results.Count} families, {results.Count - failures} passed, {failures} failed, in {seconds:F1} s; {results.Sum(result => result.Seconds):F1} s summed over the families."));
        return failures == 0 ? 0 : 1;
    }

    // The families are the test program's routines named F_ and something,
    // each taking where to say whether it passed. They run in the order of
    // their names, which is the order the program's own main routine has.
    private static List<MethodInfo> FindFamilies() =>
    [
        .. typeof(TSpiceMain).GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(method => method.Name.StartsWith("F_", StringComparison.Ordinal)
                && method.ReturnType == typeof(void)
                && method.GetParameters() is [{ ParameterType: var type }]
                && type == typeof(int).MakePointerType())
            .OrderBy(method => method.Name, StringComparer.Ordinal),
    ];

    private static HashSet<string> ReadSlowFamilies() =>
    [
        .. File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, SlowFamiliesFile))
            .Select(line => line.Trim())
            .Where(line => line.Length > 0 && !line.StartsWith('#')),
    ];

    // One instance for them all, in the working folder, one family after
    // another: the working folder is the process's, so two instances would
    // write the same files.
    private static IReadOnlyList<FamilyResult> RunOnDisk(List<MethodInfo> families, RunnerOptions options)
    {
        IReadOnlyList<FamilyResult> results = [];
        var thread = new Thread(() => results = Session.Run(families, new DiskFileProvider(), options.IsVerbose ? Report : null), StackSize);
        thread.Start();
        thread.Join();
        return results;
    }

    // An instance for each family, with files of its own in memory, as many
    // at once as there are jobs.
    private static FamilyResult[] RunInMemory(List<MethodInfo> families, RunnerOptions options)
    {
        var results = new FamilyResult[families.Count];
        var next = -1;
        var threads = new List<Thread>();
        for (var job = 0; job < Math.Min(options.Jobs, families.Count); job++)
        {
            var thread = new Thread(
                () =>
                {
                    int index;
                    while ((index = Interlocked.Increment(ref next)) < families.Count)
                    {
                        results[index] = Session.Run([families[index]], new MemoryFileProvider(), options.IsVerbose ? Report : null)[0];
                    }
                },
                StackSize);
            thread.Start();
            threads.Add(thread);
        }

        foreach (var thread in threads)
        {
            thread.Join();
        }

        return results;
    }

    private static void Report(FamilyResult result)
    {
        lock (Console.Out)
        {
            Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"{(result.Passed ? "passed" : "FAILED")}  {result.Seconds,8:F2} s  {result.Name}"));
        }
    }
}
