// Copyright (C) GameWright. Licensed under MIT.

using System.Collections.Generic;
using GameWright.Editor.DI;
using GameWright.Editor.Settings;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace GameWright.Editor.MCP.Server
{
    internal class GameWrightMCPWindow : EditorWindow
    {
        private enum Tab
        {
            Server,
            Settings,
            Skills,
            ToolExposure,
            Integrations
        }

        private static readonly (Tab key, string label, string icon)[] Tabs =
        {
            (Tab.Server, "Server", "d_Profiler.NetworkOperations"),
            (Tab.Settings, "Settings", "d_SettingsIcon"),
            (Tab.Skills, "Skills", "d_CustomTool"),
            (Tab.ToolExposure, "Tool Exposure", "d_FilterByType"),
            (Tab.Integrations, "Integrations", "d_Package Manager"),
        };

        internal const string FlatButtonClass = "gw-flat-button";

        private ISettingsController _settingsController;
        private MCPServerService _mcpServer;
        private VisualElement _contentContainer;
        private IMCPWindowPanel _activePanel;
        private MCPTabBar<Tab> _tabBar;
        private Tab _activeTab = Tab.Server;
        private bool? _lastRunning;

        [MenuItem("Window/GameWright/MCP Window", false, 0)]
        public static void ShowWindow()
        {
            var window = GetWindow<GameWrightMCPWindow>("GameWright MCP");
            window.minSize = new Vector2(460, 560);
            window.Show();
        }

        public void CreateGUI()
        {
            var icon = GameWrightIcon.TabTexture;
            if (icon != null)
                titleContent = new GUIContent("GameWright MCP", icon);

            _settingsController = RootScopeServices.Services?.GetService(typeof(ISettingsController))
                as ISettingsController;
            _mcpServer = RootScopeServices.Services?.GetService(typeof(MCPServerService))
                as MCPServerService;

            if (_settingsController == null || _mcpServer == null)
            {
                rootVisualElement.Add(new Label("Failed to initialize services."));
                return;
            }

            BuildShell();
            _tabBar.Select(_activeTab);
            UpdateServerTabIcon();
        }

        private void Update()
        {
            UpdateServerTabIcon();
        }

        private static readonly Color RunningIconColor = new Color(0.30f, 0.95f, 0.45f);

        private void UpdateServerTabIcon()
        {
            var running = _mcpServer?.IsRunning ?? false;
            if (_lastRunning == running)
                return;
            _lastRunning = running;

            _tabBar?.TintIcon(Tab.Server, running ? RunningIconColor : Color.white);
        }

        private void OnDestroy()
        {
            GameWrightMCPUpdateChecker.StateChanged -= RefreshUpdateButton;
            _activePanel?.Dispose();
            _activePanel = null;
        }

        private void BuildShell()
        {
            rootVisualElement.Clear();
            rootVisualElement.style.flexGrow = 1;
            rootVisualElement.style.backgroundColor = new Color(0.18f, 0.18f, 0.18f);

            _tabBar = new MCPTabBar<Tab>(Tabs, SelectTab);
            rootVisualElement.Add(_tabBar.Root);

            _contentContainer = new VisualElement();
            _contentContainer.style.flexGrow = 1;
            _contentContainer.Padding(10, 10, 10, 10);
            rootVisualElement.Add(_contentContainer);

            rootVisualElement.Add(CreateFooter());
        }

        private VisualElement _updateFooter;
        private Button _updateButton;

        private VisualElement CreateFooter()
        {
            _updateFooter = new VisualElement();
            _updateFooter.style.flexShrink = 0;
            _updateFooter.style.borderTopWidth = 1;
            _updateFooter.style.borderTopColor = new Color(0.08f, 0.08f, 0.08f);
            _updateFooter.style.backgroundColor = new Color(0.13f, 0.13f, 0.13f);
            _updateFooter.Padding(6, 8, 8, 8);
            _updateFooter.style.display = DisplayStyle.None;

            _updateButton = new Button(GameWrightMCPUpdateChecker.UpdateToLatestFromWindow);
            _updateButton.style.height = 28;
            _updateButton.style.marginTop = 0;
            _updateButton.style.marginBottom = 0;
            _updateButton.style.marginLeft = 0;
            _updateButton.style.marginRight = 0;
            _updateButton.Rounded(5);
            _updateButton.style.backgroundColor = new Color(0.30f, 0.66f, 0.36f);
            _updateButton.style.color = Color.white;
            _updateButton.style.fontSize = 13;
            _updateButton.style.unityFontStyleAndWeight = FontStyle.Bold;
            _updateButton.style.transitionProperty = new List<StylePropertyName> { "background-color" };
            _updateButton.style.transitionDuration = new List<TimeValue> { new TimeValue(0.12f, TimeUnit.Second) };
            _updateButton.RegisterCallback<MouseEnterEvent>(_ =>
                _updateButton.style.backgroundColor = new Color(0.36f, 0.76f, 0.44f));
            _updateButton.RegisterCallback<MouseLeaveEvent>(_ =>
                _updateButton.style.backgroundColor = new Color(0.30f, 0.66f, 0.36f));
            _updateFooter.Add(_updateButton);

            GameWrightMCPUpdateChecker.StateChanged -= RefreshUpdateButton;
            GameWrightMCPUpdateChecker.StateChanged += RefreshUpdateButton;
            RefreshUpdateButton();
            GameWrightMCPUpdateChecker.MaybeCheckForUpdatesInBackground();

            return _updateFooter;
        }

        private void RefreshUpdateButton()
        {
            if (_updateFooter == null || _updateButton == null)
                return;

            var state = GameWrightMCPUpdateChecker.CurrentState;
            var show = state.HasUpdateAvailable && !state.UpdateStarted;
            _updateFooter.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
            if (show)
            {
                _updateButton.text = $"Update to v{state.LatestVersion}";
                _updateButton.SetEnabled(!state.IsUpdating);
            }
        }

        private void SelectTab(Tab tab)
        {
            _activeTab = tab;

            _activePanel?.Dispose();
            _activePanel = null;
            _contentContainer.Clear();

            _activePanel = CreatePanel(tab);
            _activePanel.Build(_contentContainer);

            ApplyRoundedButtons(_contentContainer);
        }

        private static void ApplyRoundedButtons(VisualElement root)
        {
            root.Query<Button>().ForEach(button =>
            {
                // Segment buttons live inside a clipped rounded group and are intentionally flat.
                if (button.ClassListContains(FlatButtonClass))
                    return;

                button.Rounded(6);
            });
        }

        private IMCPWindowPanel CreatePanel(Tab tab)
        {
            switch (tab)
            {
                case Tab.Settings:
                    return new GameWrightPluginSettingsPanel(_settingsController);
                case Tab.Skills:
                    return new GameWrightProjectSkillsPanel(_settingsController);
                case Tab.ToolExposure:
                    return new GameWrightToolExposureEditorPanel(_settingsController, _mcpServer);
                case Tab.Integrations:
                    return new GameWrightIntegrationsPanel();
                default:
                    return new GameWrightMCPServerPanel(_settingsController, _mcpServer);
            }
        }
    }
}
