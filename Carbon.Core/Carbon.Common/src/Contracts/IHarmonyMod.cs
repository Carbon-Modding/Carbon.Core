using System;

/*
 *
 * Copyright (c) 2022-2023 Carbon Community 
 * All rights reserved.
 *
 */

namespace API.Contracts;

public interface IHarmonyMod
{
	public void OnLoaded(EventArgs args);
	public void OnUnloaded(EventArgs args);
}