var whitelisted = new[]
{
	"Assembly-CSharp.dll",
	"Facepunch.Console.dll",
	"Facepunch.Network.dll",
	"Facepunch.Nexus.dll",
	"Rust.Clans.Local.dll",
	"Rust.Harmony.dll",
	"Rust.Global.dll",
	"Rust.Data.dll",
};
var input = args[1];
var patchableFiles = Directory.EnumerateFiles(input);

foreach (var file in patchableFiles)
{
	if (!whitelisted.Any(x => file.Contains(x, StringComparison.InvariantCultureIgnoreCase)))
	{
		continue;
	}
	try
	{
		new Publicizer(file);
	}
	catch (Exception ex)
	{
		Console.WriteLine(ex.ToString());
	}
}
