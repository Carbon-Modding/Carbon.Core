using CommandLine;

internal class CommandLineArguments
{
	[Option("input", Required = true, HelpText = "Input assembly file to be publicized")]
	public string Input { get; set; }
}
