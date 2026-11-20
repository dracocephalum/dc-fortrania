using System.Globalization;

namespace Dracocephalum.Spice.TestRunner;

/// <summary>What the command line asked for.</summary>
internal sealed record RunnerOptions
{
    /// <summary>Gets a value indicating whether the families listed as slow are left out.</summary>
    public bool IsQuick { get; init; }

    /// <summary>Gets a value indicating whether files are on disk, in the working folder, and the families run one after another.</summary>
    public bool UsesDisk { get; init; }

    /// <summary>Gets a value indicating whether the families are listed and not run.</summary>
    public bool ListsOnly { get; init; }

    /// <summary>Gets a value indicating whether every family's result is printed as it ends.</summary>
    public bool IsVerbose { get; init; }

    /// <summary>Gets how many families run at once when their files are in memory.</summary>
    public int Jobs { get; init; } = Environment.ProcessorCount;

    /// <summary>Gets the file each family's time is written to, or null.</summary>
    public string? TimesFile { get; init; }

    /// <summary>Gets the families asked for by name; all of them when there are none.</summary>
    public IReadOnlyList<string> Names { get; init; } = [];

    /// <summary>Reads a command line.</summary>
    /// <param name="arguments">The arguments.</param>
    /// <returns>The options, or null where the command line is not one the tool takes.</returns>
    public static RunnerOptions? Parse(IReadOnlyList<string> arguments)
    {
        var options = new RunnerOptions();
        var names = new List<string>();
        for (var index = 0; index < arguments.Count; index++)
        {
            switch (arguments[index])
            {
                case "--quick":
                    options = options with { IsQuick = true };
                    break;
                case "--disk":
                    options = options with { UsesDisk = true };
                    break;
                case "--list":
                    options = options with { ListsOnly = true };
                    break;
                case "--verbose":
                    options = options with { IsVerbose = true };
                    break;
                case "--jobs" when index + 1 < arguments.Count
                    && int.TryParse(arguments[index + 1], NumberStyles.None, CultureInfo.InvariantCulture, out var jobs) && jobs > 0:
                    options = options with { Jobs = jobs };
                    index++;
                    break;
                case "--times" when index + 1 < arguments.Count:
                    options = options with { TimesFile = arguments[++index] };
                    break;
                case var name when !name.StartsWith('-'):
                    names.Add(name.ToUpperInvariant());
                    break;
                default:
                    return null;
            }
        }

        return options with { Names = names };
    }
}
