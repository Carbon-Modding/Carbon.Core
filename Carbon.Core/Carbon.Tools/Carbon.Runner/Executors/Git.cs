namespace Carbon.Runner.Executors;

public class Git : Program
{
	public override string Name => "Git";

	public Git()
	{
		programFile = "git";
	}
}
