using System.Reflection;
using System.Text;
using Carbon.Runner;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;

var file = args[1];

if (!File.Exists(file))
{
	Console.WriteLine($"Runner file {file} not found");
	return;
}

var parseOptions = new CSharpParseOptions(LanguageVersion.Preview);
var tree = CSharpSyntaxTree.ParseText(Executor.BuildRunner(File.ReadAllText(file)), options: parseOptions, file, Encoding.UTF8);
var trees = new List<SyntaxTree>() { tree };
var options = new CSharpCompilationOptions(
	OutputKind.DynamicallyLinkedLibrary,
	optimizationLevel: OptimizationLevel.Debug,
	deterministic: true, warningLevel: 4,
	allowUnsafe: true
);
var references = new List<MetadataReference>
{
	MetadataReference.CreateFromFile(Executor.FindAssembly("System.Private.CoreLib")!.Location),
	MetadataReference.CreateFromFile(Executor.FindAssembly("System.Runtime")!.Location),
	MetadataReference.CreateFromFile(Executor.FindAssembly("System.Collections.Immutable")!.Location),
	MetadataReference.CreateFromFile(Executor.FindAssembly("System.Collections")!.Location),
	MetadataReference.CreateFromFile(Executor.FindAssembly("System.Console")!.Location),
	MetadataReference.CreateFromFile(Executor.FindAssembly("System.Threading")!.Location),
	MetadataReference.CreateFromFile(Executor.FindAssembly("System.Memory")!.Location),
	MetadataReference.CreateFromFile(Executor.FindAssembly("System.Linq")!.Location),
	MetadataReference.CreateFromFile(Executor.FindAssembly("Carbon.Runner")!.Location)

};
var compilation = CSharpCompilation.Create($"{Guid.NewGuid():N}", trees, references, options);
using var dllStream = new MemoryStream();
var emit = compilation.Emit(dllStream, options: new EmitOptions(debugInformationFormat: DebugInformationFormat.Embedded));
if (!emit.Success)
{
	InternalRunner.Error($"Compilation failed for '{file}'");
	foreach (var diagnostic in emit.Diagnostics)
	{
		if (diagnostic.Severity != DiagnosticSeverity.Error)
		{
			continue;
		}
		InternalRunner.Warn($" {diagnostic.Severity}|{diagnostic.Id}  {diagnostic.GetMessage()}");
	}

	return;
}
Console.WriteLine("Runner compiled successfully");

var assembly = Assembly.Load(dllStream.ToArray());
var runner = assembly.GetType("_Runner");
runner?.GetMethod("Run")?.Invoke(null, new object[] { args.Skip(2).ToArray() });
