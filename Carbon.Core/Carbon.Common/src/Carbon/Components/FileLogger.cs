﻿/*
 *
 * Copyright (c) 2022-2023 Carbon Community 
 * All rights reserved.
 *
 */

namespace Carbon;

public class FileLogger : IDisposable
{
	public string Name { get; set; } = "default";

	/// <summary>
	/// By default, each log file gets split when it reaches exactly 2.5MB in file size and sent in the archive folder.
	/// </summary>
	public int SplitSize { get; set; } = (int)(2.5f * 1000000f);

	internal bool _hasInit;
	internal List<string> _buffer = new();
	internal StreamWriter _file;

	public FileLogger() { }
	public FileLogger(string name)
	{
		Name = name;
	}

	public virtual void Init(bool archive = false, bool backup = false)
	{
		if (_hasInit && !archive) return;

		var path = Path.Combine(Defines.GetLogsFolder(), $"{Name}.log");
		var archiveFolder = Path.Combine(Defines.GetLogsFolder(), "archive");
		OsEx.Folder.Create(archiveFolder);

		if (backup && OsEx.File.Exists(path))
		{
			var backupPath = Path.Combine(archiveFolder, $"{Name}.backup.{DateTime.Now:yyyy.MM.dd}.log");
			var logContent = OsEx.File.ReadText(path);

			if (OsEx.File.Exists(backupPath))
			{
				File.AppendAllText(backupPath, logContent);
			}
			else
			{
				OsEx.File.Create(backupPath, logContent);
			}
		}

		if (archive)
		{
			if (OsEx.File.Exists(path))
			{
				OsEx.File.Move(path, Path.Combine(archiveFolder, $"{Name}.{DateTime.Now:yyyy.MM.dd.HHmmss}.log"));
			}
		}

		try
		{
			File.Delete(path);
		}
		catch { }

		_hasInit = true;

		_file = new StreamWriter(path, append: true);
	}
	public virtual void Dispose()
	{
		_file.Flush();
		_file.Close();
		_file.Dispose();

		_hasInit = false;
	}
	public virtual void _flush()
	{
		var buffer = Facepunch.Pool.GetList<string>();
		buffer.AddRange(_buffer);

		foreach (var line in buffer)
		{
			_file?.WriteLine(line);
		}

		_file.Flush();
		_buffer.Clear();
		Facepunch.Pool.FreeList(ref buffer);

		if (_file.BaseStream.Length > SplitSize)
		{
			Dispose();
			Init(archive: true);
		}
	}
	internal void _queueLog(string message)
	{
		if (Community.IsConfigReady && Community.Runtime.Config.LogFileMode == 0) return;

		_buffer.Add($"[{Logger.GetDate()}] {message}");
		if (Community.IsConfigReady && Community.Runtime.Config.LogFileMode == 2) _flush();
	}
}
