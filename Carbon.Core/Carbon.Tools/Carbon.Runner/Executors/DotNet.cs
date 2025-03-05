namespace Carbon.Runner.Executors;

public class DotNet : Program
{
	public override string Name => "DotNet";

	public DotNet()
	{
		programFile = "dotnet";
	}
}
