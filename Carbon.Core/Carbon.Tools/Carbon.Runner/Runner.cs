using System.Reflection;
using System.Text;
using Carbon.Runner.Executors;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;

namespace Carbon.Runner;

public class InternalRunner
{
	public static Executors.Program Program = new();
	public static DotNet DotNet = new();
	public static Git Git = new();
	public static Copy Copy = new();
	public static Files Files = new();
	public static Directories Directories = new();
	public static Archive Archive = new();

	public string[] Args { get; set; }
	public static string Home => Environment.CurrentDirectory;
	public static void SetHome(string directory)
	{
		Environment.CurrentDirectory = directory;
	}
	public static string Path(params string[] paths) => System.IO.Path.Combine(paths);
	public static string PathEnquotes(params string[] paths) => $"\"{Path(paths)}\"";

	public bool HasArgs(int minArgs) => Args.Length >= minArgs;
	public string GetArg(int index, string? defaultValue = null)
	{
		if (index >= Args.Length)
		{
			return defaultValue;
		}
		return Args[index];
	}
	public static void Write(object message, ConsoleColor color)
	{
		var originalColor = Console.ForegroundColor;
		Console.ForegroundColor = color;
		Console.WriteLine(message);
		Console.ForegroundColor = originalColor;
	}
	public static void Log(object message) => Write(message, ConsoleColor.DarkGray);
	public static void Warn(object message) => Write(message, ConsoleColor.DarkYellow);
	public static void Error(object message) => Write(message, ConsoleColor.DarkRed);

	public static string GetVariable(string variable) => Environment.GetEnvironmentVariable(variable)!;
	public static void Run(string file, params string[] args)
	{
		Run(file, args, false);
	}
	public static void Exit(int code = 0) => Environment.Exit(code);

	internal static string Build(string source, bool shouldExit)
	{
		return $@"using System;
using System.Threading;
using System.Threading.Tasks;

public class _Runner : Carbon.Runner.InternalRunner
{{
	public async ValueTask Run(string[] args)
	{{
		try
		{{
			{source}
		}}
		catch(Exception ex)
		{{
			Error($""{{ex.Message}}\n{{ex.StackTrace}}"");
			{(shouldExit ? "Exit(1);" : null)}
			return;
		}}
		{(shouldExit ? "Exit(0);" : null)}
	}}
}}";
	}
	internal static void Run(string file, string[] args, bool shouldExit)
	{
		if (args.Length < 1)
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

			Error("Missing Carbon runner file!");
			Log($"Available Executors ({executors.Count:n0})\n{string.Join("\n", executors.Select(x =>
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
			if (shouldExit)
			{
				Exit(1);
			}
			return;
		}

		if (!File.Exists(file))
		{
			Error($"Runner file '{file}' not found");
			return;
		}

		var parseOptions = new CSharpParseOptions(LanguageVersion.Preview);
		var tree = CSharpSyntaxTree.ParseText(Build(File.ReadAllText(file), shouldExit), options: parseOptions, file, Encoding.UTF8);
		var trees = new List<SyntaxTree>() { tree };
		var options = new CSharpCompilationOptions(
			OutputKind.DynamicallyLinkedLibrary,
			optimizationLevel: OptimizationLevel.Debug,
			deterministic: true, warningLevel: 4,
			allowUnsafe: true
		);
		var references = new List<MetadataReference>();
		Executor.RegisterReference(references, "System.Private.CoreLib");
		Executor.RegisterReference(references, "System.Runtime");
		Executor.RegisterReference(references, "System.Collections.Immutable");
		Executor.RegisterReference(references, "System.Collections");
		Executor.RegisterReference(references, "System.Threading");
		Executor.RegisterReference(references, "System.Memory");
		Executor.RegisterReference(references, "System.Linq");
		Executor.RegisterReference(references, "Carbon.Runner");

		var compilation = CSharpCompilation.Create($"{Guid.NewGuid():N}", trees, references, options);
		using var dllStream = new MemoryStream();
		var emit = compilation.Emit(dllStream, options: new EmitOptions(debugInformationFormat: DebugInformationFormat.Embedded));
		if (!emit.Success)
		{
			Error($"Compilation failed for '{file}'");
			foreach (var diagnostic in emit.Diagnostics)
			{
				if (diagnostic.Severity != DiagnosticSeverity.Error)
				{
					continue;
				}
				Warn($" {diagnostic.Severity}|{diagnostic.Id}  {diagnostic.GetMessage()}");
			}
			Exit(1);
			return;
		}

		var assembly = Assembly.Load(dllStream.ToArray());
		var runnerType = assembly.GetType("_Runner");
		var runner = Activator.CreateInstance(runnerType!) as InternalRunner;
		runner!.Args = args;
		runnerType?.GetMethod("Run")?.Invoke(runner, [args.Skip(2).ToArray()]);
	}
}
