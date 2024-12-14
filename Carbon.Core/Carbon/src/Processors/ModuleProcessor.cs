using System;
using System.Collections.Generic;
using System.Linq;
using API.Events;
using Carbon.Base;
using Carbon.Base.Interfaces;
using Carbon.Contracts;
using Facepunch;

namespace Carbon.Managers;

public class ModuleProcessor : BaseProcessor, IModuleProcessor
{
	public override string Name => "Module Processor";
	public override bool EnableWatcher => false;

	List<BaseHookable> IModuleProcessor.Modules { get; } = new(200);

	internal List<BaseHookable> _modules;

	public void Init()
	{
		_modules = (this as IModuleProcessor).Modules;

		// TODO ---------------------------------------------------------------
		// This needs to go into the ModuleManager. Each module must implement
		// the ICarbonModule. Currently is just a workaround to be compatible
		// with the single modules file we ship. After migrating this the
		// IAddonCache.Types and ITypeManager.LoadedTypes should no longer be
		// required and will be removed.

		Community.Runtime.Events.Subscribe(API.Events.CarbonEvent.ModuleLoaded, e =>
		{
			if (e is ModuleEventArgs m)
			{
				var types = (m.Data as IReadOnlyList<Type>).ToArray();
				Build(m.Payload.ToString(), types);
				Array.Clear(types, 0, types.Length);
				types = null;
			}
		});
		Community.Runtime.Events.Subscribe(API.Events.CarbonEvent.ModuleUnloaded, e =>
		{
			if (e is ModuleEventArgs m)
			{
				var pool = Pool.Get<List<BaseHookable>>();
				pool.AddRange(_modules);

				foreach (var module in pool)
				{
					if (module is BaseModule baseModule)
					{
						if (baseModule.Context is string context1 && m.Payload is string context2 && context1.Equals(context2))
						{
							baseModule.Shutdown();
						}
					}
				}

				Pool.FreeUnmanaged(ref pool);
			}
		});

		Build(typeof(Community).Assembly.GetExportedTypes());

		foreach(var type in Community.Runtime.AssemblyEx.Modules.Shared)
		{
			Build(type.Value, type.Key);
		}
	}
	public void OnServerInit()
	{
		foreach (var hookable in _modules)
		{
			if (hookable is IModule module && module.IsEnabled())
			{
				try
				{
					module.OnServerInit(true);
				}
				catch (Exception ex)
				{
					Logger.Error($"[ModuleProcessor] Failed OnServerInit for '{hookable.Name}'", ex);
				}
			}
		}

		foreach (var hookable in _modules)
		{
			if (hookable is IModule module && module.IsEnabled())
			{
				try
				{
					module.OnPostServerInit(true);
				}
				catch (Exception ex)
				{
					Logger.Error($"[ModuleProcessor] Failed OnPostServerInit for '{hookable.Name}'", ex);
				}
			}
		}
	}
	public void OnServerSave()
	{
		foreach (var hookable in _modules)
		{
			if (hookable is not IModule module || !module.IsEnabled())
			{
				continue;
			}

			try
			{
				module.OnServerSaved();
			}
			catch (Exception ex)
			{
				Logger.Error($"[ModuleProcessor] Failed OnServerSave for '{hookable.Name}'", ex);
			}
		}
	}

	public void Setup(BaseHookable hookable)
	{
		_modules.Add(hookable);
	}
	public void Build(params Type[] types)
	{
		Build(null, types);
	}
	public void Build(string context, params Type[] types)
	{
		var cache = Pool.Get<List<BaseModule>>();

		foreach (var type in types)
		{
			if (type.IsAbstract || type.BaseType == null || !type.IsSubclassOf(typeof(BaseModule)))
			{
				continue;
			}

			var module = Activator.CreateInstance(type) as BaseModule;
			module.Context = context;
			cache.Add(module);
		}

		foreach (var hookable in cache)
		{
			if (hookable is IModule)
			{
				Setup(hookable);
			}
		}

		foreach (var hookable in cache)
		{
			if (hookable is not IModule module) continue;

			try
			{
				module.Init();
			}
			catch (Exception ex)
			{
				Logger.Error($"Failed module Init for {module?.GetType().FullName}", ex);
			}
		}

		foreach (var hookable in cache)
		{
			if (hookable is not IModule module) continue;

			try
			{
				module.Load();
			}
			catch (Exception ex)
			{
				Logger.Error($"Failed module Load for {module?.GetType().FullName}", ex);
			}
		}

		foreach (var hookable in cache)
		{
			if (hookable is not IModule module || !module.IsEnabled()) continue;

			try
			{
				module.InitEnd();
			}
			catch (Exception ex)
			{
				Logger.Error($"Failed module InitEnd for {module?.GetType().FullName}", ex);
			}
		}

		foreach (var hookable in cache)
		{
			if (hookable is not IModule module || !module.IsEnabled()) continue;

			try
			{
				module.OnEnableStatus();
			}
			catch (Exception ex)
			{
				Logger.Error($"Failed module OnEnableStatus for {module?.GetType().FullName}", ex);
			}
		}

		if (Community.IsServerInitialized)
		{
			foreach (var hookable in cache)
			{
				if (hookable is not IModule module || !module.IsEnabled()) continue;

				try
				{
					module.OnServerInit(true);
				}
				catch (Exception ex)
				{
					Logger.Error($"Failed module OnEnableStatus for {module?.GetType().FullName}", ex);
				}
			}

			foreach (var hookable in cache)
			{
				if (hookable is not IModule module || !module.IsEnabled()) continue;

				try
				{
					module.OnPostServerInit(true);
				}
				catch (Exception ex)
				{
					Logger.Error($"Failed module OnEnableStatus for {module?.GetType().FullName}", ex);
				}
			}
		}

		Pool.FreeUnmanaged(ref cache);
	}
	public void Uninstall(IModule module)
	{
		_modules.RemoveAll(x => x == module);
	}

	public void Save()
	{
		foreach (var hookable in _modules)
		{
			var module = hookable.To<IModule>();

			module.Save();
		}
	}
	public void Load()
	{
		foreach (var hookable in _modules)
		{
			var module = hookable.To<IModule>();

			try
			{
				module.Load();
			}
			catch (Exception ex)
			{
				Logger.Error($"Failed module Load for {module?.GetType().FullName}", ex);
			}
		}
	}

	public override void Dispose()
	{
		foreach (var hookable in _modules)
		{
			var module = hookable.To<IModule>();

			module.Dispose();
		}

		_modules.Clear();

		base.Dispose();
	}
}
