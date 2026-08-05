// Copyright (C) GameWright. Licensed under MIT.

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using GameWright.Editor.Settings;

namespace GameWright.Editor.MCP.Server
{
    internal sealed class GameWrightPluginSettingsPanel : IMCPWindowPanel
    {
        private readonly ISettingsController _settingsController;
        private VisualElement _container;
        private MCPSwitchToggle _debugLoggingToggle;

        public GameWrightPluginSettingsPanel(ISettingsController settingsController)
        {
            _settingsController = settingsController;
        }

        public void Build(VisualElement container)
        {
            _container = container;
            _settingsController.OnSettingsChanged += RefreshStatus;
            BuildUI();
        }

        public void Dispose()
        {
            if (_settingsController != null)
                _settingsController.OnSettingsChanged -= RefreshStatus;
        }

        private void BuildUI()
        {
            _container.Clear();

            var title = new Label("MCP Settings");
            title.style.fontSize = 18;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.color = Color.white;
            title.style.marginBottom = 4;
            _container.Add(title);

            var hint = new Label("Project-level settings for the GameWright MCP for Unity plugin. Safety checks and debug logging are stored per project.");
            hint.style.fontSize = 13;
            hint.style.color = new Color(0.65f, 0.65f, 0.65f);
            hint.style.whiteSpace = WhiteSpace.Normal;
            hint.style.marginBottom = 10;
            _container.Add(hint);

            var settingsSection = CreateSection();
            var settingsFoldout = new Foldout { text = "Settings", value = true }.Persist("Settings");
            var foldoutLabel = settingsFoldout.Q<Toggle>()?.Q<Label>();
            if (foldoutLabel != null)
            {
                foldoutLabel.style.fontSize = 12;
                foldoutLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
                foldoutLabel.style.color = new Color(0.55f, 0.7f, 0.9f);
                foldoutLabel.style.flexGrow = 1;
            }
            settingsSection.Add(settingsFoldout);
            _container.Add(settingsSection);

            var autostartSection = CreateSection();
            var autostartToggle = new MCPSwitchToggle("Autostart on Unity open");
            autostartToggle.tooltip = "When enabled, the MCP server starts automatically the next time you open this Unity project (if it was connected).";
            autostartToggle.SetValueWithoutNotify(_settingsController.MCPAutostartEnabled);
            autostartToggle.RegisterValueChangedCallback(value => _settingsController.MCPAutostartEnabled = value);
            autostartSection.Add(autostartToggle);
            settingsFoldout.Add(autostartSection);

            GameWrightMCPSafetyPanel.AddTo(settingsFoldout, _settingsController, CreateSection);

            var debugSection = CreateSection();

            _debugLoggingToggle = new MCPSwitchToggle("Enable debug logging");
            _debugLoggingToggle.tooltip = "When enabled, plugin lifecycle, MCP request, transport, and tool execution traces are written to the Unity Console. Warnings and errors are always written.";
            _debugLoggingToggle.SetValueWithoutNotify(_settingsController.PluginDebugLoggingEnabled);
            _debugLoggingToggle.RegisterValueChangedCallback(value =>
            {
                _settingsController.PluginDebugLoggingEnabled = value;
                RefreshStatus();
            });
            debugSection.Add(_debugLoggingToggle);

            settingsFoldout.Add(debugSection);

            var compactSection = CreateSection();
            var compactToggle = new MCPSwitchToggle("Compact tool schema");
            compactToggle.tooltip = "Strip parameter descriptions and trim each tool description to its first sentence when exporting the schema. Saves ~8-13k tokens per session at the cost of terser tool docs.";
            compactToggle.SetValueWithoutNotify(_settingsController.MCPCompactSchemaEnabled);
            compactToggle.RegisterValueChangedCallback(value => _settingsController.MCPCompactSchemaEnabled = value);
            compactSection.Add(compactToggle);
            settingsFoldout.Add(compactSection);

            var logCapacitySection = CreateSection();
            logCapacitySection.Add(BuildSizeSlider("Recent activity log limit",
                "Maximum number of tool call entries kept in the Recent Activity log buffer (circular buffer).",
                () => _settingsController.ActivityLogCapacity,
                v => _settingsController.ActivityLogCapacity = v,
                50, 1000));
            settingsFoldout.Add(logCapacitySection);

            var screenshotSection = CreateSection();
            screenshotSection.Add(BuildSizeSlider("Game/Scene screenshot size",
                "Longest side of capture_game_view/scene_view when no width/height is passed. Smaller = fewer tokens.",
                () => _settingsController.ScreenshotDefaultSize,
                v => _settingsController.ScreenshotDefaultSize = v));
            var wndSlider = BuildSizeSlider("Editor window screenshot size",
                "Longest side of capture_editor_window when no width/height is passed. Smaller = fewer tokens.",
                () => _settingsController.EditorWindowScreenshotSize,
                v => _settingsController.EditorWindowScreenshotSize = v);
            wndSlider.style.marginTop = 8;
            screenshotSection.Add(wndSlider);
            settingsFoldout.Add(screenshotSection);

            GameWrightMCPRequirementsPanel.AddTo(_container, CreateSection);

            RefreshStatus();
        }

        private void RefreshStatus()
        {
            if (_settingsController == null)
                return;

            if (_debugLoggingToggle != null)
                _debugLoggingToggle.SetValueWithoutNotify(_settingsController.PluginDebugLoggingEnabled);
        }

        private const int ScreenshotSizeMin = 64;
        private const int ScreenshotSizeMax = 4096;

        private VisualElement BuildSizeSlider(string labelText, string tooltip, Func<int> getter, Action<int> setter, int min = ScreenshotSizeMin, int max = ScreenshotSizeMax)
        {
            var accent = new Color(0.30f, 0.66f, 0.36f);
            var root = new VisualElement();
            root.style.flexDirection = FlexDirection.Row;
            root.style.alignItems = Align.Center;

            var title = new Label(labelText);
            title.style.fontSize = 13;
            title.style.color = new Color(0.88f, 0.88f, 0.9f);
            title.style.flexShrink = 0;
            title.style.width = 190;
            title.style.marginRight = 10;
            title.tooltip = tooltip;
            root.Add(title);

            var track = new VisualElement();
            track.style.flexGrow = 1;
            track.style.height = 8;
            track.style.backgroundColor = new Color(0.22f, 0.22f, 0.24f);
            track.Rounded(4);
            track.style.justifyContent = Justify.Center;

            var fill = new VisualElement();
            fill.style.position = Position.Absolute;
            fill.style.left = 0;
            fill.style.height = 8;
            fill.style.backgroundColor = accent;
            fill.Rounded(4);
            track.Add(fill);

            var handle = new VisualElement();
            handle.style.position = Position.Absolute;
            handle.style.width = 18;
            handle.style.height = 18;
            handle.style.backgroundColor = Color.white;
            handle.Rounded(9).Border(2, accent);
            track.Add(handle);
            root.Add(track);

            var valueBox = new IntegerField();
            valueBox.style.width = 60;
            valueBox.style.flexGrow = 0;
            valueBox.style.flexShrink = 0;
            valueBox.style.marginLeft = 10;
            var vbInput = valueBox.Q("unity-text-input");
            if (vbInput != null)
            {
                vbInput.style.backgroundColor = new Color(0.12f, 0.14f, 0.12f);
                vbInput.style.unityTextAlign = TextAnchor.MiddleCenter;
            }
            var vbText = valueBox.Q<Label>();
            if (vbText != null) vbText.style.color = accent;
            root.Add(valueBox);

            var current = Mathf.Clamp(getter(), min, max);

            void Paint(int v)
            {
                var t = (v - min) / (float)(max - min);
                var w = track.resolvedStyle.width;
                fill.style.width = t * w;
                handle.style.left = t * w - 9;
                handle.style.top = -5;
                valueBox.SetValueWithoutNotify(v);
            }

            void Commit(int v)
            {
                current = Mathf.Clamp(v, min, max);
                setter(current);
                Paint(current);
            }

            void SetFromMouse(float localX, float trackWidth)
            {
                var t = Mathf.Clamp01(localX / Mathf.Max(1f, trackWidth));
                Commit(Mathf.RoundToInt(min + t * (max - min)));
            }

            var dragging = false;
            track.RegisterCallback<MouseDownEvent>(e => { dragging = true; track.CaptureMouse(); SetFromMouse((float)e.localMousePosition.x, track.resolvedStyle.width); });
            track.RegisterCallback<MouseMoveEvent>(e => { if (dragging) SetFromMouse((float)e.localMousePosition.x, track.resolvedStyle.width); });
            track.RegisterCallback<MouseUpEvent>(e => { dragging = false; track.ReleaseMouse(); });
            track.RegisterCallback<MouseCaptureOutEvent>(_ => dragging = false);
            track.RegisterCallback<GeometryChangedEvent>(_ => Paint(current));
            valueBox.RegisterCallback<FocusOutEvent>(_ => Commit(valueBox.value));
            valueBox.RegisterCallback<KeyDownEvent>(e => { if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter) Commit(valueBox.value); });

            Paint(current);
            return root;
        }

        private static VisualElement CreateSection()
        {
            var section = new VisualElement();
            section.style.backgroundColor = new Color(0.155f, 0.155f, 0.16f);
            section.Rounded(6);
            section.Border(1, new Color(0.09f, 0.09f, 0.09f));
            section.Padding(8, 10, 8, 10);
            section.style.marginBottom = 8;
            return section;
        }
    }

    // Project-settings checklist for tools that need the game to keep running while
    // Unity is unfocused (input simulation). Each row shows a
    // pass/warn icon and a Fix button that flips the setting.
    internal static class GameWrightMCPRequirementsPanel
    {
        private sealed class Requirement
        {
            public string Label;
            public Func<bool> IsMet;
            public Action Fix;
            public string Hint;
        }

        private static readonly Requirement[] Requirements =
        {
            new Requirement
            {
                Label = "Run In Background enabled",
                Hint = "Lets Play Mode keep ticking while Unity is unfocused, so input simulation runs when you're in another window.",
                IsMet = () => PlayerSettings.runInBackground,
                Fix = () => PlayerSettings.runInBackground = true
            },
            new Requirement
            {
                Label = "Recompile after finished playing",
                Hint = "Set Script Changes While Playing to 'Recompile After Finished Playing' so entering Play Mode with dirty scripts doesn't recompile mid-request and drop MCP tool calls.",
                IsMet = () => EditorPrefs.GetInt("ScriptCompilationDuringPlay", 0) == 1,
                Fix = () => EditorPrefs.SetInt("ScriptCompilationDuringPlay", 1)
            }
        };

        public static void AddTo(VisualElement parent, Func<VisualElement> sectionFactory)
        {
            var section = sectionFactory();

            var foldout = new Foldout { text = "Requirements", value = true }.Persist("Requirements");
            var foldoutLabel = foldout.Q<Toggle>()?.Q<Label>();
            if (foldoutLabel != null)
            {
                foldoutLabel.style.fontSize = 12;
                foldoutLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
                foldoutLabel.style.color = new Color(0.55f, 0.7f, 0.9f);
                foldoutLabel.style.flexGrow = 1;
            }
            section.Add(foldout);
            parent.Add(section);

            var rows = new List<Action>();
            var fixAllButton = new Button { text = "Fix All" };
            fixAllButton.style.height = 20;
            fixAllButton.style.marginRight = 0;
            fixAllButton.clicked += () =>
            {
                foreach (var req in Requirements)
                    if (!req.IsMet()) req.Fix();
                foreach (var r in rows) r();
            };
            foldout.Q<Toggle>()?.Add(fixAllButton);

            void RefreshFixAll()
            {
                var unmet = Requirements.Count(req => !req.IsMet());
                fixAllButton.style.display = unmet >= 2 ? DisplayStyle.Flex : DisplayStyle.None;
            }

            foreach (var req in Requirements)
                rows.Add(BuildRow(foldout, req, RefreshFixAll));

            foreach (var r in rows) r();
            RefreshFixAll();
        }

        private static Action BuildRow(VisualElement container, Requirement req, Action onChanged)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginBottom = 4;

            var icon = new Image { scaleMode = ScaleMode.ScaleToFit };
            icon.style.width = 16;
            icon.style.height = 16;
            icon.style.marginRight = 6;
            row.Add(icon);

            var label = new Label(req.Label);
            label.style.flexGrow = 1;
            label.tooltip = req.Hint;
            row.Add(label);

            var fixButton = new Button { text = "Fix" };
            fixButton.style.height = 20;
            row.Add(fixButton);

            container.Add(row);

            void Refresh()
            {
                var met = req.IsMet();
                icon.image = EditorGUIUtility.IconContent(met ? "TestPassed" : "console.warnicon.sml").image;
                fixButton.style.display = met ? DisplayStyle.None : DisplayStyle.Flex;
            }

            fixButton.clicked += () => { req.Fix(); Refresh(); onChanged?.Invoke(); };
            return Refresh;
        }
    }
}
