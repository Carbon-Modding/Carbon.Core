namespace Carbon.Runner.Executors;

public class Copy : Executor
{
	public override string? Name => "Copy";

	[Expose("Copies an entire folder and its subdirectories to a destination")]
	public void Folder(string source, string destination, bool overwrite = true)
	{
		if (!Directory.Exists(source))
		{
			Error($"Could not find source folder: {source}");
			return;
		}

		var folderInfo = new DirectoryInfo(source);
		var folders = folderInfo.GetDirectories();
		Directory.CreateDirectory(destination);

		var files = folderInfo.GetFiles();
		foreach (var file in files)
		{
			var tempPath = InternalRunner.Path(destination, file.Name);
			file.CopyTo(tempPath, overwrite);
			Log($"Copied file: {file.Name}");
		}

		foreach (var subDirectory in folders)
		{
			var tempPath = InternalRunner.Path(destination, subDirectory.Name);
			Folder(subDirectory.FullName, tempPath, overwrite);
			Log($"Copied folder: {subDirectory.Name}");
		}
	}

	[Expose("Copies a specific file to a destination")]
	public void File(string source, string destination, bool overwrite = true)
	{
		if (!System.IO.File.Exists(source))
		{
			Error($"Could not find source file: {source}");
			return;
		}
		System.IO.File.Copy(source, destination, overwrite);
	}
}
