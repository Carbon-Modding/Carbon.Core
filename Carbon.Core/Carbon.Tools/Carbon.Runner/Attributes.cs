namespace Carbon.Runner;

[AttributeUsage(AttributeTargets.Method)]
public class Expose(string help) : Attribute
{
	public string Help = help;
}
