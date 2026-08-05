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

        private VisualElement CreateFooter()
        {
            var footer = new VisualElement();
            footer.style.flexShrink = 0;
            footer.style.borderTopWidth = 1;
            footer.style.borderTopColor = new Color(0.08f, 0.08f, 0.08f);
            footer.style.backgroundColor = new Color(0.13f, 0.13f, 0.13f);
            footer.Padding(6, 8, 8, 8);

            var checkButton = new Button(GameWrightMCPUpdateChecker.CheckForUpdates)
            {
                text = "Check for Updates"
            };
            checkButton.style.height = 28;
            checkButton.style.marginTop = 0;
            checkButton.style.marginBottom = 0;
            checkButton.style.marginLeft = 0;
            checkButton.style.marginRight = 0;
            checkButton.Rounded(5);
            checkButton.style.backgroundColor = new Color(0.24f, 0.42f, 0.58f);
            checkButton.style.color = Color.white;
            checkButton.style.fontSize = 13;
            checkButton.style.unityFontStyleAndWeight = FontStyle.Bold;
            checkButton.style.transitionProperty = new List<StylePropertyName> { "background-color" };
            checkButton.style.transitionDuration = new List<TimeValue> { new TimeValue(0.12f, TimeUnit.Second) };
            checkButton.RegisterCallback<MouseEnterEvent>(_ =>
                checkButton.style.backgroundColor = new Color(0.30f, 0.52f, 0.70f));
            checkButton.RegisterCallback<MouseLeaveEvent>(_ =>
                checkButton.style.backgroundColor = new Color(0.24f, 0.42f, 0.58f));
            footer.Add(checkButton);

            return footer;
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
