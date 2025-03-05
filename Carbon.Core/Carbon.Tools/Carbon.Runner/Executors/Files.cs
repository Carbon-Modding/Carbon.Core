namespace Carbon.Runner.Executors;

public class Files : Executor
{
	public override string? Name => "Files";
	
	[Expose("Gets a list of all files in a directory")]
	public string[] Get(string folder, string search = "*") => Directory.GetFiles(folder, search);

	[Expose("Create a file with text inside of it")]
	public void Create(string target, string content) => File.WriteAllText(target, content);
}
