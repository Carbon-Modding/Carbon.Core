Run(Path(Home, "Tools", "Build", "runners", "git.cr"));

var target = GetArg(1, "Debug");
var defines = GetArg(2);
var version = GetVariable("VERSION");

Warn($"Target: {target}");
Warn($"Defines: {defines ?? "N/A"}");
Warn($"Version: {version ?? "N/A"}");

Directories.Delete(Path(Home, "Release", ".tmp", target));
Files.Delete(Path(Home, "Release", $"Carbon.{target}.tar.gz"));

DotNet.Run("restore", PathEnquotes(Home, "Carbon.Core"));
DotNet.Run("clean", PathEnquotes(Home, "Carbon.Core"), "--configuration", target);
DotNet.Run("build", PathEnquotes(Home, "Carbon.Core"), "--configuration", target, "--no-restore",
	$"/p:UserConstants=\"{defines}\"", $"/p:UserVersion=\"{version}\"");