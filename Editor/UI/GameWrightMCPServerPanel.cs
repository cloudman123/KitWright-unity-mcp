// Copyright (C) GameWright. Licensed under MIT.

using GameWright.Editor.Settings;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace GameWright.Editor.MCP.Server
{
    internal sealed class GameWrightMCPServerPanel : IMCPWindowPanel
    {
        private readonly ISettingsController _settingsController;
        private readonly MCPServerService _mcpServer;

        private VisualElement _container;
        private GameWrightMCPHeaderStatusPanel _headerStatusPanel;
        private GameWrightMCPUpdatePanel _updatePanel;
        private GameWrightMCPRecentActivityPanel _activityPanel;

        public GameWrightMCPServerPanel(
            ISettingsController settingsController,
            MCPServerService mcpServer)
        {
            _settingsController = settingsController;
            _mcpServer = mcpServer;
        }

        public void Build(VisualElement container)
        {
            _container = container;

            GameWrightMCPUpdateChecker.StateChanged -= OnUpdateStateChanged;
            GameWrightMCPUpdateChecker.StateChanged += OnUpdateStateChanged;

            _mcpServer.InteractionLog.OnEntryAdded -= OnLogEntryAdded;
            _mcpServer.InteractionLog.OnEntryAdded += OnLogEntryAdded;

            BuildUI();
            GameWrightMCPUpdateChecker.MaybeCheckForUpdatesInBackground();
        }

        private void BuildUI()
        {
            DisposePanels();

            _container.Clear();

            var connectionSection = CreateSection();
            var connectionFoldout = new Foldout { text = "Connection", value = true }.Persist("Connection");
            var connToggle = connectionFoldout.Q<Toggle>();
            var connToggleLabel = connToggle?.Q<Label>();
            if (connToggleLabel != null)
            {
                connToggleLabel.style.fontSize = 12;
                connToggleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
                connToggleLabel.style.color = new Color(0.55f, 0.7f, 0.9f);
                connToggleLabel.style.flexGrow = 1;
            }

            _headerStatusPanel = new GameWrightMCPHeaderStatusPanel(_settingsController, _mcpServer);
            _headerStatusPanel.AddTo(_container, connToggle);

            _updatePanel = new GameWrightMCPUpdatePanel();
            _updatePanel.AddTo(_container);

            connectionSection.Add(connectionFoldout);
            new GameWrightMCPServerControlsPanel(
                    _settingsController,
                    _mcpServer,
                    () => _headerStatusPanel?.RefreshStatus())
                .AddTo(connectionFoldout);
            _container.Add(connectionSection);

            var clientSection = CreateSection();
            new GameWrightMCPClientConfigPanel(
                    _settingsController,
                    _mcpServer,
                    BuildUI)
                .AddTo(clientSection);
            _container.Add(clientSection);

            _activityPanel = new GameWrightMCPRecentActivityPanel(_mcpServer);
            _activityPanel.AddTo(_container);
        }

        private static VisualElement CreateSection()
        {
            var section = new VisualElement().Card().Padding(8, 10, 10, 10);
            section.style.minWidth = 0;
            section.style.marginBottom = 10;
            return section;
        }

        public void Dispose()
        {
            if (_mcpServer?.InteractionLog != null)
                _mcpServer.InteractionLog.OnEntryAdded -= OnLogEntryAdded;

            GameWrightMCPUpdateChecker.StateChanged -= OnUpdateStateChanged;
            DisposePanels();
        }

        private void DisposePanels()
        {
            _activityPanel?.Dispose();
            _activityPanel = null;
        }

        private void OnUpdateStateChanged()
        {
            EditorApplication.delayCall += () =>
            {
                _headerStatusPanel?.RefreshVersion();
                _updatePanel?.Refresh();
            };
        }

        private void OnLogEntryAdded(MCPLogEntry entry)
        {
            _activityPanel?.OnEntryAdded(entry);
        }
    }
}
