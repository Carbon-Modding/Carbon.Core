using System.Reflection;
using Microsoft.CodeAnalysis;

namespace Carbon.Runner;

public abstract class Executor
{
	public bool IsQuiet;
	public void SetQuiet(bool wants) => IsQuiet = wants;

	public virtual string? Name => null;
	public virtual ValueTask Run(params string[] args)
	{
		InternalRunner.Warn($"Executor {Name}.Run(..) runner is not implemented!");
		return default;
	}

	public void Log(object message)
	{
		if (IsQuiet) return;
		InternalRunner.Log($"{Name?.ToUpperInvariant()}| {message}");
	}
	public void Warn(object message)
	{
		InternalRunner.Warn($"{Name?.ToUpperInvariant()}| {message}");
	}
	public void Error(object message)
	{
		InternalRunner.Error($"{Name?.ToUpperInvariant()}| {message}");
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
