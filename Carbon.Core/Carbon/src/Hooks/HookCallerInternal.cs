using System;
using System.Collections.Generic;
using System.Reflection;
using Carbon.Components;
using Carbon.Pooling;
using Facepunch;
using Oxide.Core.Plugins;
using UnityEngine;
using static Carbon.Base.BaseHookable;

namespace Carbon.Hooks;

public class HookCallerInternal : HookCallerCommon
{
	public override object[] AllocateBuffer(int count)
	{
		if (_argumentBuffer.TryGetValue(count, out var pool))
		{
			return pool.Rent();
		}

		_argumentBuffer[count] = pool = new HookArgPool(count, 15);
		return pool.Rent();
	}
	public override object[] RescaleBuffer(object[] oldBuffer, int newScale, CachedHook hook)
	{
		if (oldBuffer.Length == newScale)
		{
			return oldBuffer;
		}

		var newBuffer = AllocateBuffer(newScale);

		for (int i = 0; i < newScale; i++)
		{
			var value = newBuffer[i];

			if (i > oldBuffer.Length - 1)
			{
				newBuffer[i] = value = null;
			}
			else
			{
				newBuffer[i] = value = oldBuffer[i];
			}

			if (i <= hook.InfoParameters.Length - 1 && value == null)
			{
				var parameter = hook.InfoParameters[i];

				if (parameter.HasDefaultValue)
				{
					newBuffer[i] = parameter.DefaultValue;
				}
			}
		}

		return newBuffer;
	}
	public override void ProcessDefaults(object[] buffer, CachedHook hook)
	{
		var length = buffer.Length;

		for (int i = 0; i < length; i++)
		{
			if (i <= hook.InfoParameters.Length - 1 && buffer[i] == null)
			{
				if (hook.InfoParameters[i] is ParameterInfo info && info.HasDefaultValue)
				{
					buffer[i] = info.DefaultValue;
				}
				else
				{
					buffer[i] = null;
				}
			}
		}
	}
	public override void ReturnBuffer(object[] buffer)
	{
		if (!_argumentBuffer.TryGetValue(buffer.Length, out var pool))
		{
			_argumentBuffer[buffer.Length] = pool = new HookArgPool(buffer.Length, 15);
		}

		pool.Return(buffer);
	}

	public override object CallHook<T>(T hookable, uint hookId, BindingFlags flags, object[] args)
	{
		if (hookable.IsHookIgnored(hookId))
		{
			return null;
		}

		hookable.BuildHookCache(flags);

		CachedHookInstance hookInstance = default;

		if (hookable.HookPool != null && !hookable.HookPool.TryGetValue(hookId, out hookInstance))
		{
			return null;
		}

		var result = (object)null;
		var conflicts = Pool.Get<List<Conflict>>();
		var hasRescaledBuffer = false;

		if (hookable.InternalCallHookOverriden)
		{
			var hook = (CachedHook)default;

			if (hookInstance != null && hookInstance.IsValid())
			{
				hook = hookInstance.PrimaryHook;
			}

			hookable.TrackStart();
			var beforeMemory = CurrentMemory;

			if (hook != null && args != null)
			{
				var actualLength = hook.Parameters.Length;

				if (actualLength != args.Length)
				{
					args = HookCaller.Caller.RescaleBuffer(args, actualLength, hook);
					hasRescaledBuffer = true;
				}
				else
				{
					HookCaller.Caller.ProcessDefaults(args, hook);
				}
			}

			if (hook != null && hook.IsAsync)
			{
				hookable.InternalCallHook(hookId, args);
				hookable.TrackEnd();
			}
			else
			{
				var currentResult = hookable.InternalCallHook(hookId, args);
				hookable.TrackEnd();

				if (currentResult != null)
				{
					HookCaller.ResultOverride(conflicts, hookable, hookId, result = currentResult);
				}
			}

			var afterHookTime = hookable.CurrentHookTime;
			var afterMemory = CurrentMemory;
			var totalMemory = Mathf.Abs(afterMemory - beforeMemory);

			hook?.OnFired(hookable, afterHookTime, totalMemory);

			var afterHookTimeMs = afterHookTime.TotalMilliseconds;

			if (afterHookTimeMs > 100 && hookable is Plugin basePlugin && !basePlugin.IsCorePlugin)
			{
				var readableHook = HookStringPool.GetOrAdd(hookId);

				Logger.Warn($" {hookable.ToPrettyString()} hook '{readableHook}' took longer than 100ms [{afterHookTimeMs:0}ms]{(hookable.HasGCCollected ? " [GC]" : string.Empty)}");

				var wasLagSpike = afterHookTimeMs >= Community.Runtime.Config.Debugging.HookLagSpikeThreshold;

				if (wasLagSpike)
				{
					hook?.OnLagSpike(hookable);
				}

				Analytics.plugin_time_warn(readableHook, basePlugin, afterHookTimeMs, totalMemory, wasLagSpike, hook, hookable);
			}

			HookCaller.ConflictCheck(conflicts, ref result, hookId);
			FrameDispose(hasRescaledBuffer, args, ref conflicts);
		}
		else
		{
			if (hookInstance != null && hookInstance.IsValid())
			{
				foreach (var cachedHook in hookInstance.Hooks)
				{
					try
					{
						if (cachedHook.IsAsync)
						{
							DoCall(hookable, hookId, cachedHook, args, ref hasRescaledBuffer);
						}
						else
						{
							var currentResult = DoCall(hookable, hookId, cachedHook, args, ref hasRescaledBuffer);

							if (currentResult != null)
							{
								HookCaller.ResultOverride(conflicts, hookable, hookId, result = currentResult);
							}
						}
					}
					catch (Exception ex)
					{
						var exception = ex.InnerException ?? ex;
						var readableHook = HookStringPool.GetOrAdd(hookId);
						Carbon.Logger.Error(
							$"Failed to call hook '{readableHook}' on plugin '{hookable.Name} v{hookable.Version}'",
							exception
						);
					}
				}
			}

			HookCaller.ConflictCheck(conflicts, ref result, hookId);
			FrameDispose(false, args, ref conflicts);

			static object DoCall(T hookable, uint hookId, CachedHook hook, object[] args, ref bool hasRescaledBuffer)
			{
				if (args != null)
				{
					var actualLength = hook.Parameters.Length;

					if (actualLength != args.Length)
					{
						args = HookCaller.Caller.RescaleBuffer(args, actualLength, hook);
						hasRescaledBuffer = true;
					}
					else
					{
						HookCaller.Caller.ProcessDefaults(args, hook);
					}
				}

				if (args != null && !SequenceEqual(hook.Parameters, args)) return null;
				var result2 = (object)default;
				hookable.TrackStart();
				var beforeMemory = hookable.TotalMemoryUsed;

				try
				{
					result2 = hook.Method.Invoke(hookable, args);
				}
				catch (Exception ex)
				{
					var exception = ex.InnerException ?? ex;
					var readableHook = HookStringPool.GetOrAdd(hookId);
					Carbon.Logger.Error(
						$"Failed to call hook '{readableHook}' on plugin '{hookable.Name} v{hookable.Version}'",
						exception
					);
				}

				hookable.TrackEnd();
				var afterHookTime = hookable.CurrentHookTime;
				var afterMemory = hookable.TotalMemoryUsed;
				var totalMemory = afterMemory - beforeMemory;

				hook?.OnFired(hookable, afterHookTime, totalMemory);

				var afterHookTimeMs = afterHookTime.TotalMilliseconds;

				if (afterHookTimeMs > 100)
				{
					if (hookable is Plugin basePlugin && !basePlugin.IsCorePlugin)
					{
						var readableHook = HookStringPool.GetOrAdd(hookId);
						Carbon.Logger.Warn($" {hookable.ToPrettyString()} hook '{readableHook}' took longer than 100ms [{afterHookTimeMs:0}ms]{(hookable.HasGCCollected ? " [GC]" : string.Empty)}");

						var wasLagSpike = afterHookTimeMs >= Community.Runtime.Config.Debugging.HookLagSpikeThreshold;

						if (wasLagSpike)
						{
							hook.OnLagSpike(hookable);
						}

						Analytics.plugin_time_warn(readableHook, basePlugin, afterHookTimeMs, totalMemory, wasLagSpike, hook, hookable);
					}
				}

				if (hasRescaledBuffer)
				{
					HookCaller.Caller.ReturnBuffer(args);
				}

				return result2;
			}
		}

		static void FrameDispose(bool hasRescaledBuffer, object[] buffer, ref List<Conflict> conflicts)
		{
			if (hasRescaledBuffer)
			{
				HookCaller.Caller.ReturnBuffer(buffer);
			}

			Pool.FreeUnmanaged(ref conflicts);
		}

		return result;
	}
	public override object CallDeprecatedHook<T>(T plugin, uint oldHook, uint newHook, DateTime expireDate, BindingFlags flags, object[] args)
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

		return CallHook(plugin, newHook, flags, args);
	}

	internal static bool SequenceEqual(Type[] source, object[] target)
	{
		for(int i = 0; i < source.Length; i++)
		{
			var sourceItem = source[i];
			var targetItem = target[i]?.GetType();

			if (targetItem != null && !sourceItem.IsByRef && !targetItem.IsByRef &&
				sourceItem != targetItem &&
				!sourceItem.IsAssignableFrom(targetItem))
			{
				return false;
			}
		}

		return true;
	}
}
