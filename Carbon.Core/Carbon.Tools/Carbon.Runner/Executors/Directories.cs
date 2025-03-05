namespace Carbon.Runner.Executors;

public class Directories : Executor
{
	public override string? Name => "Directories";

	[Expose("Gets a list of all files in a directory")]
	public string[] Get(string folder, string search = "*") => Directory.GetDirectories(folder, search);
}
