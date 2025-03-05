using System.Reflection;
using System.Text;
using Carbon.Runner.Executors;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;

namespace Carbon.Runner;

public class InternalRunner
{
	public static Carbon.Runner.Executors.Program Program = new();
	public static DotNet DotNet = new();
	public static Git Git = new();
	public static Copy Copy = new();

	public static string[] GlobalArgs { get; set; }
	public static string Home => Environment.CurrentDirectory;
	public static void SetHome(string directory)
	{
		Environment.CurrentDirectory = directory;
	}
	public static string Path(params string[] paths) => System.IO.Path.Combine(paths);
	public static string PathEnquotes(params string[] paths) => $"\"{Path(paths)}\"";

	public static bool HasArgs(int minArgs) => GlobalArgs.Length >= minArgs;
	public static string GetArg(int index, string defaultValue = null)
	{
		if (index >= GlobalArgs.Length)
		{
			return defaultValue;
		}
		return GlobalArgs[index];
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
	public static async ValueTask Run(string[] args)
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
		var runner = assembly.GetType("_Runner");
		runner?.GetMethod("Run")?.Invoke(null, new object[] { args.Skip(2).ToArray() });
	}
}
