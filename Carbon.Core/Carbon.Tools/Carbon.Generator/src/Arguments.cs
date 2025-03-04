using CommandLine;

/*
 *
 * Copyright (c) 2022-2023 Carbon Community
 * All rights reserved.
 *
 */

public class CommandLineArguments
{
	[Option("plugininput", Required = false, HelpText = "Path to the plugin folder")]
	public string PluginInput { get; set; }

	[Option("pluginoutput", Required = false, HelpText = "File destination path")]
	public string PluginOutput { get; set; }

	[Option("pluginname", Required = false, HelpText = "Plugin name")]
	public string PluginName { get; set; } = @"CorePlugin";

	[Option("pluginnamespace", Required = false, HelpText = "Plugin namespace")]
	public string PluginNamespace { get; set; } = @"Carbon.Core";

	[Option("basecall", Required = false, HelpText = "InternalCallHook base call as default")]
	public bool BaseCall { get; set; } = false;

	[Option("basename", Required = false, HelpText = "InternalCallHook base name")]
	public string BaseName { get; set; } = "plugin";
}
