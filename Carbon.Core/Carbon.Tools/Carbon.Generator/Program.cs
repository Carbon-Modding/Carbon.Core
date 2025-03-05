using System;
using CommandLine;

Parser.Default.ParseArguments<CommandLineArguments>(args)
	.WithNotParsed(x => Environment.Exit(1))
	.WithParsed(x => Generator.Arguments = x);

Generator.Generate();
