using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using Dracocephalum.Fortrania.Runtime;
using Dracocephalum.Spice.TSpice;
using static Dracocephalum.Fortrania.Runtime.Fortran;

namespace Dracocephalum.Spice.TestRunner;

/// <summary>
/// Runs test families in one instance of the translated test program, as
/// its own main program does: the test utilities are set up, each family is
/// called in turn, and the utilities are closed. Each session has its own
/// instance, so sessions on different threads share nothing but the file
/// provider's idea of a file, which is why only sessions whose files are in
/// memory run side by side.
/// </summary>
internal static unsafe class Session
{
    private const int CommandLineLength = 1024;

    /// <summary>Runs families one after another in a new instance.</summary>
    /// <param name="families">The families' routines, in the order to run them.</param>
    /// <param name="provider">Where the instance's files are.</param>
    /// <param name="report">Called with each result as its family ends; may be null.</param>
    /// <returns>A result for each family, in the same order.</returns>
    public static IReadOnlyList<FamilyResult> Run(IReadOnlyList<MethodInfo> families, IFileProvider provider, Action<FamilyResult>? report)
    {
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        using var context = new FortranContext(new FortranContextOptions { FileProvider = provider });
        context.IO.Output = output;
        context.IO.Error = output;

        var program = context.Get<TSpiceMain>();
        var utilities = context.Get<TestUtil>();
        var results = new List<FamilyResult>(families.Count);

        // No options: the families run as the test program runs them when
        // it is started with nothing on its command line.
        var commandLine = stackalloc byte[CommandLineLength];
        new Span<byte>(commandLine, CommandLineLength).Fill((byte)' ');

        var stopped = false;
        try
        {
            utilities.TSETUP(commandLine, CommandLineLength, AddrOf("spice{0-9}{0-9}.log"u8), 19, AddrOf("1.0.0"u8), 5);
        }
        catch (FortranStopException stop)
        {
            output.WriteLine(string.Create(CultureInfo.InvariantCulture, $"The test utilities stopped the program while being set up, with exit code {stop.ExitCode}."));
            stopped = true;
        }

        foreach (var family in families)
        {
            var written = output.GetStringBuilder().Length;
            var started = Stopwatch.GetTimestamp();
            var ok = 0;
            if (stopped)
            {
                output.WriteLine("Not run: the program had stopped.");
            }
            else
            {
                try
                {
                    family.CreateDelegate<Family>(program)(&ok);
                }
                catch (FortranStopException stop)
                {
                    // What a family left behind when it stopped the program
                    // is not a state to run the next one in.
                    output.WriteLine(string.Create(CultureInfo.InvariantCulture, $"The family stopped the program, with exit code {stop.ExitCode}."));
                    ok = 0;
                    stopped = true;
                }
            }

            var result = new FamilyResult(
                family.Name,
                ok != 0,
                Stopwatch.GetElapsedTime(started).TotalSeconds,
                output.GetStringBuilder().ToString(written, output.GetStringBuilder().Length - written));
            results.Add(result);
            report?.Invoke(result);
        }

        if (!stopped)
        {
            try
            {
                utilities.TCLOSE();
            }
            catch (FortranStopException)
            {
                // Closing is the last thing done, and the results are in.
            }
        }

        return results;
    }
}
