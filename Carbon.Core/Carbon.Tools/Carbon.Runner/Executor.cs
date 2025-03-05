using System.Reflection;
using Microsoft.CodeAnalysis;

namespace Carbon.Runner;

public abstract class Executor
{
	public static string[] Args => Environment.GetCommandLineArgs().Skip(2).ToArray();
	public virtual string Name => "Default";
	public virtual string Program => "Default.exe";
	public abstract ValueTask Run(params string[] args);

	public static string BuildRunner(string source)
	{
		return $@"using System;
using System.Threading;
using System.Threading.Tasks;

public class _Runner : Carbon.Runner.InternalRunner
{{
	public static async ValueTask Run(string[] args)
	{{
		try
		{{
			{source}
		}}
		catch(Exception ex)
		{{
			Error($""{{ex.Message}}\n{{ex.StackTrace}}"");
		}}
	}}
}}";
	}

	public static void RegisterReference(List<MetadataReference> references, string name)
	{
		var assembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(x => x.GetName().Name!.Equals(name, StringComparison.OrdinalIgnoreCase));

		if (assembly == null)
		{
			InternalRunner.Error($"Couldn't register reference: {name}");
			return;
		}

		references.Add(MetadataReference.CreateFromFile(assembly.Location));
	}

}
