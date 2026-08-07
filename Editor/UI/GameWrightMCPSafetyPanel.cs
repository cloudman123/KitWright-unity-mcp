// Copyright (C) GameWright. Licensed under MIT.

using GameWright.Editor.Settings;
using UnityEngine;
using UnityEngine.UIElements;

namespace GameWright.Editor.MCP.Server
{
    internal static class GameWrightMCPSafetyPanel
    {
        public static void AddTo(VisualElement parent, ISettingsController settings)
        {
            if (parent == null || settings == null)
                return;

            AddSafetyBox(parent, new VisualElement().Card(),
                "Default execute_code safety checks",
                "Default for execute_code calls when safety_checks is omitted. Explicit safety_checks=false can still bypass this for trusted local calls.",
                settings.ExecuteCodeSafetyChecksEnabled,
                value => settings.ExecuteCodeSafetyChecksEnabled = value);

            AddSafetyBox(parent, new VisualElement().Card(),
                "Strict filesystem guard",
                "Adds checks for broad System.IO file writes, raw file streams, and absolute/user/system/traversal paths. This is a defensive guard, not a complete sandbox.",
                settings.ExecuteCodeStrictFilesystemSafetyEnabled,
                value => settings.ExecuteCodeStrictFilesystemSafetyEnabled = value);

            AddSafetyBox(parent, new VisualElement().Card(),
                "Auto-inject project namespaces",
                "Off by default. When enabled, only namespaces from loaded Library/ScriptAssemblies assemblies are injected; explicit using directives remain the least ambiguous option.",
                settings.ExecuteCodeProjectNamespaceInjectionEnabled,
                value => settings.ExecuteCodeProjectNamespaceInjectionEnabled = value);
        }

        private static void AddSafetyBox(
            VisualElement parent,
            VisualElement box,
            string title,
            string hint,
            bool value,
            System.Action<bool> onChanged)
        {
            var toggle = new MCPSwitchToggle(title);
            toggle.tooltip = hint;
            toggle.SetValueWithoutNotify(value);
            toggle.RegisterValueChangedCallback(onChanged);
            box.Add(toggle);

            parent.Add(box);
        }
    }
}
