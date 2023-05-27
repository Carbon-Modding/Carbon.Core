﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using API.Abstracts;
using API.Assembly;
using Loaders;

/*
 *
 * Copyright (c) 2022-2023 Carbon Community 
 * All rights reserved.
 *
 */

namespace Components;
#pragma warning disable IDE0051

internal abstract class AddonManager : CarbonBehaviour, IAddonManager
{
	internal class Item
	{
		public ICarbonAddon Addon { get; internal set; }
		public IReadOnlyList<Type> Types { get; internal set; }
		public string File { get; internal set; }
	}

	internal readonly AssemblyLoader _loader = new();

	internal IAssemblyManager AssemblyManager
	{ get => GetComponentInParent<IAssemblyManager>(); }

	internal List<Item> _loaded
	{ get; set; } = new();

	public IReadOnlyList<string> Loaded
	{ get => _loaded.Select(x => x.File).ToList(); }

	public IReadOnlyList<Type> LoadedTypes
	{ get => _loaded.SelectMany(x => x.Types).ToList(); }

	public byte[] Read(string file)
		=> _loader.ReadFromCache(file).Raw;

	public abstract Assembly Load(string file, string requester);

	internal virtual void Hydrate(Assembly assembly, ICarbonAddon addon)
	{
		// BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

		// foreach (Type type in assembly.GetTypes())
		// {
		// 	foreach (MethodInfo method in type.GetMethods(flags))
		// 	{
		// 		// Community.Runtime.HookManager.IsHookLoaded(method.Name)
		// 		// Community.Runtime.HookManager.Subscribe(method.Name, Name);
		// 	}
		// }
	}
}
