using System.Diagnostics;

namespace Carbon.Runner.Executors;

public class Program : Executor
{
	public override string Name => "Program";

	private string workingDirectory = Environment.CurrentDirectory;
	internal string programFile;

	[Expose("Starts and runs a program")]
	public override ValueTask Run(params string[] args)
	{
		try
		{
			Log(string.Join(" ", args));
			Process.Start(new ProcessStartInfo
			{
				FileName = programFile,
				Arguments = string.Join(" ", args),
				WorkingDirectory = workingDirectory,
				UseShellExecute = false
			})!.WaitForExit();
		}
		catch (Exception ex)
		{
			Error($"Failed Run(..) ({ex.Message})\n{ex.StackTrace}");
		}
		return default;
	}

	[Expose("Overrides the working directory specifically for this process")]
	public Program WorkingDirectory(string workingDirectory)
	{
		this.workingDirectory = workingDirectory;
		return this;
	}

	[Expose("Updates the program to be executed")]
	public Program Setup(string programFile)
	{
		this.programFile = programFile;
		return this;
	}
}
