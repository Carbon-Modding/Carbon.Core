using System;
using System.IO;
using System.Linq;
using Carbon.Utilities;
using Startup;

public sealed class Program
{
	public static void Main(string[] args)
	{
		Config.Singleton ??= new();
		Config.Singleton.ForceEnsurePublicizedAssembly("Assembly-CSharp.dll");
		Config.Singleton.ForceEnsurePublicizedAssembly("Facepunch.Console.dll");
		Config.Singleton.ForceEnsurePublicizedAssembly("Facepunch.Network.dll");
		Config.Singleton.ForceEnsurePublicizedAssembly("Facepunch.Nexus.dll");
		Config.Singleton.ForceEnsurePublicizedAssembly("Rust.Clans.Local.dll");
		Config.Singleton.ForceEnsurePublicizedAssembly("Rust.Harmony.dll");
		Config.Singleton.ForceEnsurePublicizedAssembly("Rust.Global.dll");
		Config.Singleton.ForceEnsurePublicizedAssembly("Rust.Data.dll");

		var input = args[1];
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
					using var memoryStream = new MemoryStream();
					patch.assembly.Write(memoryStream);
					patch.processed = memoryStream.ToArray();
					File.WriteAllBytes(file, patch.processed);
					Console.WriteLine($"Patched {file}");
					continue;
				}

				patch = new Patch(Path.GetDirectoryName(file), name);
				if (patch.Execute())
				{
					using var memoryStream = new MemoryStream();
					patch.assembly.Write(memoryStream);
					patch.processed = memoryStream.ToArray();
					File.WriteAllBytes(file, patch.processed);
					Console.WriteLine($"Patched {file}");
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
