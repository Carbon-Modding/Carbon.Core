using System;
using System.Collections.Generic;
using System.IO;
using API.Events;
using Carbon.Components;
using Carbon.Core;
using Carbon.Extensions;
using Carbon.Hooks;
using Carbon.Managers;
using Oxide.Core;
using System.Linq;
using API.Commands;
using UnityEngine;
using Carbon.Client;
using Carbon.Jobs;

namespace Carbon;

#if !(WIN || UNIX)
#error Target architecture not defined
#endif

public class CommunityInternal : Community
{
	public static CommunityInternal InternalRuntime
	{
		get
		{
			return Runtime as CommunityInternal;
		}
		set
		{
			Runtime = value;
		}
	}

	public bool IsInitialized
	{
		get; set;
	}

	public override void ReloadPlugins(IEnumerable<string> except = null)
	{
		base.ReloadPlugins(except);

		ScriptLoader.LoadAll(except);
	}

	internal void _installCore()
	{
		Runtime.Core = Core = new CorePlugin();
		Core.Setup("Core", "Carbon Community", new VersionNumber(1, 0, 0), string.Empty);
		ModLoader.ProcessPrecompiledType(Core);
		Core.IsCorePlugin = Core.IsPrecompiled = true;
		Core.IInit();
		Core.ILoadDefaultMessages();

		ModLoader.RegisterPackage(Core.Package = ModLoader.Package.Get("Carbon Community", true).AddPlugin(Core));
		ModLoader.RegisterPackage(Plugins = ModLoader.Package.Get("Scripts", false));
		ModLoader.RegisterPackage(ZipPlugins = ModLoader.Package.Get("Zip Scripts", false));

		ModLoader.ProcessCommands(typeof(CorePlugin), Core, prefix: "c");
		ModLoader.ProcessCommands(typeof(CorePlugin), Core, prefix: "carbon", hidden: true);

		var commandCount = CommandManager.Chat.Count(x => x.Reference == Core && !x.HasFlag(CommandFlags.Hidden)) +
		                   CommandManager.ClientConsole.Count(x => x.Reference == Core && !x.HasFlag(CommandFlags.Hidden));

		Logger.Log($"Initialized Carbon Core plugin ({Core.Hooks.Count:n0} {Core.Hooks.Count.Plural("hook", "hooks")}, {commandCount:n0} {commandCount.Plural("command", "commands")})");

#if !MINIMAL
		CarbonAuto.Init();
		API.Abstracts.CarbonAuto.Singleton.Load();
#endif
	}
	internal void _installProcessors()
	{
		Carbon.Logger.Log("Installed processors");
		{
			_uninstallProcessors();

			var gameObject = new GameObject("Processors");
			ScriptProcessor = gameObject.AddComponent<ScriptProcessor>();
			ZipScriptProcessor = gameObject.AddComponent<ZipScriptProcessor>();
#if DEBUG
			ZipDevScriptProcessor = gameObject.AddComponent<ZipDevScriptProcessor>();
#endif
			CarbonProcessor = gameObject.AddComponent<CarbonProcessor>();
			HookManager = gameObject.AddComponent<PatchManager>();
			ModuleProcessor = gameObject.AddComponent<ModuleProcessor>();
			Entities = new Entities();
			CarbonClient = new CarbonClientManager();
		}

		_registerProcessors();
		ScriptCompilationThread._injectPatchedReferences();
	}
	internal void _installTest()
	{
		Carbon.Test.Integrations.Logger = new Logger();
		Carbon.Logger.Log($"Initialized Carbon.Test backend");
	}
	internal void _registerProcessors()
	{
		if (ScriptProcessor != null) ScriptProcessor?.Start();
		if (ZipScriptProcessor != null) ZipScriptProcessor?.Start();
#if DEBUG
		if (ZipDevScriptProcessor != null) ZipDevScriptProcessor?.Start();
#endif

		if (ScriptProcessor != null) ScriptProcessor.InvokeRepeating(() => { RefreshConsoleInfo(); }, 1f, 1f);
		Carbon.Logger.Log("Registered processors");
	}
	internal void _uninstallProcessors()
	{
		var obj = ScriptProcessor == null ? null : ScriptProcessor.gameObject;

		try
		{
			if (ScriptProcessor != null) ScriptProcessor?.Dispose();
			if (ZipScriptProcessor != null) ZipScriptProcessor?.Dispose();
#if DEBUG
			if (ZipDevScriptProcessor != null) ZipDevScriptProcessor?.Dispose();
#endif
			if (ModuleProcessor != null) ModuleProcessor?.Dispose();
			if (CarbonProcessor != null) CarbonProcessor?.Dispose();
		}
		catch { }

		try
		{
			if (obj != null) UnityEngine.Object.Destroy(obj);
		}
		catch { }
	}
	internal void _handleThreads()
	{
		ThreadEx.MainThread.Name = "Main";
	}

	public override void Initialize()
	{
		base.Initialize();

		if (IsInitialized)
		{
			return;
		}

		HookCaller.Caller = new HookCallerInternal();

		LoadConfig();
		LoadMonoProfilerConfig();
		LoadClientConfig();

		Compat.Init();

		Events.Trigger(CarbonEvent.CarbonStartup, EventArgs.Empty);

		Carbon.Logger.InitTaskExceptions();
		Carbon.Logger.Log("Loaded config");

		Defines.Initialize();

		_handleThreads();
		_installProcessors();
		_installTest();

		Events.Subscribe(CarbonEvent.HooksInstalled, args =>
		{
			ClearCommands();
			_installCore();
			ModuleProcessor.Init();
			CarbonClient.Init();

			Events.Trigger(CarbonEvent.HookValidatorRefreshed, EventArgs.Empty);
		});

		Events.Subscribe(CarbonEvent.HookValidatorRefreshed, args =>
		{
			CommandLine.ExecuteCommands("+carbon.onboot", "Carbon boot");

			var serverConfigPath = Path.Combine(ConVar.Server.GetServerFolder("cfg"), "server.cfg");
			var lines = OsEx.File.Exists(serverConfigPath) ? OsEx.File.ReadTextLines(serverConfigPath) : null;

			if (lines != null)
			{
				CommandLine.ExecuteCommands("+carbon.onboot", "cfg/server.cfg", lines);
				CommandLine.ExecuteCommands(lines);
				Array.Clear(lines, 0, lines.Length);
			}

			ReloadPlugins();
		});

		Logger.Log($"  Carbon {Analytics.Version} [{Analytics.Protocol}] {Build.Git.HashShort} on {Analytics.Platform.ToCamelCase()}");
		Logger.Log($"         {Build.Git.Author} on {Build.Git.Branch} ({Build.Git.Date})");
		Logger.Log($"  Rust   {Facepunch.BuildInfo.Current.Build.Number}/{Rust.Protocol.printable} on {Facepunch.BuildInfo.Current.Scm.Branch} ({Facepunch.BuildInfo.Current.Scm.Date})");

		Interface.Initialize();

		RefreshConsoleInfo();

		IsInitialized = true;

		Logger.Log($"Loaded.");
		Events.Trigger(CarbonEvent.CarbonStartupComplete, EventArgs.Empty);

		Client.Client.Init();

		Entities.Init();
	}
	public override void Uninitialize()
	{
		try
		{
			Events.Trigger(CarbonEvent.CarbonShutdown, EventArgs.Empty);

			_uninstallProcessors();
			ClearCommands(all: true);
			ClearPlugins(all: true);
			ModLoader.Packages.Clear();
			UnityEngine.Debug.Log($"Unloaded Carbon.");

#if WIN
			try
			{
				if (IsConfigReady && Config.Misc.ShowConsoleInfo && ServerConsole.Instance != null && ServerConsole.Instance.input != null)
				{
					ServerConsole.Instance.input.statusText = new string[3];
				}
			}
			catch { }
#endif

			Entities.Dispose();

			Carbon.Logger.Dispose();

			Events.Trigger(CarbonEvent.CarbonShutdownComplete, EventArgs.Empty);
		}
		catch (Exception ex)
		{
			Carbon.Logger.Error($"Failed Carbon uninitialization.", ex);
			Events.Trigger(CarbonEvent.CarbonShutdownFailed, EventArgs.Empty);
		}

		InternalRuntime = null;
		base.Uninitialize();
	}
}
