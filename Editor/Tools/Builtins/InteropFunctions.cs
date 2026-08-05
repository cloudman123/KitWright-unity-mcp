// Copyright (C) GameWright. Licensed under MIT.

using System;
using DescriptionAttribute = System.ComponentModel.DescriptionAttribute;
using GameWright.Editor.Interop;
using GameWright.Editor.Tools.Helpers;

namespace GameWright.Editor.Tools.Builtins
{
    [ToolProvider("Interop")]
    internal static class InteropFunctions
    {
        [Description("Report the status of known third-party code-patching plugins (currently: SingularityGroup Hot Reload). " +
                     "Returns per plugin: loaded, server health, whether it suppresses Unity's normal compile pipeline, " +
                     "and its recent patch timeline (which methods were hot-patched, partially applied, or failed). " +
                     "Use after editing .cs files while such a plugin is active to verify the new code actually reached the running domain: " +
                     "a PatchApplied entry naming your method means it is live; PartiallySupportedChange/Failure/UndetectedChange means it is NOT " +
                     "and a real recompile (plugin stopped) is required. When any plugin reports suppresses_compilation=true, " +
                     "request_recompile and execute_code refreshes intentionally skip compile-escalation and trust the plugin.")]
        [ReadOnlyTool]
        public static object GetCodePatchingStatus()
        {
            try
            {
                var loaded = HotReload.IsLoaded;
                return Response.Success(
                    loaded
                        ? "A code-patching plugin is active: Unity compile pipeline is suppressed; structural changes (new classes/fields/signatures) will NOT apply until it is stopped and a real recompile runs."
                        : "No code-patching plugin is suppressing the Unity compile pipeline.",
                    new { compile_pipeline_suppressed = loaded, plugins = new[] { HotReload.GetStatus() } });
            }
            catch (Exception ex)
            {
                return ToolResultFormatter.Exception(ex);
            }
        }
    }
}
