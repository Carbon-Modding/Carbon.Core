var temp = Path(Home, "Carbon.Core", ".tmp");
var localTag = GetArg(1);

Directories.Create(temp);

Git.Run("fetch", "--tags");
Git.SetQuiet(true);

var tags = (await Git.RunOutput("tag", "-l")).Split('\n');
foreach(var tag in tags)
{
	Git.Run("tag", "-d", tag);
}

Git.SetQuiet(false);
Git.Run("fetch", "--tags");

Files.Create(Path(temp, ".gitbranch"), await Git.RunOutput("branch", "--show-current"));
Files.Create(Path(temp, ".gitchs"), await Git.RunOutput("rev-parse", "--short", "HEAD"));
Files.Create(Path(temp, ".gitchl"), await Git.RunOutput("rev-parse", "--long", "HEAD"));
Files.Create(Path(temp, ".gitauthor"), await Git.RunOutput("show", "-s", "--format=\"%an\"", "HEAD"));
Files.Create(Path(temp, ".gitcomment"), await Git.RunOutput("log -1", "--pretty=\"%B\"", "HEAD"));
Files.Create(Path(temp, ".gitdate"), await Git.RunOutput("log -1", "--format=\"%ci\"", "HEAD"));

if(string.IsNullOrEmpty(localTag))
{
	Files.Create(Path(temp, ".gittag"), await Git.RunOutput("describe", "--tags"));
}
else
{
	Files.Create(Path(temp, ".gittag"), localTag);
}

Files.Create(Path(temp, ".giturl"), await Git.RunOutput("remote", "get-url", "origin"));
Files.Create(Path(temp, ".gitchanges"), await Git.RunOutput("log -1", "--name-status", "--format=\"\""));
