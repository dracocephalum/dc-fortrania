using Dracocephalum.Fortrania.Cli;

// The command a person or a build runs: everything it does is in
// CommandLine, so that a test runs it as this does.
return CommandLine.Run(args, Console.Out, Console.Error, Directory.GetCurrentDirectory());
