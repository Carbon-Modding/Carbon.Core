using System.Diagnostics;

namespace Carbon.Runner.Executors;

public class DotNet : Executor
{
	public override string Name => "DotNet";
	public override string Program => "dotnet";
	public override async ValueTask Run(params string[] args)
	{
		InternalRunner.Log($"{Program} {(args.Length == 0 ? string.Empty : $"[{string.Join(" ", args)}]")}");
		await Process.Start(new ProcessStartInfo
		{
			FileName = Program,
			Arguments = string.Join(" ", args),
			WorkingDirectory = workingDirectory
		})!.WaitForExitAsync();
	}

	public string workingDirectory;
	public DotNet WorkingDirectory(string directory)
	{
		workingDirectory = directory;
		return this;
	}
}
