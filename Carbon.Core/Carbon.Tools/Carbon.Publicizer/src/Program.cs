using System;
using System.IO;
using System.Linq;
using Carbon.Core;
using Carbon.Extensions;
using Carbon.Utilities;
using Startup;

public sealed class Program
{
	public static void Main(string[] args)
	{
		Config.Init();

		Console.WriteLine(Defines.GetRustRootFolder());
		Console.WriteLine(Defines.GetRustManagedFolder());

		var input = CommandLineEx.GetArgumentResult("-input");
		var patchableFiles = Directory.EnumerateFiles(input);

		Patch.Init();
		foreach (var file in patchableFiles)
		{
			try
			{
				var name = Path.GetFileName(file);
				var patch = Entrypoint.Patches.FirstOrDefault(x => x.fileName.Equals(name));

				if (patch != null && patch.Execute())
				{
					patch.Write(file);
					continue;
				}

				if (!Config.Singleton.Publicizer.PublicizedAssemblies.Any(x => name.StartsWith(x, StringComparison.OrdinalIgnoreCase)))
				{
					continue;
				}

				patch = new Patch(Path.GetDirectoryName(file), name);
				if (patch.Execute())
				{
					patch.Write(file);
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine(ex.ToString());
			}
		}
		Patch.Uninit();
	}
}
