// Copyright (C) GameWright. Licensed under MIT.

using GameWright.Editor.Services;
using GameWright.Editor.Settings;
using UnityEngine;
using UnityEngine.UIElements;

namespace GameWright.Editor.MCP.Server
{
    internal sealed class GameWrightMCPHeaderStatusPanel
    {
        private readonly ISettingsController _settings;
        private readonly MCPServerService _server;
        private Label _statusLabel;
        private Label _versionLabel;

        public GameWrightMCPHeaderStatusPanel(ISettingsController settings, MCPServerService server)
        {
            _settings = settings;
            _server = server;
        }

        public void AddTo(VisualElement parent, VisualElement statusHost = null)
        {
            var titleRow = new VisualElement();
            titleRow.style.flexDirection = FlexDirection.Row;
            titleRow.style.alignItems = Align.Center;
            titleRow.style.marginBottom = 8;
            parent.Add(titleRow);

            var icon = GameWrightIcon.TabTexture;
            if (icon != null)
            {
                var logo = new Image { image = icon, scaleMode = ScaleMode.ScaleToFit };
                logo.style.width = 16;
                logo.style.height = 16;
                logo.style.marginRight = 8;
                logo.style.flexShrink = 0;
                titleRow.Add(logo);
            }

            var title = new Label("MCP Server");
            title.style.fontSize = 18;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.color = Color.white;
            titleRow.Add(title);

            _statusLabel = new Label();
            _statusLabel.style.fontSize = 14;
            if (statusHost != null)
            {
                _statusLabel.style.fontSize = 13;
                _statusLabel.style.unityTextAlign = TextAnchor.MiddleRight;
                _statusLabel.style.marginRight = 0;
                statusHost.style.marginRight = 0;
                statusHost.Add(_statusLabel);

                var spacer = new VisualElement();
                spacer.style.flexGrow = 1;
                titleRow.Add(spacer);
            }
            else
            {
                _statusLabel.style.flexGrow = 1;
                _statusLabel.style.marginLeft = 10;
                _statusLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
                titleRow.Add(_statusLabel);
            }

            _versionLabel = new Label();
            _versionLabel.style.fontSize = 13;
            _versionLabel.style.color = new Color(0.7f, 0.7f, 0.7f);
            _versionLabel.style.unityTextAlign = TextAnchor.MiddleRight;
            titleRow.Add(_versionLabel);

            Refresh();
        }

        public void Refresh()
        {
            RefreshVersion();
            RefreshStatus();
        }

        public void RefreshVersion()
        {
            if (_versionLabel != null)
                _versionLabel.text = $"v{GameWrightMCPUpdateChecker.CurrentState.CurrentVersion ?? PackageVersionUtility.CurrentVersion}";
        }

        public void RefreshStatus()
        {
            if (_statusLabel == null)
                return;

            if (_server?.IsRunning == true)
            {
                if (_server.IsAttachedToExistingTransport)
                {
                    _statusLabel.text = $"Attached to existing server on http://127.0.0.1:{_server.Port}/ ({_settings.MCPToolExportProfile ?? "core"})";
                    _statusLabel.style.color = new Color(0.4f, 1f, 0.4f);
                }
                else
                {
                    _statusLabel.text = $"Running on http://127.0.0.1:{_server.Port}/ ({_settings.MCPToolExportProfile ?? "core"})";
                    _statusLabel.style.color = new Color(0.4f, 1f, 0.4f);
                }
            }
            else
            {
                _statusLabel.text = "Stopped";
                _statusLabel.style.color = new Color(0.9f, 0.35f, 0.35f);
            }
        }
    }
}
