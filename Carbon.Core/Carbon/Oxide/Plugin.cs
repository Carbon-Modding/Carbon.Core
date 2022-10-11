﻿///
/// Copyright (c) 2022 Carbon Community 
/// All rights reserved
/// 

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Carbon;
using Carbon.Core;
using Carbon.Core.Processors;
using Facepunch;
using HarmonyLib;
using Newtonsoft.Json;
using Oxide.Core;

namespace Oxide.Plugins
{
	[JsonObject(MemberSerialization.OptIn)]
	public class Plugin : BaseHookable, IDisposable
	{
		public bool IsCorePlugin { get; set; }

		[JsonProperty]
		public string Title { get; set; } = "Rust";
		[JsonProperty]
		public string Description { get; set; }
		[JsonProperty]
		public string Author { get; set; }
		public int ResourceId { get; set; }
		public bool HasConfig { get; set; }
		public bool HasMessages { get; set; }

		[JsonProperty]
		public double CompileTime { get; internal set; }

		public string FilePath { get; set; }
		public string FileName { get; set; }

		public override void TrackStart()
		{
			if (IsCorePlugin) return;

			base.TrackStart();
		}
		public override void TrackEnd()
		{
			if (IsCorePlugin) return;

			base.TrackEnd();
		}

		public Plugin[] Requires { get; internal set; }

		internal CarbonLoader.CarbonMod _carbon;
		internal BaseProcessor _processor;
		internal BaseProcessor.Instance _processor_instance;

		public object Harmony;

		public static implicit operator bool(Plugin other)
		{
			return other != null;
		}

		internal void _unprocessHooks()
		{
			foreach (var hook in Hooks)
			{
				CarbonCore.Instance.HookProcessor.UnappendHook(hook);
			}
			Carbon.Logger.Debug(Name, $"Unprocessed hooks");
		}

		public virtual void IInit()
		{
			using (TimeMeasure.New($"Processing HookMethods on '{this}'"))
			{
				foreach (var attribute in HookMethods)
				{
					var method = attribute.Method;
					var name = (string.IsNullOrEmpty(attribute.Name) ? method.Name : attribute.Name) + method.GetParameters().Length;
					if (!HookMethodAttributeCache.TryGetValue(name, out var list))
					{
						HookMethodAttributeCache.Add(name, new List<MethodInfo>() { method });
					}
					else list.Add(method);
				}
			}
			Carbon.Logger.Debug(Name, "Installed hook method attributes");

			using (TimeMeasure.New($"Processing PluginReferences on '{this}'"))
			{
				InternalApplyPluginReferences();
			}
			Carbon.Logger.Debug(Name, "Assigned plugin references");

			using (TimeMeasure.New($"Processing Hooks on '{this}'"))
			{
				foreach (var hook in Hooks)
				{
					CarbonCore.Instance.HookProcessor.InstallHooks(hook);
					CarbonCore.Instance.HookProcessor.AppendHook(hook);
				}
			}
			Carbon.Logger.Debug(Name, "Processed hooks");

			CallHook("Init");
		}
		public virtual void Load()
		{
			using (TimeMeasure.New($"Load on '{this}'"))
			{
				IsLoaded = true;
				CallHook("OnLoaded");
				CallHook("Loaded");
			}

			using (TimeMeasure.New($"Load.PendingRequirees on '{this}'"))
			{
				var requirees = CarbonLoader.GetRequirees(this);

				if (requirees != null)
				{
					foreach (var requiree in requirees)
					{
						Logger.Warn($" [{Name}] Loading '{Path.GetFileNameWithoutExtension(requiree)}' to parent's request: '{ToString()}'");
						CarbonCore.Instance.ScriptProcessor.Prepare(requiree);
					}

					CarbonLoader.ClearPendingRequirees(this);
				}
			}
		}
		public virtual void IUnload()
		{
			using (TimeMeasure.New($"IUnload.UnprocessHooks on '{this}'"))
			{
				_unprocessHooks();
			}

			using (TimeMeasure.New($"IUnload.Disposal on '{this}'"))
			{
				IgnoredHooks.Clear();
				HookCache.Clear();
				Hooks.Clear();
				HookMethods.Clear();
				PluginReferences.Clear();
				HookMethodAttributeCache.Clear();

				IgnoredHooks = null;
				HookCache = null;
				Hooks = null;
				HookMethods = null;
				PluginReferences = null;
				HookMethodAttributeCache = null;
			}

			using (TimeMeasure.New($"IUnload.UnloadRequirees on '{this}'"))
			{
				var mods = Pool.GetList<CarbonLoader.CarbonMod>();
				mods.AddRange(CarbonLoader._loadedMods);
				var plugins = Pool.GetList<Plugin>();

				foreach (var mod in CarbonLoader._loadedMods)
				{
					plugins.Clear();
					plugins.AddRange(mod.Plugins);

					foreach (var plugin in plugins)
					{
						if (plugin.Requires != null && plugin.Requires.Contains(this))
						{
							switch (plugin._processor)
							{
								case ScriptProcessor script:
									Logger.Warn($" [{Name}] Unloading '{plugin.ToString()}' because parent '{ToString()}' has been unloaded.");
									CarbonLoader.AddPendingRequiree(this, plugin);
									plugin._processor.Get<ScriptProcessor.Script>(plugin.FileName).Dispose();
									break;
							}
						}
					}
				}

				Pool.FreeList(ref mods);
				Pool.FreeList(ref plugins);
			}
		}
		internal void InternalApplyPluginReferences()
		{
			foreach (var reference in PluginReferences)
			{
				var field = reference.Field;
				var attribute = field.GetCustomAttribute<PluginReferenceAttribute>();
				if (attribute == null) continue;

				var plugin = (Plugin)null;
				if (field.FieldType.Name != nameof(Plugin) && field.FieldType.Name != nameof(RustPlugin))
				{
					var info = field.FieldType.GetCustomAttribute<InfoAttribute>();
					if (info == null)
					{
						Carbon.Logger.Warn($"You're trying to reference a non-plugin instance: {field.Name}[{field.FieldType.Name}]");
						continue;
					}

					plugin = CarbonCore.Instance.CorePlugin.plugins.Find(info.Title);
				}
				else plugin = CarbonCore.Instance.CorePlugin.plugins.Find(field.Name);

				if (plugin != null) field.SetValue(this, plugin);
			}
		}

		public void PatchPlugin(Assembly assembly = null)
		{
			UnpatchPlugin();

			if (assembly == null) assembly = Assembly.GetExecutingAssembly();

			var patch = new HarmonyLib.Harmony(Name + "Patches");
			patch.PatchAll(assembly);
			Harmony = patch;
		}
		public void UnpatchPlugin()
		{
			if (Harmony is HarmonyLib.Harmony patch)
			{
				patch?.UnpatchAll(patch.Id);
				Harmony = null;
			}
		}

		public void SetProcessor(BaseProcessor processor)
		{
			_processor = processor;
		}

		#region Calls

		public T Call<T>(string hook)
		{
			return (T)HookExecutor.CallHook(this, hook);
		}
		public T Call<T>(string hook, object arg1)
		{
			return (T)HookExecutor.CallHook(this, hook, arg1);
		}
		public T Call<T>(string hook, object arg1, object arg2)
		{
			return (T)HookExecutor.CallHook(this, hook, arg1, arg2);
		}
		public T Call<T>(string hook, object arg1, object arg2, object arg3)
		{
			return (T)HookExecutor.CallHook(this, hook, arg1, arg2, arg3);
		}
		public T Call<T>(string hook, object arg1, object arg2, object arg3, object arg4)
		{
			return (T)HookExecutor.CallHook(this, hook, arg1, arg2, arg3, arg4);
		}
		public T Call<T>(string hook, object arg1, object arg2, object arg3, object arg4, object arg5)
		{
			return (T)HookExecutor.CallHook(this, hook, arg1, arg2, arg3, arg4, arg5);
		}
		public T Call<T>(string hook, object arg1, object arg2, object arg3, object arg4, object arg5, object arg6)
		{
			return (T)HookExecutor.CallHook(this, hook, arg1, arg2, arg3, arg4, arg5, arg6);
		}
		public T Call<T>(string hook, object arg1, object arg2, object arg3, object arg4, object arg5, object arg6, object arg7)
		{
			return (T)HookExecutor.CallHook(this, hook, arg1, arg2, arg3, arg4, arg5, arg6, arg7);
		}
		public T Call<T>(string hook, object arg1, object arg2, object arg3, object arg4, object arg5, object arg6, object arg7, object arg8)
		{
			return (T)HookExecutor.CallHook(this, hook, arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8);
		}
		public T Call<T>(string hook, object arg1, object arg2, object arg3, object arg4, object arg5, object arg6, object arg7, object arg8, object arg9)
		{
			return (T)HookExecutor.CallHook(this, hook, arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9);
		}

		public object Call(string hook)
		{
			return HookExecutor.CallHook(this, hook);
		}
		public object Call(string hook, object arg1)
		{
			return HookExecutor.CallHook(this, hook, arg1);
		}
		public object Call(string hook, object arg1, object arg2)
		{
			return HookExecutor.CallHook(this, hook, arg1, arg2);
		}
		public object Call(string hook, object arg1, object arg2, object arg3)
		{
			return HookExecutor.CallHook(this, hook, arg1, arg2, arg3);
		}
		public object Call(string hook, object arg1, object arg2, object arg3, object arg4)
		{
			return HookExecutor.CallHook(this, hook, arg1, arg2, arg3, arg4);
		}
		public object Call(string hook, object arg1, object arg2, object arg3, object arg4, object arg5)
		{
			return HookExecutor.CallHook(this, hook, arg1, arg2, arg3, arg4, arg5);
		}
		public object Call(string hook, object arg1, object arg2, object arg3, object arg4, object arg5, object arg6)
		{
			return HookExecutor.CallHook(this, hook, arg1, arg2, arg3, arg4, arg5, arg6);
		}
		public object Call(string hook, object arg1, object arg2, object arg3, object arg4, object arg5, object arg6, object arg7)
		{
			return HookExecutor.CallHook(this, hook, arg1, arg2, arg3, arg4, arg5, arg6, arg7);
		}
		public object Call(string hook, object arg1, object arg2, object arg3, object arg4, object arg5, object arg6, object arg7, object arg8)
		{
			return HookExecutor.CallHook(this, hook, arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8);
		}
		public object Call(string hook, object arg1, object arg2, object arg3, object arg4, object arg5, object arg6, object arg7, object arg8, object arg9)
		{
			return HookExecutor.CallHook(this, hook, arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9);
		}

		public T CallHook<T>(string hook)
		{
			return (T)HookExecutor.CallHook(this, hook);
		}
		public T CallHook<T>(string hook, object arg1)
		{
			return (T)HookExecutor.CallHook(this, hook, arg1);
		}
		public T CallHook<T>(string hook, object arg1, object arg2)
		{
			return (T)HookExecutor.CallHook(this, hook, arg1, arg2);
		}
		public T CallHook<T>(string hook, object arg1, object arg2, object arg3)
		{
			return (T)HookExecutor.CallHook(this, hook, arg1, arg2, arg3);
		}
		public T CallHook<T>(string hook, object arg1, object arg2, object arg3, object arg4)
		{
			return (T)HookExecutor.CallHook(this, hook, arg1, arg2, arg3, arg4);
		}
		public T CallHook<T>(string hook, object arg1, object arg2, object arg3, object arg4, object arg5)
		{
			return (T)HookExecutor.CallHook(this, hook, arg1, arg2, arg3, arg4, arg5);
		}
		public T CallHook<T>(string hook, object arg1, object arg2, object arg3, object arg4, object arg5, object arg6)
		{
			return (T)HookExecutor.CallHook(this, hook, arg1, arg2, arg3, arg4, arg5, arg6);
		}
		public T CallHook<T>(string hook, object arg1, object arg2, object arg3, object arg4, object arg5, object arg6, object arg7)
		{
			return (T)HookExecutor.CallHook(this, hook, arg1, arg2, arg3, arg4, arg5, arg6, arg7);
		}
		public T CallHook<T>(string hook, object arg1, object arg2, object arg3, object arg4, object arg5, object arg6, object arg7, object arg8)
		{
			return (T)HookExecutor.CallHook(this, hook, arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8);
		}
		public T CallHook<T>(string hook, object arg1, object arg2, object arg3, object arg4, object arg5, object arg6, object arg7, object arg8, object arg9)
		{
			return (T)HookExecutor.CallHook(this, hook, arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9);
		}

		public object CallHook(string hook)
		{
			return HookExecutor.CallHook(this, hook);
		}
		public object CallHook(string hook, object arg1)
		{
			return HookExecutor.CallHook(this, hook, arg1);
		}
		public object CallHook(string hook, object arg1, object arg2)
		{
			return HookExecutor.CallHook(this, hook, arg1, arg2);
		}
		public object CallHook(string hook, object arg1, object arg2, object arg3)
		{
			return HookExecutor.CallHook(this, hook, arg1, arg2, arg3);
		}
		public object CallHook(string hook, object arg1, object arg2, object arg3, object arg4)
		{
			return HookExecutor.CallHook(this, hook, arg1, arg2, arg3, arg4);
		}
		public object CallHook(string hook, object arg1, object arg2, object arg3, object arg4, object arg5)
		{
			return HookExecutor.CallHook(this, hook, arg1, arg2, arg3, arg4, arg5);
		}
		public object CallHook(string hook, object arg1, object arg2, object arg3, object arg4, object arg5, object arg6)
		{
			return HookExecutor.CallHook(this, hook, arg1, arg2, arg3, arg4, arg5, arg6);
		}
		public object CallHook(string hook, object arg1, object arg2, object arg3, object arg4, object arg5, object arg6, object arg7)
		{
			return HookExecutor.CallHook(this, hook, arg1, arg2, arg3, arg4, arg5, arg6, arg7);
		}
		public object CallHook(string hook, object arg1, object arg2, object arg3, object arg4, object arg5, object arg6, object arg7, object arg8)
		{
			return HookExecutor.CallHook(this, hook, arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8);
		}
		public object CallHook(string hook, object arg1, object arg2, object arg3, object arg4, object arg5, object arg6, object arg7, object arg8, object arg9)
		{
			return HookExecutor.CallHook(this, hook, arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9);
		}

		#endregion

		public void NextTick(Action callback)
		{
			CarbonCore.Instance.CarbonProcessor.OnFrameQueue.Enqueue(callback);
		}
		public void NextFrame(Action callback)
		{
			CarbonCore.Instance.CarbonProcessor.OnFrameQueue.Enqueue(callback);
		}

		public bool IsLoaded { get; set; }

		public new string ToString()
		{
			return GetType().Name;
		}
		public virtual void Dispose()
		{
			IsLoaded = false;
		}
	}
}
