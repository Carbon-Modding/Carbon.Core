var branch = GetArg(0, "public");

Warn($"Branch: {branch}");

DownloadRustFiles("windows");
DownloadRustFiles("linux");

void DownloadRustFiles(string platform)
{
	Log($"Downloading {platform} Rust files..");
	DotNet.Run("run", "--project", PathEnquotes(Home, "Tools", "DepotDownloader", "DepotDownloader"), 
		"-os", platform, 
		"-validate", 
		"-app 258550",
		"-branch", branch, 
		"-filelist", PathEnquotes(Home, "Tools", "Helpers", "258550_refs.txt"),
		"-dir", PathEnquotes(Home, "Rust", platform));
		
	DotNet.Run("run",
		"--project", PathEnquotes(Home, "Carbon.Core", "Carbon.Tools", "Carbon.Publicizer"), 
		"-input", PathEnquotes(Home, "Rust", platform, "RustDedicated_Data", "Managed"),
		"-carbon.rustrootdir", PathEnquotes(Home, "Rust", platform),
		"-carbon.logdir", PathEnquotes(Home, "Rust", platform));
}

DotNet.Run("run", "--project", PathEnquotes(Home, "Carbon.Core", "Carbon.Tools", "Carbon.Generator"),
	"--plugininput", PathEnquotes(Home, "Carbon.Core", "Carbon.Components", "Carbon.Common", "src", "Carbon", "Core"),
	"--pluginoutput", PathEnquotes(Home, "Carbon.Core", "Carbon.Components", "Carbon.Common", "src", "Carbon", "Core", "Core.Plugin-Generated.cs"));

var modules = new System.Collections.Generic.List<string>();
modules.AddRange(Directories.Get(Path(Home, "Carbon.Core", "Carbon.Components", "Carbon.Common", "src", "Carbon", "Modules")));
modules.AddRange(Directories.Get(Path(Home, "Carbon.Core", "Carbon.Components", "Carbon.Modules", "src")));

foreach(var directory in modules)
{
	Log(directory);
	var name = System.IO.Path.GetFileNameWithoutExtension(directory);
	DotNet.Run("run", "--project", PathEnquotes(Home, "Carbon.Core", "Carbon.Tools", "Carbon.Generator"),
		"--plugininput", PathEnquotes(directory),
		"--pluginoutput", PathEnquotes(directory, $"{name}-Generated.cs"),
		"--pluginname", name,
		"--pluginnamespace", "Carbon.Modules",
		"--basename", "module");
}