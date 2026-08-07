// Copyright (C) GameWright. Licensed under MIT.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using GameWright.Editor.Settings;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace GameWright.Editor.MCP.Server
{
    internal sealed class GameWrightMCPClientConfigPanel
    {
        private readonly ISettingsController _settings;
        private readonly MCPServerService _server;
        private readonly Action _rebuildWindow;
        private MCPConfigTarget[] _targets;
        private int _selectedTargetIndex;
        private Label _configStatusLabel;
        private Label _configPathLabel;

        public GameWrightMCPClientConfigPanel(
            ISettingsController settings,
            MCPServerService server,
            Action rebuildWindow)
        {
            _settings = settings;
            _server = server;
            _rebuildWindow = rebuildWindow;
        }

        public void AddTo(VisualElement parent)
        {
            var foldout = new Foldout { text = "Client Configuration", value = true }.Persist("ClientConfig");

            var toggle = foldout.Q<Toggle>();
            var toggleLabel = toggle?.Q<Label>();
            if (toggleLabel != null)
            {
                toggleLabel.style.fontSize = 12;
                toggleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
                toggleLabel.style.color = new Color(0.55f, 0.7f, 0.9f);
                toggleLabel.style.flexGrow = 1;
            }

            _configStatusLabel = new Label();
            _configStatusLabel.style.fontSize = 13;
            _configStatusLabel.style.unityTextAlign = TextAnchor.MiddleRight;
            _configStatusLabel.style.marginRight = 0;
            if (toggle != null)
            {
                toggle.style.marginRight = 0;
                toggle.Add(_configStatusLabel);
            }

            parent.Add(foldout);
            var body = foldout;

            var subHeaderRow = new VisualElement();
            subHeaderRow.style.flexDirection = FlexDirection.Row;
            subHeaderRow.style.alignItems = Align.Center;
            subHeaderRow.style.marginBottom = 6;

            var label = new Label("One-Click MCP Configuration");
            label.style.fontSize = 13;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.color = new Color(0.75f, 0.75f, 0.75f);
            label.style.flexGrow = 1;
            subHeaderRow.Add(label);

            _configPathLabel = new Label();
            _configPathLabel.style.fontSize = 11;
            _configPathLabel.style.color = new Color(0.5f, 0.5f, 0.5f);
            _configPathLabel.style.unityTextAlign = TextAnchor.MiddleRight;
            _configPathLabel.style.flexShrink = 0;
            subHeaderRow.Add(_configPathLabel);

            body.Add(subHeaderRow);

            var homePath = GetUserHomePath();
            _targets = CreateTargets(homePath);
            var names = _targets.Select(target => target.Name).ToList();

            _selectedTargetIndex = Mathf.Clamp(_selectedTargetIndex, 0, _targets.Length - 1);
            var persistedTargetName = _settings.MCPSelectedConfigTarget;
            if (!string.IsNullOrWhiteSpace(persistedTargetName))
            {
                var persistedIndex = names.FindIndex(name =>
                    string.Equals(name, persistedTargetName, StringComparison.OrdinalIgnoreCase));
                if (persistedIndex >= 0)
                    _selectedTargetIndex = persistedIndex;
            }

            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginBottom = 4;

            var dropdown = new PopupField<string>(names, _selectedTargetIndex);
            dropdown.style.flexGrow = 1;
            dropdown.style.height = 26;
            dropdown.RegisterValueChangedCallback(evt =>
            {
                _selectedTargetIndex = names.IndexOf(evt.newValue);
                _settings.MCPSelectedConfigTarget = evt.newValue;
                _rebuildWindow?.Invoke();
            });
            MCPDropdownStyle.Apply(dropdown);
            row.Add(dropdown);

            var configureButton = new Button(() =>
            {
                ConfigureMCPForTarget(_targets[_selectedTargetIndex]);
                RefreshStatus();
            });
            configureButton.text = "Configure";
            configureButton.style.height = 26;
            configureButton.style.width = 80;
            configureButton.style.marginLeft = 4;
            configureButton.style.backgroundColor = new Color(0.2f, 0.5f, 0.3f);
            configureButton.style.color = Color.white;
            row.Add(configureButton);

            var selectedTarget = _targets[_selectedTargetIndex];
            var skillsSupported = !string.IsNullOrEmpty(MapTargetNameToSkillsPlatformId(selectedTarget.Name));
            var configureSkillsButton = new Button(() =>
            {
                ConfigureMCPAndSkillsForTarget(_targets[_selectedTargetIndex]);
                RefreshStatus();
            });
            configureSkillsButton.text = "Configure + Skills";
            configureSkillsButton.style.height = 26;
            configureSkillsButton.style.width = 130;
            configureSkillsButton.style.marginLeft = 4;
            configureSkillsButton.style.marginRight = 0;
            configureSkillsButton.style.backgroundColor = new Color(0.25f, 0.45f, 0.65f);
            configureSkillsButton.style.color = Color.white;
            configureSkillsButton.SetEnabled(skillsSupported);
            row.Add(configureSkillsButton);

            body.Add(row);

            // Deferred: the snippet build's first Newtonsoft call after a domain reload pays
            // the JIT cost (~40ms), lengthening the blank-window flash on Play.
            body.schedule.Execute(() => AddManualConfigurationSection(body, selectedTarget));

            RefreshStatus();
        }

        private void AddManualConfigurationSection(VisualElement parent, MCPConfigTarget target)
        {
            var foldout = new Foldout { text = "Manual Configuration", value = false }.Persist("ClientConfigManual");
            foldout.style.marginTop = -4;

            var toggleLabel = foldout.Q<Toggle>()?.Q<Label>();
            if (toggleLabel != null)
            {
                toggleLabel.style.fontSize = 12;
                toggleLabel.style.color = new Color(0.55f, 0.7f, 0.9f);
            }

            foldout.Add(MakeSectionLabel("Config Path:"));

            var pathRow = new VisualElement();
            pathRow.style.flexDirection = FlexDirection.Row;
            pathRow.style.alignItems = Align.Center;
            pathRow.style.marginBottom = 6;

            var pathField = new TextField { value = target.ConfigPath, isReadOnly = true };
            pathField.style.flexGrow = 1;
            pathRow.Add(pathField);

            pathRow.Add(MakeCopyButton(() => target.ConfigPath));

            var openButton = new Button(() =>
            {
                if (File.Exists(target.ConfigPath))
                    EditorUtility.RevealInFinder(target.ConfigPath);
                else
                    EditorUtility.DisplayDialog(
                        "Manual Configuration",
                        $"Config file does not exist yet:\n{target.ConfigPath}",
                        "OK");
            });
            openButton.text = "Open";
            openButton.style.height = 22;
            openButton.style.width = 50;
            openButton.style.marginLeft = 4;
            pathRow.Add(openButton);

            foldout.Add(pathRow);

            foldout.Add(MakeSectionLabel("Configuration:"));

            var snippet = BuildManualConfigSnippet(target);
            var snippetRow = new VisualElement();
            snippetRow.style.flexDirection = FlexDirection.Row;
            snippetRow.style.alignItems = Align.FlexStart;
            snippetRow.style.marginBottom = 6;

            var snippetField = new TextField { value = snippet, isReadOnly = true, multiline = true };
            snippetField.style.flexGrow = 1;
#if UNITY_2023_2_OR_NEWER
            snippetField.style.whiteSpace = WhiteSpace.Pre;
#else
            snippetField.style.whiteSpace = WhiteSpace.NoWrap;
#endif
            snippetRow.Add(snippetField);

            snippetRow.Add(MakeCopyButton(() => snippet));
            foldout.Add(snippetRow);

            foldout.Add(MakeSectionLabel("Installation Steps:"));

            var steps = new Label(
                $"1. Open {target.Name} and locate its MCP servers configuration\n" +
                $"2. Open the config file above (or create it if missing)\n" +
                "3. Merge the configuration snippet into the file, or use the Configure button above\n" +
                $"4. Restart {target.Name} if necessary");
            steps.style.fontSize = 11;
            steps.style.color = new Color(0.65f, 0.65f, 0.65f);
            steps.style.whiteSpace = WhiteSpace.Normal;
            steps.style.marginBottom = 4;
            foldout.Add(steps);

            parent.Add(foldout);
        }

        private static Label MakeSectionLabel(string text)
        {
            var label = new Label(text);
            label.style.fontSize = 11;
            label.style.color = new Color(0.75f, 0.75f, 0.75f);
            label.style.marginBottom = 2;
            return label;
        }

        private static Button MakeCopyButton(Func<string> getText)
        {
            var button = new Button();
            button.text = "Copy";
            button.style.height = 22;
            button.style.width = 60;
            button.style.marginLeft = 4;
            button.clicked += () =>
            {
                EditorGUIUtility.systemCopyBuffer = getText();
                button.text = "Copied ✓";
                button.style.color = new Color(0.4f, 1f, 0.4f);
                button.schedule.Execute(() =>
                {
                    button.text = "Copy";
                    button.style.color = StyleKeyword.Null;
                }).ExecuteLater(1500);
            };
            return button;
        }

        private string BuildManualConfigSnippet(MCPConfigTarget target)
        {
            if (target.IsToml)
                return CreateTomlSection(target);

            var rootKey = string.IsNullOrEmpty(target.RootKey) ? "mcpServers" : target.RootKey;
            var root = new Dictionary<string, object>
            {
                [rootKey] = new Dictionary<string, object> { [GetServerEntryName()] = CreateHttpEntry(target) }
            };
            if (!string.IsNullOrEmpty(target.SchemaUrl))
                root["$schema"] = target.SchemaUrl;

            return Newtonsoft.Json.JsonConvert.SerializeObject(root, Newtonsoft.Json.Formatting.Indented);
        }

        public void RefreshStatus()
        {
            if (_configStatusLabel == null || _configPathLabel == null || _targets == null)
                return;

            var idx = Mathf.Clamp(_selectedTargetIndex, 0, _targets.Length - 1);
            var target = _targets[idx];

            bool exists = File.Exists(target.ConfigPath);
            _configStatusLabel.text = exists ? "Configured ✓" : "Not configured ✕";
            _configStatusLabel.style.color = exists
                ? new Color(0.4f, 1f, 0.4f)
                : new Color(1f, 0.6f, 0.4f);
            _configPathLabel.text = target.ConfigPath;
        }

        public static string[] GetAllTargetNames()
            => GetAllTargets().Select(t => t.Name).ToArray();

        internal static MCPConfigTarget[] GetAllTargets()
            => CreateTargets(GetUserHomePath());

        private static MCPConfigTarget[] CreateTargets(string homePath)
        {
            return new[]
            {
                new MCPConfigTarget
                {
                    Name = "Claude Code",
                    ConfigPath = Path.Combine(homePath, ".claude.json"),
                    IncludeTypeField = true
                },
                new MCPConfigTarget
                {
                    Name = "Cursor",
                    ConfigPath = Path.Combine(homePath, ".cursor", "mcp.json"),
                    IncludeTypeField = true
                },
                new MCPConfigTarget
                {
                    Name = "VS Code",
                    ConfigPath = GetVSCodeConfigPath(homePath),
                    IncludeTypeField = true,
                    RootKey = "servers"
                },
                new MCPConfigTarget
                {
                    Name = "Trae",
                    ConfigPath = GetTraeConfigPath(homePath),
                    IncludeTypeField = true
                },
                new MCPConfigTarget
                {
                    Name = "Kiro",
                    ConfigPath = Path.Combine(homePath, ".kiro", "settings", "mcp.json"),
                    IncludeTypeField = true,
                    RootKey = "mcpServers"
                },
                new MCPConfigTarget
                {
                    Name = "Codex",
                    ConfigPath = Path.Combine(homePath, ".codex", "config.toml"),
                    IsToml = true,
                },
                new MCPConfigTarget
                {
                    Name = "Windsurf",
                    ConfigPath = Path.Combine(homePath, ".codeium", "windsurf", "mcp_config.json"),
                    IncludeTypeField = true,
                    HttpUrlProperty = "serverUrl"
                },
                new MCPConfigTarget
                {
                    Name = "Cline",
                    ConfigPath = GetClineConfigPath(homePath),
                    IncludeTypeField = true,
                    HttpTypeValue = "streamableHttp"
                },
                new MCPConfigTarget
                {
                    Name = "VS Code Insiders",
                    ConfigPath = GetVSCodeInsidersConfigPath(homePath),
                    IncludeTypeField = true,
                    RootKey = "servers"
                },
                new MCPConfigTarget
                {
                    Name = "Rider",
                    ConfigPath = GetRiderConfigPath(homePath),
                    IncludeTypeField = true,
                    RootKey = "servers"
                },
                new MCPConfigTarget
                {
                    Name = "Kimi Code",
                    ConfigPath = Path.Combine(homePath, ".kimi", "mcp.json"),
                    IncludeTypeField = true
                },
                new MCPConfigTarget
                {
                    Name = "Qwen Code",
                    ConfigPath = Path.Combine(homePath, ".qwen", "settings.json"),
                    IncludeTypeField = true
                },
                new MCPConfigTarget
                {
                    Name = "Gemini CLI",
                    ConfigPath = Path.Combine(homePath, ".gemini", "settings.json"),
                    IncludeTypeField = true,
                    HttpUrlProperty = "httpUrl"
                },
                new MCPConfigTarget
                {
                    // Antigravity 2.0 đọc ~/.gemini/config/; Antigravity IDE (app riêng, cùng
                    // tồn tại trên một máy) đọc ~/.gemini/antigravity-ide/ — hai client tách biệt.
                    Name = "Antigravity 2.0",
                    ConfigPath = Path.Combine(homePath, ".gemini", "config", "mcp_config.json"),
                    IncludeTypeField = true,
                    HttpUrlProperty = "serverUrl",
                    DefaultFields = new Dictionary<string, object> { ["disabled"] = false }
                },
                new MCPConfigTarget
                {
                    Name = "Antigravity IDE",
                    ConfigPath = Path.Combine(homePath, ".gemini", "antigravity-ide", "mcp_config.json"),
                    IncludeTypeField = true,
                    HttpUrlProperty = "serverUrl",
                    DefaultFields = new Dictionary<string, object> { ["disabled"] = false }
                },
                new MCPConfigTarget
                {
                    Name = "Kilo Code",
                    ConfigPath = Path.Combine(homePath, ".config", "kilo", "kilo.jsonc"),
                    IncludeTypeField = true,
                    RootKey = "mcp",
                    HttpTypeValue = "remote",
                    SchemaUrl = "https://app.kilo.ai/config.json",
                    DefaultFields = new Dictionary<string, object> { ["enabled"] = true }
                },
                new MCPConfigTarget
                {
                    Name = "OpenCode",
                    ConfigPath = Path.Combine(homePath, ".config", "opencode", "opencode.json"),
                    IncludeTypeField = true,
                    RootKey = "mcp",
                    HttpTypeValue = "remote",
                    SchemaUrl = "https://opencode.ai/config.json",
                    DefaultFields = new Dictionary<string, object> { ["enabled"] = true }
                },
                new MCPConfigTarget
                {
                    Name = "GitHub Copilot CLI",
                    ConfigPath = Path.Combine(homePath, ".copilot", "mcp-config.json"),
                    IncludeTypeField = true
                },
                new MCPConfigTarget
                {
                    Name = "CodeBuddy CLI",
                    ConfigPath = Path.Combine(homePath, ".codebuddy.json"),
                    IncludeTypeField = true
                },
                new MCPConfigTarget
                {
                    Name = "Roo Code",
                    ConfigPath = GetRooCodeConfigPath(homePath),
                    IncludeTypeField = true,
                    HttpTypeValue = "streamableHttp"
                },
            };
        }

        private void ConfigureMCPForTarget(MCPConfigTarget target)
        {
            try
            {
                WriteMCPConfigurationForTarget(target);

                var message = $"MCP configuration written to:\n{target.ConfigPath}\n\n" +
                              $"Please restart {target.Name} for it to take effect.";

                EditorUtility.DisplayDialog("MCP Configuration", message, "OK");
                _rebuildWindow?.Invoke();
            }
            catch (Exception ex)
            {
                EditorUtility.DisplayDialog(
                    "MCP Configuration Error",
                    $"Configuration failed:\n{ex.Message}",
                    "OK");
            }
        }

        private void ConfigureMCPAndSkillsForTarget(MCPConfigTarget target)
        {
            try
            {
                WriteMCPConfigurationForTarget(target);

                var platformId = MapTargetNameToSkillsPlatformId(target.Name);
                if (string.IsNullOrEmpty(platformId))
                {
                    EditorUtility.DisplayDialog(
                        "MCP Configuration",
                        $"MCP configuration written to:\n{target.ConfigPath}\n\n" +
                        "Project skills are not available for this client.",
                        "OK");

                    _rebuildWindow?.Invoke();
                    return;
                }

                if (!ConfigureProjectSkillsForPlatform(platformId))
                    return;

                var projectRoot = GetProjectRootPath();
                var manifest = ProjectSkillsManager.LoadManifest(projectRoot);
                var generatedPaths = ProjectSkillsManager.GetGeneratedPathsForPlatform(projectRoot, manifest, platformId);

                EditorUtility.DisplayDialog(
                    "MCP Configuration",
                    $"MCP configuration written to:\n{target.ConfigPath}\n\n" +
                    "Project MCP workflow skill installed:\n" +
                    string.Join("\n", generatedPaths),
                    "OK");

                _rebuildWindow?.Invoke();
            }
            catch (Exception ex)
            {
                EditorUtility.DisplayDialog(
                    "MCP Configuration Error",
                    $"Configuration failed:\n{ex.Message}",
                    "OK");
            }
        }

        private void WriteMCPConfigurationForTarget(MCPConfigTarget target)
        {
            var dir = Path.GetDirectoryName(target.ConfigPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            if (target.IsToml)
                ConfigureTomlTarget(target);
            else
                ConfigureJsonTarget(target);
        }

        private bool ConfigureProjectSkillsForPlatform(string platformId)
        {
            var projectRoot = GetProjectRootPath();
            var manifest = ProjectSkillsManager.LoadManifest(projectRoot);
            var selectedPlatforms = new HashSet<string>(manifest.platforms, StringComparer.OrdinalIgnoreCase)
            {
                platformId
            };

            var conflictPaths = ProjectSkillsManager.GetPlatformConflictPaths(projectRoot, selectedPlatforms);
            if (conflictPaths.Length > 0)
            {
                var overwrite = EditorUtility.DisplayDialog(
                    "Project Skills Configuration",
                    "Existing non-managed project instruction files were found:\n\n" +
                    string.Join("\n", conflictPaths) +
                    "\n\nOverwrite them with GameWright-managed files?",
                    "Overwrite",
                    "Cancel");

                if (!overwrite)
                    return false;
            }

            ProjectSkillsManager.ApplyConfiguration(projectRoot, selectedPlatforms, manifest.optionalSkills);
            return true;
        }

        private void ConfigureJsonTarget(MCPConfigTarget target)
        {
            var rootKey = string.IsNullOrEmpty(target.RootKey) ? "mcpServers" : target.RootKey;
            var serverName = GetServerEntryName();
            var entry = CreateHttpEntry(target);
            Dictionary<string, object> root;

            if (File.Exists(target.ConfigPath))
            {
                var existingJson = File.ReadAllText(target.ConfigPath);
                var parsed = SimpleJsonHelper.Deserialize(existingJson) as Dictionary<string, object>;

                if (parsed != null && parsed.ContainsKey(rootKey))
                {
                    root = parsed;
                    var servers = root[rootKey] as Dictionary<string, object>;
                    if (servers != null)
                        servers[serverName] = entry;
                    else
                        root[rootKey] = new Dictionary<string, object> { [serverName] = entry };
                }
                else
                {
                    root = parsed ?? new Dictionary<string, object>();
                    root[rootKey] = new Dictionary<string, object> { [serverName] = entry };
                }
            }
            else
            {
                root = new Dictionary<string, object>
                {
                    [rootKey] = new Dictionary<string, object> { [serverName] = entry }
                };
            }

            if (!string.IsNullOrEmpty(target.SchemaUrl) && !root.ContainsKey("$schema"))
                root["$schema"] = target.SchemaUrl;

            File.WriteAllText(target.ConfigPath, SimpleJsonHelper.Serialize(root));
        }

        private void ConfigureTomlTarget(MCPConfigTarget target)
        {
            var sectionHeader = "[mcp_servers." + GetServerEntryName() + "]";
            var tomlSection = CreateTomlSection(target);
            var content = File.Exists(target.ConfigPath) ? File.ReadAllText(target.ConfigPath) : string.Empty;

            if (content.Contains(sectionHeader))
            {
                var startIdx = content.IndexOf(sectionHeader, StringComparison.Ordinal);
                var afterHeader = startIdx + sectionHeader.Length;
                var nextSection = content.IndexOf("\n[", afterHeader, StringComparison.Ordinal);
                var endIdx = nextSection >= 0 ? nextSection : content.Length;
                content = content.Substring(0, startIdx) + tomlSection + content.Substring(endIdx);
            }
            else
            {
                if (content.Length > 0 && !content.EndsWith("\n"))
                    content += "\n";
                content += "\n" + tomlSection;
            }

            content = EnsureCodexRmcpFeature(content);

            File.WriteAllText(target.ConfigPath, content);
        }

        private static string EnsureCodexRmcpFeature(string content)
        {
            if (content.Contains("rmcp_client"))
                return content;

            var featuresIdx = content.IndexOf("[features]", StringComparison.Ordinal);
            if (featuresIdx >= 0)
            {
                var afterHeader = featuresIdx + "[features]".Length;
                var insertAt = content.IndexOf('\n', afterHeader);
                insertAt = insertAt >= 0 ? insertAt + 1 : content.Length;
                return content.Substring(0, insertAt) + "rmcp_client = true\n" + content.Substring(insertAt);
            }

            if (content.Length > 0 && !content.EndsWith("\n"))
                content += "\n";
            return content + "\n[features]\nrmcp_client = true\n";
        }


        private Dictionary<string, object> CreateHttpEntry(MCPConfigTarget target)
        {
            var urlProperty = string.IsNullOrEmpty(target.HttpUrlProperty) ? "url" : target.HttpUrlProperty;
            var entry = new Dictionary<string, object>
            {
                [urlProperty] = GetServerUrl()
            };

            if (target.IncludeTypeField)
                entry["type"] = string.IsNullOrEmpty(target.HttpTypeValue) ? "http" : target.HttpTypeValue;

            if (target.DefaultFields != null)
            {
                foreach (var kvp in target.DefaultFields)
                    entry[kvp.Key] = kvp.Value;
            }

            return entry;
        }

        private string CreateTomlSection(MCPConfigTarget target)
        {
            if (!target.IsToml)
                return string.Empty;

            return $"[mcp_servers.{GetServerEntryName()}]\nurl = \"{GetServerUrl()}\"\n";
        }

        // Per-project entry name so configuring from two Unity editors does not
        // overwrite each other's entry in the client's MCP config.
        internal static string GetServerEntryName()
        {
            var name = Application.productName ?? string.Empty;
            var sb = new StringBuilder("gamewright-");
            foreach (var ch in name.ToLowerInvariant())
            {
                if ((ch >= 'a' && ch <= 'z') || (ch >= '0' && ch <= '9'))
                    sb.Append(ch);
                else if (sb.Length > 0 && sb[sb.Length - 1] != '-')
                    sb.Append('-');
            }
            var result = sb.ToString().TrimEnd('-');
            return result == "gamewright" || result.Length <= "gamewright-".Length ? "gamewright" : result;
        }

        private string GetServerUrl()
        {
            var port = _server != null && _server.IsRunning
                ? _server.Port
                : _settings.MCPServerPort;
            return $"http://127.0.0.1:{port}/";
        }

        private static string GetProjectRootPath()
        {
            return Path.GetDirectoryName(Application.dataPath) ?? Application.dataPath;
        }

        private static string MapTargetNameToSkillsPlatformId(string targetName)
        {
            switch (targetName?.Trim())
            {
                case "Codex":
                    return "codex";
                case "Claude Code":
                    return "claude";
                case "Cursor":
                    return "cursor";
                default:
                    // Mọi IDE/agent khác đọc chuẩn mở .agents/skills/ (Antigravity, Windsurf, Gemini CLI...)
                    return "agents";
            }
        }

        private static string GetUserHomePath()
        {
            var homePath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (!string.IsNullOrEmpty(homePath))
                return homePath;

            var homeDrive = Environment.GetEnvironmentVariable("HOMEDRIVE");
            var homeDir = Environment.GetEnvironmentVariable("HOMEPATH");
            if (!string.IsNullOrEmpty(homeDrive) && !string.IsNullOrEmpty(homeDir))
                return homeDrive + homeDir;

            return Environment.GetFolderPath(Environment.SpecialFolder.Personal);
        }

        private static string GetTraeConfigPath(string homePath)
        {
            switch (Application.platform)
            {
                case RuntimePlatform.WindowsEditor:
                    var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                    if (!string.IsNullOrEmpty(appData))
                        return Path.Combine(appData, "Trae", "mcp.json");
                    break;

                case RuntimePlatform.OSXEditor:
                    return Path.Combine(homePath, "Library", "Application Support", "Trae", "mcp.json");

                case RuntimePlatform.LinuxEditor:
                    return Path.Combine(homePath, ".config", "Trae", "mcp.json");
            }

            return Path.Combine(homePath, ".config", "Trae", "mcp.json");
        }

        private static string GetVSCodeInsidersConfigPath(string homePath)
        {
            switch (Application.platform)
            {
                case RuntimePlatform.WindowsEditor:
                    var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                    if (!string.IsNullOrEmpty(appData))
                        return Path.Combine(appData, "Code - Insiders", "User", "mcp.json");
                    break;

                case RuntimePlatform.OSXEditor:
                    return Path.Combine(homePath, "Library", "Application Support", "Code - Insiders", "User", "mcp.json");

                case RuntimePlatform.LinuxEditor:
                    return Path.Combine(homePath, ".config", "Code - Insiders", "User", "mcp.json");
            }

            return Path.Combine(homePath, ".config", "Code - Insiders", "User", "mcp.json");
        }

        private static string GetRiderConfigPath(string homePath)
        {
            switch (Application.platform)
            {
                case RuntimePlatform.WindowsEditor:
                    var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                    if (!string.IsNullOrEmpty(localAppData))
                        return Path.Combine(localAppData, "github-copilot", "intellij", "mcp.json");
                    break;

                case RuntimePlatform.OSXEditor:
                    return Path.Combine(homePath, "Library", "Application Support", "github-copilot", "intellij", "mcp.json");

                case RuntimePlatform.LinuxEditor:
                    return Path.Combine(homePath, ".config", "github-copilot", "intellij", "mcp.json");
            }

            return Path.Combine(homePath, ".config", "github-copilot", "intellij", "mcp.json");
        }

        private static string GetClineConfigPath(string homePath)
        {
            const string tail = "globalStorage/saoudrizwan.claude-dev/settings/cline_mcp_settings.json";
            var segments = tail.Split('/');

            switch (Application.platform)
            {
                case RuntimePlatform.WindowsEditor:
                    var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                    if (!string.IsNullOrEmpty(appData))
                        return CombineAll(appData, "Code", "User", segments);
                    break;

                case RuntimePlatform.OSXEditor:
                    return CombineAll(Path.Combine(homePath, "Library", "Application Support"), "Code", "User", segments);

                case RuntimePlatform.LinuxEditor:
                    return CombineAll(Path.Combine(homePath, ".config"), "Code", "User", segments);
            }

            return CombineAll(Path.Combine(homePath, ".config"), "Code", "User", segments);
        }

        private static string GetRooCodeConfigPath(string homePath)
        {
            const string tail = "globalStorage/rooveterinaryinc.roo-cline/settings/cline_mcp_settings.json";
            var segments = tail.Split('/');

            switch (Application.platform)
            {
                case RuntimePlatform.WindowsEditor:
                    var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                    if (!string.IsNullOrEmpty(appData))
                        return CombineAll(appData, "Code", "User", segments);
                    break;

                case RuntimePlatform.OSXEditor:
                    return CombineAll(Path.Combine(homePath, "Library", "Application Support"), "Code", "User", segments);

                case RuntimePlatform.LinuxEditor:
                    return CombineAll(Path.Combine(homePath, ".config"), "Code", "User", segments);
            }

            return CombineAll(Path.Combine(homePath, ".config"), "Code", "User", segments);
        }

        private static string CombineAll(string root, string app, string user, string[] tail)
        {
            var parts = new List<string> { root, app, user };
            parts.AddRange(tail);
            return Path.Combine(parts.ToArray());
        }

        private static string GetVSCodeConfigPath(string homePath)
        {
            switch (Application.platform)
            {
                case RuntimePlatform.WindowsEditor:
                    var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                    if (!string.IsNullOrEmpty(appData))
                        return Path.Combine(appData, "Code", "User", "mcp.json");
                    break;

                case RuntimePlatform.OSXEditor:
                    var macPrimaryPath = Path.Combine(homePath, "Library", "Application Support", "Code", "User", "mcp.json");
                    var macPrimaryDirectory = Path.GetDirectoryName(macPrimaryPath);
                    if (File.Exists(macPrimaryPath) ||
                        (!string.IsNullOrEmpty(macPrimaryDirectory) && Directory.Exists(macPrimaryDirectory)))
                    {
                        return macPrimaryPath;
                    }

                    return Path.Combine(homePath, ".vscode", "mcp.json");

                case RuntimePlatform.LinuxEditor:
                    return Path.Combine(homePath, ".config", "Code", "User", "mcp.json");
            }

            return Path.Combine(homePath, ".vscode", "mcp.json");
        }

        internal struct MCPConfigTarget
        {
            public string Name;
            public string ConfigPath;
            public string RootKey;
            public bool IsToml;
            public bool IncludeTypeField;
            public string HttpTypeValue;
            public string HttpUrlProperty;
            public string SchemaUrl;
            public Dictionary<string, object> DefaultFields;
        }
    }
}
