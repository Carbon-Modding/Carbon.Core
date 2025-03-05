namespace Carbon.Runner.Executors;

public class Files : Executor
{
	public override string? Name => "Files";
	
	[Expose("Gets a list of all files in a directory")]
	public string[] Get(string folder, string search = "*") => Directory.GetFiles(folder, search);

	[Expose("Create a file with text inside of it")]
	public void Create(string target, string content) => File.WriteAllText(target, content);

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
