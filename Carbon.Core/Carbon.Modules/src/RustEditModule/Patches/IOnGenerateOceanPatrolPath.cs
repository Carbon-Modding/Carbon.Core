﻿using System.Collections.Generic;
using API.Hooks;
using UnityEngine;

/*
 *
 * Copyright (c) 2022-2023 Carbon Community 
 * All rights reserved.
 *
 */

namespace Carbon.Modules;

public partial class RustEditModule
{
	[HookAttribute.Patch("IOnGenerateOceanPatrolPath", "IOnGenerateOceanPatrolPath", typeof(BaseBoat), "GenerateOceanPatrolPath", new System.Type[] { typeof(float), typeof(float) })]
	[HookAttribute.Identifier("52473d7cd2ad4ab796de4b52d60bb7f3")]
	[HookAttribute.Options(HookFlags.Hidden)]

	public class World_BaseBoat_GenerateOceanPatrolPath2_52473d7cd2ad4ab796de4b52d60bb7f3 : API.Hooks.Patch
	{
		public static void Postfix(ref List<Vector3> __result)
		{
			var hook = HookCaller.CallStaticHook(2477149531, __result);

			if (hook is List<Vector3> result)
			{
				__result = result;
				return;
			}

			HookCaller.CallStaticHook(2026974294, __result);
		}
	}
}
