using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using API.Events;
using Carbon.Base;
using Carbon.Contracts;
using Carbon.Core;
using Carbon.Extensions;
using Carbon.Jobs;
using Facepunch;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Plugins;

namespace Carbon.Managers;

public class ScriptLoader : IScriptLoader
{
	public const int BusyFileAttempts = 10;

	public ISource InitialSource => Sources?.Count > 0 ? Sources[0] : null;

	public bool BypassFileNameChecks { get; set; }

	public List<IScript> Scripts { get; set; } = [];
	public List<ISource> Sources { get; set; } = [];

	public bool IsCore { get; set; }
	public bool IsExtension { get; set; }

	public bool HasFinished { get; set; }
	public bool HasRequires { get; set; }

	public IBaseProcessor.IProcess Process { get; set; }
	public ModLoader.Package Mod { get; set; }
	public IBaseProcessor.IParser Parser { get; set; }
	public ScriptCompilationThread AsyncLoader { get; set; } = new();

	public void Load()
	{
		if (InitialSource == null || string.IsNullOrEmpty(InitialSource.FilePath))
		{
			Clear();
			return;
		}

		try
		{
			var directory = Path.GetDirectoryName(InitialSource.FilePath);
			IsExtension = directory.EndsWith("extensions");

			Community.Runtime.ScriptProcessor.StartCoroutine(Compile());
		}
		catch (Exception exception)
		{
			Logger.Error($"Failed loading script '{InitialSource.FilePath}':", exception);
		}
	}

	public static void LoadAll(IEnumerable<string> except = null)
	{
		var config = Community.Runtime.Config;
		var extensionPlugins = OsEx.Folder.GetFilesWithExtension(Defines.GetExtensionsFolder(), "cs");
		var plugins = OsEx.Folder.GetFilesWithExtension(Defines.GetScriptsFolder(), "cs", option: config.Watchers.ScriptWatcherOption);
		var zipPlugins = OsEx.Folder.GetFilesWithExtension(Defines.GetScriptsFolder(), "cszip", option: config.Watchers.ScriptWatcherOption);
		var count = 0;

		ExecuteProcess(Community.Runtime.ScriptProcessor, false, except, ref count, extensionPlugins, plugins);
		ExecuteProcess(Community.Runtime.ZipScriptProcessor, false, except, ref count, zipPlugins);

#if DEBUG
		var zipDevPlugins = OsEx.Folder.GetFilesWithExtension(Defines.GetZipDevFolder(), "cs", option: SearchOption.AllDirectories);
		ExecuteProcess(Community.Runtime.ZipDevScriptProcessor, true, except, ref count, zipDevPlugins);
#endif

		if(count == 0)
		{
			ModLoader.IsBatchComplete = true;
			Community.Runtime.Events.Trigger(CarbonEvent.AllPluginsLoaded, EventArgs.Empty);
		}

		static void ExecuteProcess(IScriptProcessor processor, bool folderMode, IEnumerable<string> except, ref int count, params string[][] folders)
		{
			processor.Clear();

			foreach (var files in folders)
			{
				foreach (var file in files)
				{
					if (processor.IsBlacklisted(file) || (except != null && except.Any(x => file.Contains(x))))
					{
						continue;
					}

					var folder = Path.GetDirectoryName(file);

					var id = folderMode ? folder : Path.GetFileNameWithoutExtension(file);

					if (processor.InstanceBuffer.ContainsKey(id))
					{
						continue;
					}

					var plugin = new ScriptProcessor.Script { File = folderMode ? folder : file };
					processor.InstanceBuffer.Add(id, plugin);
					count++;
				}
			}

			foreach (var plugin in processor.InstanceBuffer)
			{
				plugin.Value.MarkDirty();
			}

			Array.Clear(folders, 0, folders.Length);
			folders = null;
		}
	}

	public void Clear()
	{
		if (Scripts != null)
		{
			for (int i = 0; i < Scripts.Count; i++)
			{
				var plugin = Scripts[i];
				if (plugin.IsCore || plugin.Instance == null) continue;

				plugin.Instance.Package.Plugins?.RemoveAll(x => x == plugin.Instance);

				if (plugin.Instance.IsExtension)
				{
					ScriptCompilationThread._clearExtensionPlugin(plugin.Instance.FilePath);
				}

				try
				{
					ModLoader.UninitializePlugin(plugin.Instance);
				}
				catch (Exception ex) { Logger.Error($"Failed unloading '{plugin.Instance}'", ex); }
			}

			if (Scripts.Count > 0)
			{
				Scripts.RemoveAll(x => !x.IsCore);
			}
		}

		Dispose();
	}

	IEnumerator ReadFileAsync(string filePath, Action<string> onRead)
	{
		var task = Task.Run(async () =>
		{
			var fileInfo = new FileInfo(filePath);
			var inUse = true;
			var success = true;
			var attempts = 0;

			while (inUse)
			{
				inUse = !RunFileUseChecks();

				if (!inUse)
				{
					break;
				}

				attempts++;
				await AsyncEx.WaitForSeconds(0.2f);

				if (attempts < BusyFileAttempts)
				{
					continue;
				}

				inUse = false;
				success = false;
				Logger.Warn($"Failed compiling '{InitialSource.ContextFileName}' due to it being in use.");
			}

			if (success && !inUse)
			{
				using var reader = new StreamReader(filePath, detectEncodingFromByteOrderMarks: true);
				return await reader.ReadToEndAsync();
			}
			else
			{
				return null;
			}

			bool RunFileUseChecks()
			{
				try
				{
					using var stream = fileInfo.Open(FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);
					stream.Close();
					return true;
				}
				catch (IOException)
				{
					return false;
				}
			}
		});

		while (!task.IsCompleted)
		{
			yield return null;
		}

		onRead?.Invoke(task.Result);
	}

	public IEnumerator Compile()
	{
		if (string.IsNullOrEmpty(InitialSource.Content) && !string.IsNullOrEmpty(InitialSource.FilePath) && OsEx.File.Exists(InitialSource.FilePath))
		{
			yield return ReadFileAsync(InitialSource.FilePath, content =>
			{
				if(InitialSource == null || string.IsNullOrEmpty(content))
				{
					return;
				}

				InitialSource.Content = content;
			});
		}

		if (Parser != null && Sources != null)
		{
			for(int i = 0; i < Sources.Count; i++)
			{
				var source = Sources[i];
				Parser.Process(source.FilePath, source.Content, out var content);

				yield return null;

				if (!string.IsNullOrEmpty(content))
				{
					Sources[i] = new BaseSource
					{
						ContextFilePath = source.ContextFilePath,
						ContextFileName = source.ContextFileName,
						FilePath = source.FilePath,
						FileName = source.FileName,
						Content = content
					};
				}
			}
		}

		if (Sources == null || Sources.Count == 0)
		{
			HasFinished = true;
			// Logger.Warn("Attempted to compile an empty string of source code.");
			yield break;
		}

		var lines = Sources.Where(x => !string.IsNullOrEmpty(x.Content)).SelectMany(x => x.Content.Split('\n'));
		var resultReferences = Facepunch.Pool.Get<List<string>>();
		var resultRequires = Facepunch.Pool.Get<List<string>>();

		if (lines != null)
		{
			foreach (var line in lines)
			{
				try
				{
					if (line.StartsWith("// Reference:") || line.StartsWith("//Reference:"))
					{
						var @ref = $"{line.Replace("// Reference:", "").Replace("//Reference:", "")}".Trim();
						resultReferences.Add(@ref);
						Logger.Debug($" Added reference: {@ref}");
					}
				}
				catch { }
				try
				{
					if (line.StartsWith("// Requires:") || line.StartsWith("//Requires:"))
					{

						var @ref = $"{line.Replace("// Requires:", "").Replace("//Requires:", "")}".Trim();
						resultRequires.Add(@ref);
						Logger.Debug($" Added required plugin: {@ref}");
					}
				}
				catch { }
			}
		}

		yield return null;

		lines = null;

		if (AsyncLoader != null)
		{
			AsyncLoader.Sources = Sources;
			AsyncLoader.References = resultReferences?.ToArray();
			AsyncLoader.Requires = resultRequires?.ToArray();
			AsyncLoader.IsExtension = IsExtension;
		}
		Facepunch.Pool.FreeUnmanaged(ref resultReferences);
		Facepunch.Pool.FreeUnmanaged(ref resultRequires);

		if (AsyncLoader != null) HasRequires = AsyncLoader.Requires.Length > 0;

		yield return null;

		while (HasRequires && !Community.Runtime.ScriptProcessor.AllNonRequiresScriptsComplete() && !IsExtension && !Community.Runtime.ScriptProcessor.AllExtensionsComplete())
		{
			yield return null;
		}

		var requires = Facepunch.Pool.Get<List<Plugin>>();
		var noRequiresFound = false;
		if (AsyncLoader != null)
		{
			foreach (var require in AsyncLoader.Requires)
			{
				var plugin = Community.Runtime.Core.plugins.Find(require);
				if (plugin == null)
				{
					Logger.Warn($"Couldn't find required plugin '{require}' for '{(!string.IsNullOrEmpty(InitialSource.ContextFilePath) ? Path.GetFileNameWithoutExtension(InitialSource.ContextFilePath) : "<unknown>")}'");
					noRequiresFound = true;
				}
				else
				{
					requires.Add(plugin);
				}
			}
		}

		yield return null;

		if (noRequiresFound)
		{
			ModLoader.AddPostBatchFailedRequiree(InitialSource.ContextFilePath);
			HasFinished = true;
			Facepunch.Pool.FreeUnmanaged(ref requires);

			if (Community.AllProcessorsFinalized)
			{
				ModLoader.IsBatchComplete = true;
			}
			yield break;
		}

		yield return null;

		var requiresResult = requires.ToArray();

		AsyncLoader?.Start();

		while (AsyncLoader != null && !AsyncLoader.IsDone)
		{
			yield return null;
		}

		if (AsyncLoader == null)
		{
			HasFinished = true;
			yield break;
		}

		yield return null;

		if (AsyncLoader != null && AsyncLoader.Assembly == null)
		{
			if (AsyncLoader.Exceptions != null && AsyncLoader.Exceptions.Count > 0)
			{
				Logger.Error($"Failed compiling '{AsyncLoader.InitialSource.ContextFileName}':");
				for (int i = 0; i < AsyncLoader.Exceptions.Count; i++)
				{
					var error = AsyncLoader.Exceptions[i];
					var print = $"{error.Error.ErrorText} [{error.Error.ErrorNumber}]\n     ({error.Error.FileName} {error.Error.Column} line {error.Error.Line})";
					Logger.Error($"  {i + 1:n0}. {print}");
				}

				var compilationFailure = ModLoader.GetCompilationResult(InitialSource.ContextFilePath);
				compilationFailure.Clear();

				compilationFailure.RollbackType = ModLoader.GetRegisteredType(InitialSource.ContextFilePath);
				compilationFailure.AppendErrors(AsyncLoader.Exceptions.Select(x => new ModLoader.Trace
				{
					Message = x.Error.ErrorText,
					Number = x.Error.ErrorNumber,
					Column = x.Error.Column,
					Line = x.Error.Line
				}));

#if DEBUG
				compilationFailure.AppendWarnings(AsyncLoader.Warnings.Select(x => new ModLoader.Trace
				{
					Message = x.Error.ErrorText,
					Number = x.Error.ErrorNumber,
					Column = x.Error.Column,
					Line = x.Error.Line
				}));
#endif

				// OnCompilationFail
				HookCaller.CallStaticHook(2719094727, InitialSource.ContextFilePath, compilationFailure);

				if (Community.Runtime.Config.Compiler.UnloadOnFailure)
				{
					var rollbackTypeName = compilationFailure.GetRollbackTypeName();

					if (!string.IsNullOrEmpty(rollbackTypeName))
					{
						var existentPlugin = ModLoader.FindPlugin(rollbackTypeName);

						if (existentPlugin != null)
						{
							ModLoader.UninitializePlugin(existentPlugin);
						}
					}
				}

#if DEBUG
				OsEx.File.Create(Path.Combine(Defines.GetScriptDebugFolder(), $"{AsyncLoader.InitialSource.ContextFileName}.Internal.cs"), AsyncLoader.InternalCallHookSource);
#endif
			}

			AsyncLoader.Exceptions?.Clear();
			AsyncLoader.Warnings?.Clear();
			AsyncLoader.Exceptions = AsyncLoader.Warnings = null;
			HasFinished = true;

			if (Community.AllProcessorsFinalized)
			{
				ModLoader.OnPluginProcessFinished();
			}
			yield break;
		}

		if (AsyncLoader == null)
		{
			yield break;
		}

		Logger.Debug($" Compiling '{(!string.IsNullOrEmpty(InitialSource.FilePath) ? Path.GetFileNameWithoutExtension(InitialSource.FilePath) : "<unknown>")}' took {AsyncLoader.CompileTime.TotalMilliseconds:0}ms [int. {AsyncLoader.InternalCallHookGenTime.TotalMilliseconds:0}ms]...", 1);

		var assembly = AsyncLoader.Assembly;
		var firstPlugin = true;

		yield return null;

		foreach (var type in assembly.GetTypes())
		{
			try
			{
				if (string.IsNullOrEmpty(type.Namespace) ||
					!(type.Namespace.Equals("Oxide.Plugins") || type.Namespace.Equals("Carbon.Plugins"))) continue;

				if (type.GetCustomAttribute(typeof(InfoAttribute), true) is not InfoAttribute info) continue;

				if (!IsExtension && firstPlugin && !BypassFileNameChecks)
				{
					var name = Path.GetFileNameWithoutExtension(InitialSource.FilePath).ToLower().Replace(" ", "").Replace(".", "").Replace("-", "");

					if (type.Name.ToLower().Replace(" ", "").Replace(".", "").Replace("-", "") != name)
					{
						Logger.Warn($"Plugin '{type.Name}' does not match with its file-name '{name}'.");
						break;
					}
				}

				firstPlugin = false;

				if (requires.Any(x => x.Name == info.Title)) continue;

				var description = type.GetCustomAttribute(typeof(DescriptionAttribute), true) as DescriptionAttribute;
				var plugin = Script.Create(assembly, type);

				plugin.Name = info.Title;
				plugin.Author = info.Author;
				plugin.Version = info.Version;
				plugin.Description = description?.Description;

				if (ModLoader.InitializePlugin(type, out RustPlugin rustPlugin, Mod, preInit: p =>
					{
						Scripts.Add(plugin);
						p.HasConditionals = Sources.Any(x => x.Content.Contains("#if "));
						p.IsExtension = IsExtension;
#if DEBUG
						p.CompileWarnings = AsyncLoader.Warnings.Select(x => new ModLoader.Trace
						{
							Message = x.Error.ErrorText,
							Number = x.Error.ErrorNumber,
							Column = x.Error.Column,
							Line = x.Error.Line
						}).ToArray();
#endif
						plugin.IsCore = IsCore;

						p.Hooks = AsyncLoader.Hooks[type];
						p.HookMethods = AsyncLoader.HookMethods[type];
						p.PluginReferences = AsyncLoader.PluginReferences[type];

						p.Requires = requiresResult;
						p.SetProcessor(Community.Runtime.ScriptProcessor, Process);
						p.CompileTime = AsyncLoader.CompileTime;
						p.InternalCallHookGenTime = AsyncLoader.InternalCallHookGenTime;
						p.InternalCallHookSource = AsyncLoader.InternalCallHookSource;

						p.FilePath = AsyncLoader.InitialSource.ContextFilePath;
						p.FileName = AsyncLoader.InitialSource.ContextFileName;
					}))
				{
					plugin.Instance = rustPlugin;

					var arg = Pool.Get<CarbonEventArgs>();
					arg.Init(rustPlugin);
					Community.Runtime.Events.Trigger(CarbonEvent.PluginPreload, arg);
					Pool.Free(ref arg);

					ModLoader.RegisterType(AsyncLoader.InitialSource.ContextFilePath, type);

					Plugin.InternalApplyAllPluginReferences();

					// OnPluginLoaded
					HookCaller.CallStaticHook(3051933177, rustPlugin);
				}
			}
			catch (Exception exception)
			{
				HasFinished = true;
				Logger.Error($"Failed to compile '{(!string.IsNullOrEmpty(InitialSource.ContextFilePath) ? Path.GetFileNameWithoutExtension(InitialSource.ContextFilePath) : "<unknown>")}': ", exception);
			}

			yield return null;
		}

		if (firstPlugin)
		{
			Logger.Error($"Invalid plugin format in '{AsyncLoader.InitialSource.ContextFileName}'. Namespace must be Carbon|Oxide.Plugins and inherited class must be Carbon|Rust|CovalencePlugin.");
		}

		AsyncLoader?.Dispose();

		HasFinished = true;

		if (Community.AllProcessorsFinalized)
		{
			ModLoader.OnPluginProcessFinished();
		}

		Facepunch.Pool.FreeUnmanaged(ref requires);
		yield return null;
	}

	public void Dispose()
	{
		Community.Runtime.ScriptProcessor.StopCoroutine(Compile());

		HasFinished = true;

		AsyncLoader?.Abort();
		AsyncLoader = null;

		if (Scripts != null)
		{
			foreach (var script in Scripts)
			{
				script.Dispose();
			}
		}

		if (Sources != null)
		{
			foreach (var source in Sources)
			{
				source.Dispose();
			}
		}

		Sources?.Clear();
		Scripts?.Clear();
		Sources = null;
		Scripts = null;
	}

	[Serializable]
	public class Script : IDisposable, IScript
	{
		public Assembly Assembly { get; set; }
		public Type Type { get; set; }

		public string Name { get; set; }
		public string Author { get; set; }
		public VersionNumber Version { get; set; }
		public string Description { get; set; }
		public IScriptLoader Loader { get; set; }
		public RustPlugin Instance { get; set; }
		public bool IsCore { get; set; }

		public static Script Create(Assembly assembly, Type type)
		{
			return new Script
			{
				Assembly = assembly,
				Type = type,

				Name = null,
				Author = null,
				Version = new VersionNumber(1, 0, 0),
				Description = null,
			};
		}

		public void Dispose()
		{
			Assembly = null;
			Type = null;

			Name = null;
			Author = null;
			Version = default;
			Description = null;
			Loader = null;
			Instance = null;
			IsCore = default;
		}

		public override string ToString()
		{
			return $"{Name} v{Version}";
		}
	}
}
