﻿using System;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Humanlights.Unity.Compiler;

namespace Carbon.Core.Processors
{
    public class AsyncPluginLoader : ThreadedJob
    {
        public string Source;
        public Assembly Assembly;
        public Exception Exception;

        private int _retries;

        protected override void ThreadFunction ()
        {
            try
            {
                Assembly = CompilerManager.Compile ( Source );
            }
            catch ( Exception exception )
            {
                if ( _retries <= 2 )
                {
                    _retries++;
                    ThreadFunction ();
                }
                else
                {
                    Exception = new Exception ( $"Failed compilation after {_retries} retries.", exception );
                }
            }
        }

        public static void AddCurrentDomainAssemblies ()
        {
            CompilerManager.ReferencedAssemblies.Clear ();

            var assemblies = AppDomain.CurrentDomain.GetAssemblies ();
            foreach ( var assembly in assemblies )
            {
                if ( CarbonLoader.AssemblyCache.Any ( x => x == assembly ) ) continue;

                if ( assembly.ManifestModule is ModuleBuilder builder )
                {
                    if ( !builder.IsTransient () )
                    {
                        CompilerManager.ReferencedAssemblies.Add ( assembly );
                    }
                }
                else
                {
                    CompilerManager.ReferencedAssemblies.Add ( assembly );
                }
            }

            CarbonCore.Log ( $" Added {CompilerManager.ReferencedAssemblies.Count:n0} references." );
        }
    }
}