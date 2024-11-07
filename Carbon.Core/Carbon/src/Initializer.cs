using System;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using API.Assembly;

namespace Carbon.Core;

public class Initializer : ICarbonComponent
{
	public void Awake(EventArgs args)
	{
		Logger.Debug($"A new instance of '{this}' created");
	}

	public void OnEnable(EventArgs args)
	{
		Logger.Debug($"Triggered '{this}' OnEnable");
	}
	public void OnDisable(EventArgs args)
	{
		Logger.Debug($"Triggered '{this}' OnDisable");
	}

	public void OnLoaded(EventArgs args)
	{
		try
		{
#if UNIX
			var os = OSPlatform.Linux;
			var otherOS = OSPlatform.Windows;
#else
			var os = OSPlatform.Windows;
			var otherOS = OSPlatform.Linux;
#endif

			if (!RuntimeInformation.IsOSPlatform(os))
			{
				Logger.Log(Environment.NewLine +
					$@"                                                          " + Environment.NewLine +
					$@"  ________ _______ ______ _______ _______ _______ _______ " + Environment.NewLine +
					$@" |  |  |  |   _   |   __ \    |  |_     _|    |  |     __|" + Environment.NewLine +
					$@" |  |  |  |       |      <       |_|   |_|       |    |  |" + Environment.NewLine +
					$@" |________|___|___|___|__|__|____|_______|__|____|_______|" + Environment.NewLine +
					$@"                                                          " + Environment.NewLine +
					$@"    YOU'RE TRYING TO RUN CARBON FOR {os} ON A {otherOS}   " + Environment.NewLine +
					$@"    MACHINE. THIS CANNOT HAPPEN.                          " + Environment.NewLine +
					$@"                                                          " + Environment.NewLine +
					$@"    PLEASE VERIFY SERVER FILES WITH STEAM, DOWNLOAD THE   " + Environment.NewLine +
					$@"    {otherOS} CARBON BUILD, THEN TRY AGAIN.               " + Environment.NewLine +
					$@"                                                          " + Environment.NewLine +
					$@"    IF THIS STILL PERSISTS, PLEASE REACH OUT TO US.       " + Environment.NewLine +
					$@"    THANK YOU <3                                          " + Environment.NewLine +
					$@"                                                          " + Environment.NewLine
				);

				Thread.Sleep(60000);
				UnityEngine.Application.Quit();
				return;
			}
		}
		catch (Exception e)
		{
			Logger.Error("Unable to assert operating system status.", e);
			return;
		}

		try
		{
			if (Type.GetType("Oxide.Core.Interface, Oxide.Core") is not null)
			{
				Logger.Log(Environment.NewLine +
					@"                                                          " + Environment.NewLine +
					@"  ________ _______ ______ _______ _______ _______ _______ " + Environment.NewLine +
					@" |  |  |  |   _   |   __ \    |  |_     _|    |  |     __|" + Environment.NewLine +
					@" |  |  |  |       |      <       |_|   |_|       |    |  |" + Environment.NewLine +
					@" |________|___|___|___|__|__|____|_______|__|____|_______|" + Environment.NewLine +
					@"                                                          " + Environment.NewLine +
					@"    WE HAVE DETECTED YOUR SERVER IS STILL PATCHED WITH    " + Environment.NewLine +
					@"    OXIDE. CARBON WILL NOT WORK IN THIS ENVIRONMENT.		" + Environment.NewLine +
					@"                                                          " + Environment.NewLine +
					@"    PLEASE VERIFY YOUR GAME FILES WITH STEAMCMD THEN		" + Environment.NewLine +
					@"    REBOOT THE SERVER.									" + Environment.NewLine +
					@"                                                          " + Environment.NewLine +
					@"    THIS SERVER WILL BE TERMINATED IN 60 SECONDS.         " + Environment.NewLine +
					@"    THANK YOU <3                                          " + Environment.NewLine +
					@"                                                          " + Environment.NewLine
				);

				Thread.Sleep(60000);
				UnityEngine.Application.Quit();
				return;
			}
		}
		catch (Exception e)
		{
			Logger.Error("Unable to assert assembly status.", e);
			return;
		}

		try
		{
			Type t = Type.GetType("ServerMgr, Assembly-CSharp");
			MethodInfo method = t.GetMethod("Shutdown", (BindingFlags)62) ?? null;

			if (method == null || !method.IsPublic)
			{
				Logger.Log(Environment.NewLine +
					@"                                                          " + Environment.NewLine +
					@"  ________ _______ ______ _______ _______ _______ _______ " + Environment.NewLine +
					@" |  |  |  |   _   |   __ \    |  |_     _|    |  |     __|" + Environment.NewLine +
					@" |  |  |  |       |      <       |_|   |_|       |    |  |" + Environment.NewLine +
					@" |________|___|___|___|__|__|____|_______|__|____|_______|" + Environment.NewLine +
					@"                                                          " + Environment.NewLine +
					@"    THE SERVER ASSEMBLY CODE IS NOT PUBLICIZED.           " + Environment.NewLine +
					@"    CARBON WILL NOT WORK PROPERLY.                        " + Environment.NewLine +
					@"                                                          " + Environment.NewLine +
					@"    PLEASE MAKE SURE UNITY DOORSTOP IS BEING EXECUTED.    " + Environment.NewLine +
					@"    IF THE PROBLEM PRESISTS, PLEASE OPEN A NEW ISSUE AT   " + Environment.NewLine +
					@"    GITHUB OR ASK FOR SUPPORT ON OUR DISCORD.             " + Environment.NewLine +
					@"				DISCORD.GG/CARBONMOD						" + Environment.NewLine +
					@"                                                          " + Environment.NewLine +
					@"    THIS SERVER WILL BE TERMINATED IN 60 SECONDS.         " + Environment.NewLine +
					@"    THANK YOU <3                                          " + Environment.NewLine +
					@"                                                          " + Environment.NewLine
				);

				Thread.Sleep(60000);
				UnityEngine.Application.Quit();
				return;
			}
		}
		catch (Exception e)
		{
			Logger.Error("Unable to assert assembly status.", e);
			return;
		}

		Logger.Log(Environment.NewLine +
			@"                                               " + Environment.NewLine +
			@"  ______ _______ ______ ______ _______ _______ " + Environment.NewLine +
			@" |      |   _   |   __ \   __ \       |    |  |" + Environment.NewLine +
			@" |   ---|       |      <   __ <   -   |       |" + Environment.NewLine +
			@" |______|___|___|___|__|______/_______|__|____|" + Environment.NewLine +
			@"                          discord.gg/carbonmod " + Environment.NewLine +
			@"                                               " + Environment.NewLine
		);

		try
		{
#if MINIMAL
			Logger.Log("Initializing minimal build...");
#else
			Logger.Log("Initializing...");
#endif
			if (CommunityInternal.InternalRuntime == null) CommunityInternal.InternalRuntime = new CommunityInternal();
			else CommunityInternal.InternalRuntime?.Uninitialize();

			CommunityInternal.InternalRuntime.Initialize();
		}
		catch (System.Exception e)
		{
			Logger.Error("Unable to initialize.", e.InnerException ?? e);
			return;
		}
	}
	public void OnUnloaded(EventArgs args)
	{	
		Logger.Log("Uninitalizing...");
		CommunityInternal.InternalRuntime?.Uninitialize();
		CommunityInternal.InternalRuntime = null;
	}
}
