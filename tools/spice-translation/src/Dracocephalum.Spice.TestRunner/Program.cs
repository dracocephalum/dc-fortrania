using Dracocephalum.Spice.TestRunner;

// Runs the families of the toolkit's own test program, translated, without
// its main program: any of them by name, each in an instance of its own.
//
//   [family ...]     the families to run; all of them when none is named
//   --quick          leaves out the families listed in slow-families.txt
//   --disk           files on disk in the working folder, one family after
//                    another, as the test program itself runs; otherwise
//                    files are kept in memory and families run side by side
//   --jobs <n>       how many run at once; the number of processors by default
//   --times <file>   writes each family's result and time to a file
//   --verbose        prints each family as it ends
//   --list           lists the families and runs none
if (RunnerOptions.Parse(args) is not { } options)
{
    Console.Error.WriteLine("Usage: [--quick] [--disk] [--jobs <n>] [--times <file>] [--verbose] [--list] [family ...]");
    return 2;
}

return Runner.Run(options);
