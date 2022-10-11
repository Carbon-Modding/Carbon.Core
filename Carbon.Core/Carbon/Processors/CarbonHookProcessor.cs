﻿///
/// Copyright (c) 2022 Carbon Community 
/// All rights reserved
/// 

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Facepunch;

namespace Carbon.Core
{
	public class CarbonHookProcessor
	{
		public Dictionary<string, HookInstance> Patches { get; } = new Dictionary<string, HookInstance>();

		public bool DoesHookExist(string hookName)
		{
			using (TimeMeasure.New($"DoesHookExist: {hookName}"))
			{
				foreach (var hook in CarbonDefines.Hooks)
				{
					if (hook.Name == hookName) return true;
				}
			}

			return false;
		}
		public bool HasHook(Type type, string hookName)
		{
			foreach (var method in type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
			{
				if (method.Name == hookName) return true;
			}

			return false;
		}
		public bool IsPatched(string hookName)
		{
			return Patches.ContainsKey(hookName);
		}
		public HookInstance GetInstance(string hookName)
		{
			if (!Patches.TryGetValue(hookName, out var instance))
			{
				return null;
			}

			return instance;
		}

		public void AppendHook(string hookName)
		{
			if (!DoesHookExist(hookName)) return;

			if (Patches.TryGetValue(hookName, out var instance))
			{
				instance.Hooks++;
			}
		}
		public void UnappendHook(string hookName)
		{
			if (!DoesHookExist(hookName)) return;

			if (Patches.TryGetValue(hookName, out var instance))
			{
				instance.Hooks--;

				if (instance.Hooks <= 0)
				{
					if (UninstallHooks(hookName))
					{
						Carbon.Logger.Warn($" No plugin is using '{hookName}'. Unpatched.");
					}
				}
			}
		}

		public void InstallHooks(string hookName, bool doRequires = true, bool onlyAlwaysPatchedHooks = false)
		{
			if (!DoesHookExist(hookName)) return;
			if (!IsPatched(hookName))
				Carbon.Logger.Debug($"Found '{hookName}'...");

			new HookInstallerThread
			{
				HookName = hookName,
				DoRequires = doRequires,
				Processor = this,
				OnlyAlwaysPatchedHooks = onlyAlwaysPatchedHooks
			}.Start();
		}
		public bool UninstallHooks(string hookName, bool shutdown = false)
		{
			try
			{
				using (TimeMeasure.New($"UninstallHooks: {hookName}"))
				{
					if (Patches.TryGetValue(hookName, out var instance))
					{
						if (!shutdown && instance.AlwaysPatched) return false;

						if (instance.Patches != null)
						{
							var list = Pool.GetList<HarmonyLib.Harmony>();
							list.AddRange(instance.Patches);

							foreach (var patch in list)
							{
								if (string.IsNullOrEmpty(patch.Id)) continue;

								patch.UnpatchAll(patch.Id);
							}

							instance.Patches.Clear();
							Pool.FreeList(ref list);
							return true;
						}
					}
				}
			}
			catch (Exception ex)
			{
				Logger.Error($"Failed hook '{hookName}' uninstallation.", ex);
			}

			return false;
		}

		public void InstallAlwaysPatchedHooks()
		{
			foreach (var type in CarbonDefines.Carbon.GetTypes())
			{
				var hook = type.GetCustomAttribute<Hook>();
				if (hook == null || type.GetCustomAttribute<Hook.AlwaysPatched>() == null) continue;

				InstallHooks(hook.Name, true, true);
			}
		}

		internal Type[] GetMatchedParameters(Type type, string methodName, ParameterInfo[] parameters)
		{
			var list = new List<Type>();

			foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Static))
			{
				if (method.Name != methodName) continue;

				var @params = method.GetParameters();

				for (int i = 0; i < @params.Length; i++)
				{
					try
					{
						var param = @params[i];
						var otherParam = parameters[i];

						if (otherParam.Name.StartsWith("__")) continue;

						if (param.ParameterType.FullName.Replace("&", "") == otherParam.ParameterType.FullName.Replace("&", ""))
						{
							list.Add(param.ParameterType);
						}
					}
					catch { }
				}

				if (list.Count > 0) break;
			}

			var result = list.ToArray();
			list.Clear();
			list = null;
			return result;
		}

		public class HookInstance
		{
			public string Id { get; set; }
			public int Hooks { get; set; } = 1;
			public bool AlwaysPatched { get; set; } = false;
			public List<HarmonyLib.Harmony> Patches { get; internal set; } = new List<HarmonyLib.Harmony>();
		}

		public class HookInstallerThread : ThreadedJob
		{
			public string HookName;
			public bool DoRequires = true;
			public bool OnlyAlwaysPatchedHooks = false;
			public CarbonHookProcessor Processor;

			public override void ThreadFunction()
			{
				foreach (var type in CarbonDefines.Carbon.GetTypes())
				{
					try
					{
						var parameters = type.GetCustomAttributes<Hook.Parameter>();
						var hook = type.GetCustomAttribute<Hook>();
						var args = $"[{type.Name}]_";

						if (parameters != null)
						{
							foreach (var parameter in parameters)
							{
								args += $"_[{parameter.Type.Name}]{parameter.Name}";
							}
						}

						if (hook == null) continue;

						if (hook.Name == HookName)
						{
							var patchId = $"{hook.Name}{args}";
							var patch = type.GetCustomAttribute<Hook.Patch>();
							var hookInstance = (HookInstance)null;

							if (!Processor.Patches.TryGetValue(HookName, out hookInstance))
							{
								Processor.Patches.Add(HookName, hookInstance = new HookInstance
								{
									AlwaysPatched = type.GetCustomAttribute<Hook.AlwaysPatched>() != null
								});
							}

							if (hookInstance.AlwaysPatched && !OnlyAlwaysPatchedHooks) continue;

							if (hookInstance.Patches.Any(x => x != null && x.Id == patchId)) continue;

							if (DoRequires)
							{
								var requires = type.GetCustomAttributes<Hook.Require>();

								if (requires != null)
								{
									foreach (var require in requires)
									{
										if (require.Hook == HookName) continue;

										Processor.InstallHooks(require.Hook, false);
									}
								}
							}

							var originalParameters = new List<Type>();
							var prefix = type.GetMethod("Prefix");
							var postfix = type.GetMethod("Postfix");
							var transplier = type.GetMethod("Transplier");

							foreach (var param in (prefix ?? postfix ?? transplier).GetParameters())
							{
								originalParameters.Add(param.ParameterType);
							}
							var originalParametersResult = originalParameters.ToArray();

							var matchedParameters = patch.UseProvidedParameters ? originalParametersResult : Processor.GetMatchedParameters(patch.Type, patch.Method, (prefix ?? postfix ?? transplier).GetParameters());

							var instance = new HarmonyLib.Harmony(patchId);
							var originalMethod = patch.Type.GetMethod(patch.Method, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static, null, matchedParameters, default);

							instance.Patch(originalMethod,
								prefix: prefix == null ? null : new HarmonyLib.HarmonyMethod(prefix),
								postfix: postfix == null ? null : new HarmonyLib.HarmonyMethod(postfix),
								transpiler: transplier == null ? null : new HarmonyLib.HarmonyMethod(transplier));
							hookInstance.Patches.Add(instance);
							hookInstance.Id = patchId;

							if (CarbonCore.Instance.Config.LogVerbosity > 2) Console.WriteLine($" -> Patched '{hook.Name}' <- {patchId}");

							Pool.Free(ref matchedParameters);
							Pool.Free(ref originalParametersResult);
							originalParameters.Clear();
							originalParameters = null;
						}
					}
					catch (Exception exception)
					{
						Console.WriteLine($" Couldn't patch hook '{HookName}' ({type.FullName})\n{exception}");
					}
				}
			}
		}
	}
}
