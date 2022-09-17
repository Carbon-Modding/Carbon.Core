﻿using Harmony;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Carbon.Core.Extensions
{
    public static class ServerTagEx
    {
        internal static PropertyInfo _gameTags = AccessTools.TypeByName ( "Steamworks.SteamServer" ).GetProperty ( "GameTags", BindingFlags.Public | BindingFlags.Static );

        public static bool SetRequiredTag ( string tag )
        {
            var tags = _gameTags.GetValue ( null ) as string;

            if ( !tags.Contains ( $",{tag}" ) )
            {
                _gameTags.SetValue ( null, $"{tags},{tag}" );
                return true;
            }

            return false;
        }

        public static bool UnsetRequiredTag ( string tag )
        {
            var tags = _gameTags.GetValue ( null ) as string;

            if ( tags.Contains ( $",{tag}" ) )
            {
                _gameTags.SetValue ( null, tags.Replace ( $",{tag}", "" ) );
                return true;
            }

            return false;
        }
    }
}