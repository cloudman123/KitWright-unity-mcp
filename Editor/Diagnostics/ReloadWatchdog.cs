// Copyright (C) KitWright. Licensed under MIT.
// TEMPORARY DIAGNOSTIC -- added 2026-08-29 to chase editor hangs at "Reloading Domain".
// Delete this file (and the three call sites that reference it) once the hang is understood.

using System;
using System.Globalization;
using System.IO;
using KitWright.Editor.MCP.Server;
using KitWright.Editor.Services;
using KitWright.Editor.Threading;
using UnityEditor;
using UnityEngine;

namespace KitWright.Editor.Diagnostics
{
    [InitializeOnLoad]
    internal static class ReloadWatchdog
    {
        private static readonly object Gate = new object();

        /// <summary>Tool the editor thread is executing, or null. Set by FunctionInvoker.</summary>
        internal static volatile string CurrentTool;

        static ReloadWatchdog()
        {
            Write("domain-loaded");
            AssemblyReloadEvents.beforeAssemblyReload += () => Write("before-reload  " + Snapshot());
            AssemblyReloadEvents.afterAssemblyReload += () => Write("after-reload   " + Snapshot());
            EditorApplication.playModeStateChanged += state => Write("playmode " + state + "  " + Snapshot());
            EditorApplication.quitting += () => Write("quitting");
        }

        private static string Snapshot()
        {
            // A "before-reload" line with no matching "after-reload" is the hang, and these
            // fields say what this package was holding when the domain stopped coming back.
            var tool = CurrentTool;
            return string.Format(
                CultureInfo.InvariantCulture,
                "broker_inflight={0} last_broker_call={1} editor_thread_busy={2} since_pump={3:0.0}s tool={4}",
                MCPBrokerClientTransport.InFlightCount,
                MCPBrokerClientTransport.LastStartedPath ?? "-",
                EditorThreadHelper.WorkItemRunning,
                EditorThreadHelper.SinceLastPump.TotalSeconds,
                tool ?? "-");
        }

        internal static void Write(string line)
        {
            try
            {
                var path = Path.Combine(ApplicationPaths.ProjectRoot, "Library/KitWrightMcp/reload-trace.log");
                var dir = Path.GetDirectoryName(path);
                if (dir != null && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                lock (Gate)
                {
                    File.AppendAllText(path,
                        DateTime.Now.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture) + "  " + line + Environment.NewLine);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[KitWright] ReloadWatchdog could not write its trace: " + ex.Message);
            }
        }
    }
}
