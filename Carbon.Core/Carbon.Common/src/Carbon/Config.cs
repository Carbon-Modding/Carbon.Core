﻿using System;
using System.Collections.Generic;
using API.Logger;
using Oxide.Core.Libraries;

/*
 *
 * Copyright (c) 2022-2023 Carbon Community 
 * All rights reserved.
 *
 */

namespace Carbon.Core;

[Serializable]
public class Config
{
	public bool AutoUpdate { get; set; } = true;
	public bool HarmonyReference { get; set; } = false;
	public bool ScriptWatchers { get; set; } = true;
	public System.IO.SearchOption ScriptWatcherOption { get; set; } = System.IO.SearchOption.TopDirectoryOnly;
	public bool HookValidation { get; set; } = true;
	public bool FileNameCheck { get; set; } = true;
	public bool IsModded { get; set; } = true;
	public bool HigherPriorityHookWarns { get; set; } = false;
	public int EntityMapBufferSize { get; set; } = 100000;
	public string PlayerDefaultGroup { get; set; } = "default";
	public string AdminDefaultGroup { get; set; } = "admin";
	public int LogFileMode { get; set; } = 2;
	public int LogVerbosity { get; set; } = 0;
	public bool UnityStacktrace { get; set; } =
#if DEBUG
		true;
#else
		false;
#endif
	public List<string> ConditionalCompilationSymbols { get; set; }
	public Severity LogSeverity { get; set; } = Severity.Notice;
	public Permission.SerializationMode PermissionSerialization { get; set; } = Permission.SerializationMode.Protobuf;
	public string Language { get; set; } = "en";
	public string WebRequestIp { get; set; }
	public bool oCommandChecks { get; set; } = true;

#if WIN
	public bool ShowConsoleInfo { get; set; } = true;
#endif
}
