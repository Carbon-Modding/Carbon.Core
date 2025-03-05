using System.IO;

namespace Carbon.Runner.Executors;

public class Directories : Executor
{
	public override string? Name => "Directories";

	[Expose("Gets a list of all files in a directory")]
	public string[] Get(string directory, string search = "*") => Directory.GetDirectories(directory, search);

	[Expose("Ensure the directory exists")]
	public void Create(string directory)
	{
		if (Directory.Exists(directory))
		{
			Log($"Directory '{directory}' already exists. Skipping..");
			return;
		}

		Directory.CreateDirectory(directory);
		Warn($"Created folder: {directory}");
	}

	[Expose("Deletes a directory if the directory exists")]
	public void Delete(string directory)
	{
		if (!Directory.Exists(directory))
		{
			Log($"Directory '{directory}' not found. Skipping..");
			return;
		}

		Directory.Delete(directory, true);
		Warn($"Deleted directory: '{directory}'");
	}
}
