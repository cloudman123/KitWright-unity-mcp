// Copyright (C) GameWright. Licensed under MIT.

using GameWright.Editor.DI;
using UnityEngine;

namespace GameWright.Editor.Settings
{
    internal static class PluginDebugLogger
    {
        public static void Log(string message)
        {
            if (!IsEnabled || string.IsNullOrEmpty(message))
                return;

            Debug.Log(message);
        }

        public static bool IsEnabled
        {
            get
            {
                try
                {
                    var settings = RootScopeServices.Services?.GetService(typeof(ISettingsController)) as ISettingsController;
                    return settings?.PluginDebugLoggingEnabled == true;
                }
                catch
                {
                    return false;
                }
            }
        }
    }
}
