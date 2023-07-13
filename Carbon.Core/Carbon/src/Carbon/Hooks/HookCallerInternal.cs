﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Carbon.Base;
using Carbon.Components;
using Carbon.Core;
using Carbon.Extensions;
using Carbon.Pooling;
using Facepunch;
using Oxide.Core.Plugins;
using static Carbon.Base.BaseHookable;

/*
 *
 * Copyright (c) 2022-2023 Carbon Community 
 * All rights reserved.
 *
 */

namespace Carbon.Hooks;

public class HookCallerInternal : HookCallerCommon
{
	internal static List<Conflict> _conflictCache = new(10);

	public override void AppendHookTime(uint hook, int time)
	{
		if(!_hookTimeBuffer.ContainsKey(hook))
		{
			_hookTimeBuffer.Add(hook, time);
		}
		else
		{
			_hookTimeBuffer[hook] += time;
		}

		if (!_hookTotalTimeBuffer.ContainsKey(hook))
		{
			_hookTotalTimeBuffer.Add(hook, time);
		}
		else
		{
			_hookTotalTimeBuffer[hook] += time;
		}
	}
	public override void ClearHookTime(uint hook)
	{
		_hookTimeBuffer[hook] = 0;
	}

	public override object[] AllocateBuffer(int count)
	{
		if (!_argumentBuffer.TryGetValue(count, out var buffer))
		{
			_argumentBuffer.Add(count, buffer = new object[count]);
		}

		return buffer;
	}
	public override object[] RescaleBuffer(object[] oldBuffer, int newScale)
	{
		if (oldBuffer.Length == newScale)
		{
			return oldBuffer;
		}

		var newBuffer = AllocateBuffer(newScale);

		for (int i = 0; i < newScale; i++)
		{
			if (i > oldBuffer.Length - 1)
			{
				newBuffer[i] = null;
			}
			else
			{
				newBuffer[i] = oldBuffer[i];
			}
		}

		return newBuffer;
	}
	public override void ClearBuffer(object[] buffer)
	{
		for (int i = 0; i < buffer.Length; i++)
		{
			buffer[i] = null;
		}
	}

	internal static Conflict _defaultConflict = new()
	{
		Priority = Priorities.Low
	};
	internal static string _getPriorityName(Priorities priority)
	{
		switch (priority)
		{
			case Priorities.Low:
				return "lower";

			case Priorities.Normal:
				return "normal";

			case Priorities.High:
				return "higher";

			case Priorities.Highest:
				return "highest";
		}

		return "normal";
	}

	public override object CallHook<T>(T plugin, uint hookId, BindingFlags flags, object[] args, ref Priorities priority, bool keepArgs = false)
	{
		if (plugin.IsHookIgnored(hookId)) return null;

		var result = (object)null;

		if (plugin.InternalCallHookOverriden)
		{
			var processedId = hookId;

			if (args != null)
			{
				processedId += (uint)args.Length;
			}

			if (plugin.HookMethodAttributeCache.TryGetValue(processedId, out var hooks)) { }
			else if (!plugin.HookCache.TryGetValue(processedId, out hooks))
			{
				plugin.HookCache.Add(processedId, hooks = new());

				var methods = plugin.Type.GetMethods(flags);
				var readableHook = HookStringPool.GetOrAdd(hookId);

				for (int i = 0; i < methods.Length; i++)
				{
					var method = methods[i];
					if (method.Name != readableHook) continue;

					var methodPriority = method.GetCustomAttribute<HookPriority>();
					hooks.Add(CachedHook.Make(method, methodPriority == null ? Priorities.Normal : methodPriority.Priority, plugin));
				}
			}

			if (args != null && hooks.Count > 0)
			{
				var actualLength = hooks[0].Parameters.Length;

				if (actualLength != args.Length)
				{
					args = RescaleBuffer(args, actualLength);
				}
			}

#if DEBUG
			Profiler.StartHookCall(plugin, hookId);
#endif

			var beforeTicks = Environment.TickCount;
			plugin.TrackStart();

			result = plugin.InternalCallHook(hookId, args);

			plugin.TrackEnd();
			var afterTicks = Environment.TickCount;
			var totalTicks = afterTicks - beforeTicks;

#if DEBUG
			Profiler.EndHookCall(plugin);
#endif

			AppendHookTime(hookId, totalTicks);

			if (afterTicks > beforeTicks + 100 && afterTicks > beforeTicks)
			{
				if (plugin is Plugin basePlugin && !basePlugin.IsCorePlugin)
				{
					var readableHook = HookStringPool.GetOrAdd(hookId);
					Carbon.Logger.Warn($" {plugin.Name} hook '{readableHook}' took longer than 100ms [{totalTicks:0}ms]{(plugin.HasGCCollected ? " [GC]" : string.Empty)}");
				}
			}
		}
		else
		{
			priority = Priorities.Normal;

			var processedId = hookId;

			if (args != null)
			{
				processedId += (uint)args.Length;
			}

			if (plugin.HookMethodAttributeCache.TryGetValue(processedId, out var hooks)) { }
			else if (!plugin.HookCache.TryGetValue(processedId, out hooks))
			{
				plugin.HookCache.Add(processedId, hooks = new());

				var methods = plugin.Type.GetMethods(flags);
				var readableHook = HookStringPool.GetOrAdd(hookId);

				for (int i = 0; i < methods.Length; i++)
				{
					var method = methods[i];
					if (method.Name != readableHook) continue;

					var methodPriority = method.GetCustomAttribute<HookPriority>();
					hooks.Add(CachedHook.Make(method, methodPriority == null ? Priorities.Normal : methodPriority.Priority, plugin));
				}
			}

			for (int i = 0; i < hooks.Count; i++)
			{
				try
				{
					var cachedHook = hooks[i];

					if (cachedHook.IsByRef)
					{
						keepArgs = true;
					}

					var methodResult = DoCall(cachedHook);

					if (methodResult != null)
					{
						priority = cachedHook.Priority;
						result = methodResult;
					}

					ResultOverride(plugin, priority);
				}
				catch (Exception ex)
				{
					var exception = ex.InnerException ?? ex;
					var readableHook = HookStringPool.GetOrAdd(hookId);
					Carbon.Logger.Error(
						$"Failed to call hook '{readableHook}' on plugin '{plugin.Name} v{plugin.Version}'",
						exception
					);
				}
			}

			object DoCall(CachedHook hook)
			{
				if (hook.Delegate == null && !hook.IsByRef)
				{
					return null;
				}

				if (args != null)
				{
					var actualLength = hook.Parameters.Length;

					if (actualLength != args.Length)
					{
						args = RescaleBuffer(args, actualLength);
					}
				}

				if (args == null || SequenceEqual(hook.Parameters, args))
				{
#if DEBUG
					Profiler.StartHookCall(plugin, hookId);
#endif

					var beforeTicks = Environment.TickCount;
					plugin.TrackStart();
					var result2 = (object)default;

					if (hook.IsByRef) result2 = hook.Method.Invoke(plugin, args);
					else result2 = hook.Delegate.DynamicInvoke(args);

					plugin.TrackEnd();
					var afterTicks = Environment.TickCount;
					var totalTicks = afterTicks - beforeTicks;

					AppendHookTime(hookId, totalTicks);

					if (afterTicks > beforeTicks + 100 && afterTicks > beforeTicks)
					{
						if (plugin is Plugin basePlugin && !basePlugin.IsCorePlugin)
						{
							var readableHook = HookStringPool.GetOrAdd(hookId);
							Carbon.Logger.Warn($" {plugin.Name} hook '{readableHook}' took longer than 100ms [{totalTicks:0}ms]");
						}
					}

#if DEBUG
					Profiler.EndHookCall(plugin);
#endif
					return result2;
				}

				return null;
			}

			ConflictCheck();

			_conflictCache.Clear();

			void ResultOverride(BaseHookable hookable, Priorities priority)
			{
				_conflictCache.Add(Conflict.Make(hookable, hookId, result, priority));
			}
			void ConflictCheck()
			{
				var differentResults = false;

				if (_conflictCache.Count > 1)
				{
					var localResult = _conflictCache[0].Result;
					var priorityConflict = _defaultConflict;

					for (int i = 0; i < _conflictCache.Count; i++)
					{
						var conflict = _conflictCache[i];

						if (conflict.Result?.ToString() != localResult?.ToString())
						{
							differentResults = true;
						}

						if (conflict.Priority > priorityConflict.Priority)
						{
							priorityConflict = conflict;
						}
					}

					localResult = priorityConflict.Result;
					if (differentResults && !_conflictCache.All(x => x.Priority == priorityConflict.Priority) && Community.Runtime.Config.HigherPriorityHookWarns)
					{
						var readableHook = HookStringPool.GetOrAdd(hookId);
						Carbon.Logger.Warn($"Hook conflict while calling '{readableHook}', but used {priorityConflict.Hookable.Name} {priorityConflict.Hookable.Version} due to the {_getPriorityName(priorityConflict.Priority)} priority:\n  {_conflictCache.Select(x => $"{x.Hookable.Name} {x.Hookable.Version} [{x.Priority}:{x.Result}]").ToArray().ToString(", ", " and ")}");
					}
					if (localResult != null)
					{
						result = localResult;
					}
				}
			}
		}

		return result;
	}
	public override object CallDeprecatedHook<T>(T plugin, uint oldHook, uint newHook, DateTime expireDate, BindingFlags flags, object[] args, ref Priorities priority)
	{
		if (expireDate < DateTime.Now)
		{
			return null;
		}

		var now = DateTime.Now;

		if (!_lastDeprecatedWarningAt.TryGetValue(oldHook, out var lastWarningAt) || (now - lastWarningAt).TotalSeconds > 3600f)
		{
			_lastDeprecatedWarningAt[oldHook] = now;

			Carbon.Logger.Warn($"'{plugin.Name} v{plugin.Version}' is using deprecated hook '{oldHook}', which will stop working on {expireDate.ToString("D")}. Please ask the author to update to '{newHook}'");
		}

		return CallHook(plugin, newHook, flags, args, ref priority);
	}

	internal bool SequenceEqual(Type[] source, object[] target)
	{
		var equal = true;

		for(int i = 0; i < source.Length; i++)
		{
			var sourceItem = source[i];
			var targetItem = target[i]?.GetType();

			if (targetItem != null && !sourceItem.IsByRef && !targetItem.IsByRef &&
				sourceItem != targetItem &&
				!sourceItem.IsAssignableFrom(targetItem))
			{
				equal = false;
				break;
			}
		}

		return equal;
	}
}
