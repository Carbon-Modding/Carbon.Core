Log(Home);

Warn("Git Hooks");
{
	Copy.Folder(".githooks", ".git/hooks");
}

Warn("Git Setup");
{
	Git.Run("-C", Home, "submodule", "init");
	Git.Run("-C", Home, "submodule", "update");
	Git.Run("-C", Home, "submodule", "foreach", "git", "checkout");
}

Warn("Building Submodules");
{	
	DotNet.Run("restore", PathEnquotes(Home, "Tools", "DepotDownloader"));
	DotNet.Run("clean", PathEnquotes(Home, "Tools", "DepotDownloader"));
	DotNet.Run("build", PathEnquotes(Home, "Tools", "DepotDownloader"), "--no-restore", "--no-incremental");
}

Run(Path(Home, "Tools", "Build", "runners", "update.cr"));