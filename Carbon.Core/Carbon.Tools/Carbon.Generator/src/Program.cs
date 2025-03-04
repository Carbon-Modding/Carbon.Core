using System;
using CommandLine;

public sealed class Program
{
	public static CommandLineArguments Arguments;

	public static void Main(string[] args)
	{
		Parser.Default.ParseArguments<CommandLineArguments>(args)
			.WithNotParsed(x => Environment.Exit(1))
			.WithParsed(x => Arguments = x);

		Generator.Generate();
	}
}
