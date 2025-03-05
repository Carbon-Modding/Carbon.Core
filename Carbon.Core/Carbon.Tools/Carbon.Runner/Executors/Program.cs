using System.Diagnostics;

namespace Carbon.Runner.Executors;

public class Program : Executor
{
	public override string Name => "Program";

	private string workingDirectory = Environment.CurrentDirectory;
	internal string? programFile;

	[Expose("Starts and runs a program")]
	public override void Run(params string[] args)
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
	}
	[Expose("Starts and runs a program and returns the output string")]
	public override string RunOutput(params string[] args)
	{
		var output = string.Empty;
		try
		{
			Log(string.Join(" ", args));
			var process = Process.Start(new ProcessStartInfo
			{
				FileName = programFile,
				Arguments = string.Join(" ", args),
				WorkingDirectory = workingDirectory,
				UseShellExecute = false,
				RedirectStandardError = true,
				RedirectStandardOutput = true
			});

			using (var reader = process!.StandardOutput)
			{	
				output += reader.ReadToEnd();
			}
			using (var reader = process!.StandardError)
			{
				output += reader.ReadToEnd();
			}
			process!.WaitForExit();
		}
		catch (Exception ex)
		{
			Error($"Failed Run(..) ({ex.Message})\n{ex.StackTrace}");

		}
		return output;
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
