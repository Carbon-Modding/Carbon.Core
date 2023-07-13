﻿using System.Diagnostics;
using API.Logger;
using ILogger = API.Logger.ILogger;

/*
 *
 * Copyright (c) 2022-2023 Carbon Community 
 * All rights reserved.
 *
 */

namespace Carbon;

public sealed class Logger : ILogger
{
	public static FileLogger CoreLog { get; set; } = new FileLogger("Carbon.Core");

	public static Action<string, Exception, int> OnErrorCallback { get; set; }
	public static Action<string, int> OnWarningCallback { get; set; }
	public static Action<string, int> OnNoticeCallback { get; set; }
	public static Action<string, int> OnDebugCallback { get; set; }

	internal static string GetDate()
	{
		return DateTime.Now.ToString("yyyy.MM.dd HH:mm:ss");
	}
	internal static void Write(Severity severity, object message, Exception ex = null, int verbosity = 1, bool nativeLog = true)
	{
		CoreLog.Init(backup: true);

		if (severity != Severity.Debug)
		{
			Severity minSeverity = Community.Runtime?.Config?.LogSeverity ?? Severity.Notice;
			if (severity > minSeverity) return;
		}

		var textMessage = message?.ToString();

		switch (severity)
		{
			case Severity.Error:
				var dex = ex?.Demystify() ?? ex;

				if (dex != null)
				{
					var exceptionResult = $"({dex?.Message})\n{dex.GetFullStackTrace(false)}";
					CoreLog._queueLog($"[ERRO] {textMessage} {exceptionResult}");
					if (nativeLog) UnityEngine.Debug.LogError($"{textMessage} {exceptionResult}");
				}
				else
				{
					CoreLog._queueLog($"[ERRO] {textMessage}");
					if (nativeLog) UnityEngine.Debug.LogError(textMessage);
				}

				OnErrorCallback?.Invoke(textMessage, dex, verbosity);
				break;

			case Severity.Warning:
				CoreLog._queueLog($"[WARN] {textMessage}");
				if (nativeLog) UnityEngine.Debug.LogWarning(textMessage);
				OnWarningCallback?.Invoke(textMessage, verbosity);
				break;

			case Severity.Notice:
				CoreLog._queueLog($"[INFO] {textMessage}");
				if (nativeLog) UnityEngine.Debug.Log(textMessage);
				OnNoticeCallback?.Invoke(textMessage, verbosity);
				break;

			case Severity.Debug:
				int minVerbosity = Community.Runtime?.Config?.LogVerbosity ?? -1;
				if (verbosity > minVerbosity) break;
				CoreLog._queueLog($"[INFO] {textMessage}");
				if (nativeLog) UnityEngine.Debug.Log(textMessage);
				OnDebugCallback?.Invoke(textMessage, verbosity);
				break;

			default:
				throw new Exception($"Severity {severity} not implemented.");
		}
	}

	public static void Dispose()
	{
		CoreLog.Dispose();
	}

#if DEBUG
	internal static string _getFileNameEx(string input)
	{
		// For some reason Path.GetFileName() is not working with
		// [CallerFilePath]. Trying to be OS agnostic..
		string[] arr = input.Split((input.Contains("/") ? '/' : '\\'));
		string ret = arr[arr.Length - 1];

		Array.Clear(arr, 0, arr.Length);
		return ret;
	}
#endif

	/// <summary>
	/// Outputs to the game's console a message with severity level 'DEBUG'.
	/// </summary>
	/// <param name="header"></param>
	/// <param name="message"></param>
	/// <param name="verbosity"></param>
	public static void Debug(object header, object message, int verbosity)
		=> Write(Severity.Debug, $"[CRBN.{header}] {message}", null, verbosity);

	/// <summary>
	/// Outputs to the game's console a message with severity level 'DEBUG'.
	/// </summary>
	/// <param name="message"></param>
	/// <param name="verbosity"></param>
	public static void Debug(object message, int verbosity)
		=> Write(Severity.Debug, $"[CRBN] {message}", null, verbosity);

	/// <summary>
	/// Outputs to the game's console a message with severity level 'DEBUG'.
	/// </summary>
	/// <param name="header"></param>
	/// <param name="message"></param>
	public static void Debug(object header, object message)
		=> Write(Severity.Debug, $"[CRBN.{header}] {message}");

	/// <summary>
	/// Outputs to the game's console a message with severity level 'DEBUG'.
	/// </summary>
	/// <param name="message"></param>
	public static void Debug(object message)
		=> Write(Severity.Debug, $"[CRBN] {message}");

	/// <summary>
	/// Outputs to the game's console a message with severity level 'NOTICE'.
	/// </summary>
	/// <param name="message"></param>
	public static void Log(object message)
		=> Write(Severity.Notice, message);

	/// <summary>
	/// Outputs to the game's console a message with severity level 'WARNING'.
	/// DEBUG IS ENABLED FOR THIS METHOD.
	/// </summary>
	/// <param name="message"></param>
	public static void Warn(object message)
		=> Write(Severity.Warning, message);

	/// <summary>
	/// Outputs to the game's console a message with severity level 'ERROR'.
	/// </summary>
	/// <param name="message"></param>
	/// <param name="ex"></param>
	public static void Error(object message, Exception ex = null)
		=> Write(Severity.Error, message, ex);

	// Interface implementation workaround for static methods.
	void ILogger.Console(string message, Severity severity, Exception exception)
	{
		switch (severity)
		{
			case Severity.Error:
				Logger.Error(message, exception);
				break;

			case Severity.Warning:
				Logger.Warn(message);
				break;

			case Severity.Debug:
				Logger.Debug(message);
				break;

			default:
				Logger.Log(message);
				break;
		}
	}
}
