using System.Reflection;
using Carbon.Runner;

InternalRunner.GlobalArgs = args;

if (!InternalRunner.HasArgs(1))
{
	var executors = new List<Executor>();
	var baseExecutor = typeof(Executor);

	foreach (var type in typeof(InternalRunner).Assembly.GetTypes())
	{
		if (baseExecutor != type && baseExecutor.IsAssignableFrom(type))
		{
			executors.Add(Activator.CreateInstance(type) as Executor ?? throw new InvalidOperationException());
		}
	}

	InternalRunner.Error("Missing Carbon runner file!");
	InternalRunner.Log($"Available Executors ({executors.Count:n0})\n{string.Join("\n", executors.Select(x =>
	{
		var result = string.Empty;
		var exposedMethods = x.GetType().GetMethods();

		foreach (var method in exposedMethods)
		{
			var expose = method.GetCustomAttribute<Expose>();
			if (expose == null)
			{
				continue;
			}
			result += $"  {x.Name}.{method.Name}( {string.Join(", ", method.GetParameters().Select(y => $"{y.ParameterType.FullName} {y.Name}"))} ) [{expose.Help}]\n";
		}
		return result;
	}))}");
	return;
}

var file = InternalRunner.GetArg(0);
InternalRunner.Run(file, args, true);
Console.ReadLine();
