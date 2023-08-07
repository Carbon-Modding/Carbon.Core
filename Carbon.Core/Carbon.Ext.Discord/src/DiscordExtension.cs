﻿/*
 *
 * Copyright (c) 2022-2023 Carbon Community 
 * Copyright (c) 2022 Oxide, uMod
 * All rights reserved.
 *
 */

using System;
using System.IO;
using System.Linq;
using API.Assembly;
using API.Events;
using Carbon;
using Carbon.Core;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Ext.Discord.Configuration;
using Oxide.Ext.Discord.Libraries.Command;
using Oxide.Ext.Discord.Libraries.Linking;
using Oxide.Ext.Discord.Libraries.Subscription;
using Oxide.Ext.Discord.Logging;
using Oxide.Plugins;
using Logger = Oxide.Ext.Discord.Logging.Logger;

namespace Oxide.Ext.Discord
{
	// Token: 0x02000004 RID: 4
	public class DiscordExtension : ICarbonExtension
	{
		internal static bool HasInit = false;

		public void Awake(EventArgs args)
		{
			GlobalLogger = (string.IsNullOrEmpty("") ? new Logger(DiscordLogLevel.Warning) : new Logger(DiscordLogLevel.Debug));
			GlobalLogger.Debug($"A new instance of '{this}' created");
		}

		public void OnLoaded(EventArgs args)
		{
			if (HasInit) return;
			HasInit = true;

			string text = Path.Combine(Defines.GetConfigsFolder(), "discord.config.json");
			bool flag = !File.Exists(text);
			if (flag)
			{
				DiscordConfig = new DiscordConfig(text);
				DiscordConfig.Save(null);
			}
			DiscordConfig = ConfigFile.Load<DiscordConfig>(text);
			DiscordConfig.Save(null);
			DiscordLink = new DiscordLink(GlobalLogger);
			DiscordCommand = new DiscordCommand(DiscordConfig.Commands.CommandPrefixes);
			DiscordSubscriptions = new DiscordSubscriptions(GlobalLogger);


			Community.Runtime.Events.Subscribe(
				CarbonEvent.PluginPreload, arg => OnPluginLoaded(arg as CarbonEventArgs));

			Community.Runtime.Events.Subscribe(
				CarbonEvent.PluginUnloaded, arg => OnPluginUnloaded(arg as CarbonEventArgs));
		}

		public void OnUnloaded(EventArgs args)
		{
			if (!HasInit) return;
			HasInit = false;

			foreach (DiscordClient client in DiscordClient.Clients.Values.ToList())
			{
				DiscordClient.CloseClient(client);
			}

			Community.Runtime.Events.Unsubscribe(
				CarbonEvent.PluginPreload, arg => OnPluginLoaded(arg as CarbonEventArgs));

			Community.Runtime.Events.Unsubscribe(
				CarbonEvent.PluginUnloaded, arg => OnPluginUnloaded(arg as CarbonEventArgs));

			GlobalLogger.Info("Disconnected all clients - shutdown.");
		}

		internal void OnPluginLoaded(CarbonEventArgs arg)
		{
			if (arg is CarbonEventArgs carbon)
			{
				DiscordClient.OnPluginAdded(carbon.Payload as RustPlugin);
			}
		}
		internal void OnPluginUnloaded(CarbonEventArgs arg)
		{
			if (arg is CarbonEventArgs carbon)
			{
				DiscordClient.OnPluginRemoved(carbon.Payload as RustPlugin);
			}
		}

		// Token: 0x04000015 RID: 21
		public const string TestVersion = "";

		// Token: 0x04000016 RID: 22
		internal static readonly JsonSerializerSettings ExtensionSerializeSettings = new JsonSerializerSettings
		{
			NullValueHandling = (NullValueHandling)1
		};

		// Token: 0x04000017 RID: 23
		private static readonly VersionNumber ExtensionVersion = new VersionNumber(2, 1, 9);

		public static string GetExtensionVersion => ExtensionVersion.ToString() + TestVersion;

		// Token: 0x04000018 RID: 24
		public static ILogger GlobalLogger;

		// Token: 0x04000019 RID: 25
		internal static DiscordLink DiscordLink;

		// Token: 0x0400001A RID: 26
		internal static DiscordCommand DiscordCommand;

		// Token: 0x0400001B RID: 27
		internal static DiscordSubscriptions DiscordSubscriptions;

		// Token: 0x0400001C RID: 28
		internal static DiscordConfig DiscordConfig;
	}
}
