var target = GetArg(1, "release");

Log($"Update: {target}");

DownloadRustFiles("windows");
DownloadRustFiles("linux");

void DownloadRustFiles(string platform)
{
	Log($"Downloading {platform} Rust files..");
	DotNet.Run("run", "--project", PathEnquotes(Home, "Tools", "DepotDownloader", "DepotDownloader"), 
		"-os", platform, 
		"-validate", 
		"-app 258550",
		"-branch", target, 
		"-filelist", PathEnquotes(Home, "Tools", "Helpers", "258550_refs.txt"),
		"-dir", PathEnquotes(Home, "Rust", platform));
		
	DotNet.Run("run",
		"--project", PathEnquotes(Home, "Carbon.Core", "Carbon.Tools", "Carbon.Publicizer"), 
		"-input", PathEnquotes(Home, "Rust", platform, "RustDedicated_Data", "Managed"),
		"-carbon.rustrootdir", PathEnquotes(Home, "Rust", platform),
		"-carbon.logdir", PathEnquotes(Home, "Rust", platform));
}
