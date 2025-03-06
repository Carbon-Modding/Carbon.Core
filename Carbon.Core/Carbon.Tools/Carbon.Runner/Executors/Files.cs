namespace Carbon.Runner.Executors;

public class Files : Executor
{
	public override string? Name => "Files";
	
	[Expose("Gets a list of all files in a directory")]
	public string[] Get(string folder, string search = "*") => Directory.GetFiles(folder, search);

	[Expose("Create a file with text inside of it")]
	public void Create(string target, string content) => File.WriteAllText(target, content);

	[Expose("Copies a file if the file exists")]
	public void Copy(string target, string destination)
	{
		if (!File.Exists(target))
		{
			Error($"File '{target}' not found!");
			return;
		}

		if (Directory.Exists(destination))
		{
			destination = Path.Combine(destination, Path.GetFileName(target));
		}

		var alreadyExisted = File.Exists(destination);
		File.Copy(target, destination, true);
		Warn($"{(alreadyExisted ? "Overwrote" : "Copied")} file to '{destination}'");
	}

	[Expose("Deletes a file if the file exists")]
	public void Delete(string target)
	{
		if (!File.Exists(target))
		{
			Log($"File '{target}' not found. Skipping..");
			return;
		}

		File.Delete(target);
		Warn($"Deleted file: '{target}'");
	}
}
