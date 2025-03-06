using CommandLine;

public class CommandLineArguments
{
	[Option("plugininput", Required = false, HelpText = "Path to the plugin folder")]
	public string PluginInput { get; set; }

	[Option("pluginnamespace", Required = false, HelpText = "Plugin namespace")]
	public string PluginNamespace { get; set; } = @"Carbon.Core";

	[Option("basecall", Required = false, HelpText = "InternalCallHook base call as default")]
	public bool BaseCall { get; set; } = false;

	[Option("basename", Required = false, HelpText = "InternalCallHook base name")]
	public string BaseName { get; set; } = "plugin";
}
