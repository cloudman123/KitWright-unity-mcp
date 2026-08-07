// Copyright (C) GameWright. Licensed under MIT.

using GameWright.Editor.Services;
using GameWright.Editor.Settings;
using UnityEngine;
using UnityEngine.UIElements;

namespace GameWright.Editor.MCP.Server
{
    internal sealed class HeaderStatusPanel
    {
        private readonly ISettingsController _settings;
        private readonly MCPServerService _server;
        private Label _statusLabel;
        private Label _versionLabel;

        public HeaderStatusPanel(ISettingsController settings, MCPServerService server)
        {
            _settings = settings;
            _server = server;
        }

        public void AddTo(VisualElement parent, VisualElement statusHost = null)
        {
            var titleRow = new VisualElement().Card().Padding(6, 10, 6, 10);
            titleRow.style.flexDirection = FlexDirection.Row;
            titleRow.style.alignItems = Align.Center;
            parent.Add(titleRow);

            var icon = PluginIcon.LogoTextTexture;
            if (icon != null)
            {
                var logo = new Image { image = icon, scaleMode = ScaleMode.ScaleToFit };
                var h = 28;
                logo.style.height = h;
                logo.style.width = h * icon.width / icon.height;
                logo.style.flexShrink = 0;
                titleRow.Add(logo);
            }
            else
            {
                var title = new Label("GameWright");
                title.style.fontSize = 18;
                title.style.unityFontStyleAndWeight = FontStyle.Bold;
                title.style.color = Color.white;
                titleRow.Add(title);
            }

            _statusLabel = new Label();
            _statusLabel.style.fontSize = 14;
            if (statusHost != null)
            {
                _statusLabel.style.fontSize = 13;
                _statusLabel.style.unityTextAlign = TextAnchor.MiddleRight;
                _statusLabel.style.marginRight = 0;
                _statusLabel.Ellipsize();
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
                _versionLabel.text = $"v{UpdateChecker.CurrentState.CurrentVersion ?? PackageVersionUtility.CurrentVersion}";
        }

        public void RefreshStatus()
        {
            if (_statusLabel == null)
                return;

            if (_server?.IsRunning == true)
            {
                var attached = _server.IsAttachedToExistingTransport;
                var url = $"http://127.0.0.1:{_server.Port}/";

                var rawProfile = _settings?.MCPToolExportProfile ?? "core";
                var profileDisplay = string.IsNullOrEmpty(rawProfile) ? "Core" : char.ToUpperInvariant(rawProfile[0]) + rawProfile.Substring(1);
                var isCustom = _settings != null && _settings.IsProfileConfigured(rawProfile);
                var customTag = isCustom ? " (Custom)" : "";

                // Port already has its own field right below and 127.0.0.1 is fixed, so the
                // URL in status would just repeat it — keep it in the tooltip so this line doesn't get cut.
                _statusLabel.text = $"{(attached ? "Attached" : "Running")} · {profileDisplay}{customTag}";
                _statusLabel.tooltip = attached
                    ? $"Attached to an existing listener on {url}"
                    : $"Running on {url}";
                _statusLabel.style.color = new Color(0.4f, 1f, 0.4f);
            }
            else
            {
                _statusLabel.text = "Stopped";
                _statusLabel.tooltip = null;
                _statusLabel.style.color = new Color(0.9f, 0.35f, 0.35f);
            }
        }
    }
}
