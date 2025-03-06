using System.Formats.Tar;
using System.IO.Compression;

namespace Carbon.Runner.Executors;

public class Archive : Executor
{
	public override string Name => "Archive";

	[Expose("Creates a new ZIP file of a directory")]
	public void Zip(string directory, string destination)
	{
		if (File.Exists(destination))
		{
			File.Delete(destination);
		}
		ZipFile.CreateFromDirectory(directory, destination, CompressionLevel.Optimal, false);
		Warn($"Created ZIP file: {destination}");
	}

	[Expose("Creates a new TAR file of a directory")]
	public void Tar(string directory, string destination)
	{
		if (File.Exists(destination))
		{
			File.Delete(destination);
		}
		TarFile.CreateFromDirectory(directory, destination, false);
		Warn($"Created TAR file: {destination}");
	}
}
