// Copyright (C) GameWright. Licensed under MIT.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using GameWright.Editor.Settings;

namespace GameWright.Editor.MCP.Server
{
    internal sealed class GameWrightProjectSkillsPanel : IMCPWindowPanel
    {
        private readonly Dictionary<string, MCPSwitchToggle> _optionalSkillToggles = new Dictionary<string, MCPSwitchToggle>(StringComparer.OrdinalIgnoreCase);
        private readonly ISettingsController _settingsController;
        private VisualElement _root;
        private VisualElement _mainContainer;
        private Label _statusSummaryText;
        private Label _manifestPathLabel;
        private VisualElement _generatedFilesContainer;
        private MCPSwitchToggle _enableCurrentPlatformToggle;
        private string[] _platformTargets;
        private int _selectedTargetIndex;

        public GameWrightProjectSkillsPanel(ISettingsController settingsController)
        {
            _settingsController = settingsController;
        }

        public void Build(VisualElement container)
        {
            _root = container;
            BuildUI();
        }

        public void Dispose()
        {
        }

        private void BuildUI()
        {
            _root.Clear();

            var outerLayout = new VisualElement();
            outerLayout.style.flexGrow = 1;
            outerLayout.style.flexDirection = FlexDirection.Column;
            _root.Add(outerLayout);

            var scrollView = new ScrollView(ScrollViewMode.Vertical);
            scrollView.style.flexGrow = 1;
            scrollView.style.marginBottom = 8;
            outerLayout.Add(scrollView);

            _mainContainer = scrollView.contentContainer;

            _mainContainer.Add(MCPSection.PanelTitle("Project Skills"));
            _mainContainer.Add(MCPSection.PanelHint("Configure project-level skills for supported AI clients. Built-in skills are always installed. Optional skills will be added after verification."));

            BuildPlatformSection();
            BuildSkillsSection();
            BuildStatusSection();
            BuildActionsSection(outerLayout);

            RefreshStatus();
        }

        private void BuildPlatformSection()
        {
            var (section, foldout) = MCPSection.Create("Current Platform", "CurrentPlatform");

            _platformTargets = GameWrightMCPClientConfigPanel.GetAllTargetNames();
            _selectedTargetIndex = Mathf.Clamp(_selectedTargetIndex, 0, _platformTargets.Length - 1);
            var persistedTargetName = _settingsController.MCPSelectedConfigTarget;
            if (!string.IsNullOrWhiteSpace(persistedTargetName))
            {
                var persistedIndex = Array.FindIndex(_platformTargets, name => string.Equals(name, persistedTargetName, StringComparison.OrdinalIgnoreCase));
                if (persistedIndex >= 0)
                    _selectedTargetIndex = persistedIndex;
            }

            var platformDropdown = new PopupField<string>(new List<string>(_platformTargets), _selectedTargetIndex);
            platformDropdown.style.marginBottom = 6;
            platformDropdown.RegisterValueChangedCallback(evt =>
            {
                _selectedTargetIndex = Array.IndexOf(_platformTargets, evt.newValue);
                _settingsController.MCPSelectedConfigTarget = evt.newValue;
                BuildUI();
            });
            MCPDropdownStyle.Apply(platformDropdown);
            foldout.Add(platformDropdown);

            var currentPlatformId = GetCurrentSkillsPlatformId();
            var currentPlatformSupported = !string.IsNullOrEmpty(currentPlatformId);
            var manifest = ProjectSkillsManager.LoadManifest(GetProjectRootPath());

            _enableCurrentPlatformToggle = new MCPSwitchToggle("Enable skills for current platform");
            _enableCurrentPlatformToggle.SetValueWithoutNotify(
                currentPlatformSupported &&
                manifest.platforms.Contains(currentPlatformId, StringComparer.OrdinalIgnoreCase));
            _enableCurrentPlatformToggle.SetEnabled(currentPlatformSupported);
            _enableCurrentPlatformToggle.style.marginBottom = 4;
            foldout.Add(_enableCurrentPlatformToggle);

            if (!currentPlatformSupported)
            {
                foldout.Add(CreateHint("Project skills integration is not available for this platform.", new Color(1f, 0.75f, 0.45f)));
            }

            _mainContainer.Add(section);
        }

        private void BuildSkillsSection()
        {
            var manifest = ProjectSkillsManager.LoadManifest(GetProjectRootPath());
            _optionalSkillToggles.Clear();

            var (builtInSection, builtInFoldout) = MCPSection.Create("Built-in Skills", "BuiltInSkills");

            foreach (var skill in ProjectSkillsManager.GetBuiltInSkills())
            {
                builtInFoldout.Add(CreateSkillRow(skill.Title, skill.Description, $"v{skill.Version} Required"));
            }
            _mainContainer.Add(builtInSection);

            var (optionalSection, optionalFoldout) = MCPSection.Create("Optional Skills", "OptionalSkills");

            var optionalSkills = ProjectSkillsManager.GetOptionalSkills();

            if (optionalSkills.Count == 0)
            {
                var optionalHint = "No optional skills are available yet. Additional skills will be added after verification.";
                optionalFoldout.Add(CreateHint(optionalHint, new Color(0.65f, 0.65f, 0.65f)));
            }
            else
            {
                foreach (var skill in optionalSkills)
                {
                    var isEnabled = manifest.optionalSkills.Contains(skill.Id, StringComparer.OrdinalIgnoreCase);
                    var card = CreateOptionalSkillCard(skill, isEnabled);
                    optionalFoldout.Add(card);
                }

                var optionalHint = "Uncheck optional skills and click Apply Skills to remove them. Built-in skills cannot be removed.";
                optionalFoldout.Add(CreateHint(optionalHint, new Color(0.65f, 0.65f, 0.65f)));
            }

            _mainContainer.Add(optionalSection);
        }

        private void BuildStatusSection()
        {
            var (section, foldout) = MCPSection.Create("Installed Files", "InstalledFiles");

            _statusSummaryText = new Label();
            _statusSummaryText.style.fontSize = 13;
            _statusSummaryText.style.marginBottom = 4;
            foldout.Add(_statusSummaryText);

            _manifestPathLabel = new Label();
            _manifestPathLabel.style.fontSize = 11;
            _manifestPathLabel.style.color = new Color(0.5f, 0.5f, 0.5f);
            _manifestPathLabel.style.marginBottom = 6;
            _manifestPathLabel.style.whiteSpace = WhiteSpace.Normal;
            foldout.Add(_manifestPathLabel);

            _generatedFilesContainer = new VisualElement();
            foldout.Add(_generatedFilesContainer);
            _mainContainer.Add(section);
        }

        private void BuildActionsSection(VisualElement root)
        {
            var actionRow = new VisualElement();
            actionRow.style.flexDirection = FlexDirection.Row;
            actionRow.style.alignItems = Align.Center;
            actionRow.Padding(8, 10, 8, 10);
            actionRow.style.backgroundColor = new Color(0.16f, 0.16f, 0.16f);

            var applyButton = new Button(ApplyProjectSkillsConfiguration);
            applyButton.text = "Apply Skills";
            applyButton.style.height = 26;
            applyButton.style.width = 100;
            applyButton.style.backgroundColor = new Color(0.25f, 0.45f, 0.65f);
            applyButton.style.color = Color.white;
            applyButton.tooltip = "Write GameWright-managed skill files for the selected platform using the versions bundled in this package.";
            actionRow.Add(applyButton);

            root.Add(actionRow);
        }

        private void RefreshStatus()
        {
            if (_statusSummaryText == null || _manifestPathLabel == null || _generatedFilesContainer == null)
                return;

            var projectRoot = GetProjectRootPath();
            var manifest = ProjectSkillsManager.LoadManifest(projectRoot);
            var installedSkills = ProjectSkillsManager.GetInstalledSkills(manifest);
            var currentPlatformId = GetCurrentSkillsPlatformId();
            var currentPlatformDisplayName = GetCurrentSkillsPlatformDisplayName();
            var currentPlatformSupported = !string.IsNullOrEmpty(currentPlatformId);
            var currentPlatformConfigured = currentPlatformSupported &&
                                            manifest.platforms.Contains(currentPlatformId, StringComparer.OrdinalIgnoreCase);
            var manifestPath = ProjectSkillsManager.GetManifestPath(projectRoot);
            var manifestExists = File.Exists(manifestPath);
            var upgradeStatus = currentPlatformConfigured
                ? ProjectSkillsManager.GetUpgradeStatus(projectRoot, manifest, currentPlatformId)
                : null;

            if (_enableCurrentPlatformToggle != null)
            {
                _enableCurrentPlatformToggle.SetEnabled(currentPlatformSupported);
                _enableCurrentPlatformToggle.SetValueWithoutNotify(currentPlatformConfigured);
            }

            if (!currentPlatformSupported)
            {
                _statusSummaryText.text = $"Status: Unsupported current platform | Built-in: {ProjectSkillsManager.GetBuiltInSkills().Count} | Optional installed: {manifest.optionalSkills.Count}";
                _statusSummaryText.style.color = new Color(1f, 0.6f, 0.4f);
            }
            else if (!currentPlatformConfigured)
            {
                _statusSummaryText.text = $"Status: Not configured for {currentPlatformDisplayName} | Built-in: {ProjectSkillsManager.GetBuiltInSkills().Count} | Optional installed: {manifest.optionalSkills.Count}";
                _statusSummaryText.style.color = new Color(1f, 0.6f, 0.4f);
            }
            else
            {
                if (upgradeStatus != null && upgradeStatus.HasUpdates)
                {
                    _statusSummaryText.text = $"Status: Configured for {currentPlatformDisplayName} | Skills: {installedSkills.Count} | Updates available - click Apply Skills";
                    _statusSummaryText.style.color = new Color(1f, 0.72f, 0.32f);
                }
                else
                {
                    _statusSummaryText.text = $"Status: Configured for {currentPlatformDisplayName} | Skills: {installedSkills.Count} | Up to date";
                    _statusSummaryText.style.color = new Color(0.4f, 1f, 0.4f);
                }
            }

            _manifestPathLabel.text = manifestExists
                ? $"Manifest: {manifestPath}"
                : $"Manifest will be created at: {manifestPath}";

            RefreshGeneratedFiles(projectRoot, manifest, currentPlatformId, currentPlatformDisplayName, currentPlatformConfigured);
        }

        private void ApplyProjectSkillsConfiguration()
        {
            var projectRoot = GetProjectRootPath();
            var currentPlatformId = GetCurrentSkillsPlatformId();
            var selectedOptionalSkills = _optionalSkillToggles
                .Where(entry => entry.Value.value)
                .Select(entry => entry.Key)
                .ToArray();

            try
            {
                if (string.IsNullOrEmpty(currentPlatformId))
                {
                    EditorUtility.DisplayDialog(
                        "Project Skills Configuration",
                        "Project skills are not supported for the currently selected platform.",
                        "OK");
                    return;
                }

                var manifest = ProjectSkillsManager.LoadManifest(projectRoot);
                var selectedPlatforms = new HashSet<string>(manifest.platforms, StringComparer.OrdinalIgnoreCase);
                if (_enableCurrentPlatformToggle != null && _enableCurrentPlatformToggle.value)
                    selectedPlatforms.Add(currentPlatformId);
                else
                    selectedPlatforms.Remove(currentPlatformId);

                if (!ProjectSkillsManager.ConfirmOverwriteConflicts(projectRoot, selectedPlatforms))
                    return;

                ProjectSkillsManager.ApplyConfiguration(projectRoot, selectedPlatforms, selectedOptionalSkills);

                EditorUtility.DisplayDialog(
                    "Project Skills Configuration",
                    "Project skills configuration updated successfully.\n\n" +
                    $"Manifest:\n{ProjectSkillsManager.GetManifestPath(projectRoot)}",
                    "OK");

                BuildUI();
            }
            catch (Exception ex)
            {
                EditorUtility.DisplayDialog(
                    "Project Skills Configuration Error",
                    $"Configuration failed:\n{ex.Message}",
                    "OK");
            }
        }

        private string GetCurrentSkillsPlatformId()
        {
            if (_platformTargets == null || _platformTargets.Length == 0)
                return null;

            var idx = Mathf.Clamp(_selectedTargetIndex, 0, _platformTargets.Length - 1);
            return MapTargetNameToSkillsPlatformId(_platformTargets[idx]);
        }

        private string GetCurrentSkillsPlatformDisplayName()
        {
            if (_platformTargets == null || _platformTargets.Length == 0)
                return "Unknown";

            var idx = Mathf.Clamp(_selectedTargetIndex, 0, _platformTargets.Length - 1);
            return _platformTargets[idx];
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
                    return "agents";
            }
        }

        private void RefreshGeneratedFiles(
            string projectRoot,
            ProjectSkillsManager.ProjectSkillsManifest manifest,
            string currentPlatformId,
            string currentPlatformDisplayName,
            bool currentPlatformConfigured)
        {
            _generatedFilesContainer.Clear();

            if (string.IsNullOrEmpty(currentPlatformId))
            {
                _generatedFilesContainer.Add(CreateHint($"{currentPlatformDisplayName} is not supported for project skills yet.", new Color(0.6f, 0.6f, 0.6f)));
                return;
            }

            if (!currentPlatformConfigured)
            {
                _generatedFilesContainer.Add(CreateHint($"{currentPlatformDisplayName} skills are not configured yet. Enable skills for the current platform, then click Apply Skills to generate files.", new Color(0.7f, 0.7f, 0.7f)));
                return;
            }

            var upgradeStatus = ProjectSkillsManager.GetUpgradeStatus(projectRoot, manifest, currentPlatformId);
            if (upgradeStatus.Files.Count > 0)
            {
                _generatedFilesContainer.Add(CreateHint($"Versioned files for {currentPlatformDisplayName}:", new Color(0.7f, 0.7f, 0.7f)));
                foreach (var file in upgradeStatus.Files)
                {
                    var upToDate = !file.Missing && !file.Unmanaged && !file.RequiresUpgrade;
                    var row = CreateStatusRow(FormatVersionStatus(file), upToDate, upToDate ? new Color(0.55f, 0.85f, 0.55f) : new Color(1f, 0.72f, 0.32f));
                    _generatedFilesContainer.Add(row);
                }
            }

            var paths = ProjectSkillsManager.GetGeneratedPathsForPlatform(projectRoot, manifest, currentPlatformId);
            if (paths.Count == 0)
            {
                _generatedFilesContainer.Add(CreateHint($"Generated files for {currentPlatformDisplayName}: none.", new Color(0.6f, 0.6f, 0.6f)));
                return;
            }

            _generatedFilesContainer.Add(CreateHint($"Generated paths for {currentPlatformDisplayName}:", new Color(0.7f, 0.7f, 0.7f)));
            foreach (var path in paths)
            {
                var exists = File.Exists(path) || Directory.Exists(path);
                var row = CreateStatusRow(exists ? path : $"Missing  {path}", exists, exists ? new Color(0.55f, 0.85f, 0.55f) : new Color(1f, 0.65f, 0.45f));
                _generatedFilesContainer.Add(row);
            }
        }

        private static string GetProjectRootPath()
        {
            return Path.GetDirectoryName(Application.dataPath) ?? Application.dataPath;
        }

        private static string FormatVersionStatus(ProjectSkillsManager.SkillFileVersionStatus status)
        {
            if (status == null)
                return "Unknown skill file status";

            if (status.Missing)
                return $"Missing  {status.Path}  (expected {status.ExpectedVersion})";

            if (status.Unmanaged)
                return $"Conflict  {status.Path}  (not GameWright-managed, expected {status.ExpectedVersion})";

            if (status.RequiresUpgrade)
                return $"Update  {status.Path}  ({status.InstalledVersion} -> {status.ExpectedVersion})";

            return $"{status.Path}  ({status.ExpectedVersion})";
        }

        private static VisualElement CreateStatusRow(string text, bool ok, Color color)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.justifyContent = Justify.SpaceBetween;
            row.style.marginLeft = 8;
            row.style.marginBottom = 2;

            var label = new Label(text);
            label.style.fontSize = 11;
            label.style.color = color;
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.flexGrow = 1;
            row.Add(label);

            if (ok)
            {
                var check = new Label("✓");
                check.style.fontSize = 11;
                check.style.color = color;
                check.style.marginLeft = 8;
                row.Add(check);
            }

            return row;
        }

        private static Label CreateHint(string text, Color color)
        {
            var label = new Label(text);
            label.style.fontSize = 11;
            label.style.color = color;
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.marginBottom = 4;
            return label;
        }

        private static VisualElement CreateSkillRow(string title, string description, string badgeText)
        {
            var row = new VisualElement();
            row.style.backgroundColor = new Color(0.17f, 0.17f, 0.17f);
            row.Rounded(4);
            row.Border(1, new Color(0.09f, 0.09f, 0.09f));
            row.Padding(5, 7, 5, 7);
            row.style.marginBottom = 4;

            var titleRow = new VisualElement();
            titleRow.style.flexDirection = FlexDirection.Row;
            titleRow.style.alignItems = Align.Center;

            var titleLabel = new Label(title);
            titleLabel.style.flexGrow = 1;
            titleLabel.style.fontSize = 13;
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLabel.style.color = new Color(0.88f, 0.88f, 0.88f);
            titleRow.Add(titleLabel);

            var badge = new Label(badgeText);
            badge.style.fontSize = 11;
            badge.style.color = Color.white;
            badge.style.backgroundColor = new Color(0.25f, 0.45f, 0.65f);
            badge.Rounded(3);
            badge.Padding(1, 5, 1, 5);
            titleRow.Add(badge);

            row.Add(titleRow);

            var descriptionLabel = new Label(description);
            descriptionLabel.style.fontSize = 11;
            descriptionLabel.style.color = new Color(0.6f, 0.6f, 0.6f);
            descriptionLabel.style.marginTop = 3;
            descriptionLabel.style.whiteSpace = WhiteSpace.Normal;
            row.Add(descriptionLabel);

            return row;
        }

        private VisualElement CreateOptionalSkillCard(ProjectSkillsManager.SkillDefinition skill, bool isEnabled)
        {
            var row = new VisualElement();
            row.style.backgroundColor = new Color(0.17f, 0.17f, 0.17f);
            row.Rounded(4);
            row.Border(1, new Color(0.09f, 0.09f, 0.09f));
            row.Padding(5, 7, 5, 7);
            row.style.marginBottom = 4;

            var toggle = new MCPSwitchToggle(skill.Title);
            toggle.SetValueWithoutNotify(isEnabled);
            toggle.style.marginBottom = 0;
            row.Add(toggle);

            var descriptionLabel = new Label($"v{skill.Version} - {skill.Description}");
            descriptionLabel.style.fontSize = 11;
            descriptionLabel.style.color = new Color(0.58f, 0.58f, 0.58f);
            descriptionLabel.style.marginTop = 2;
            descriptionLabel.style.whiteSpace = WhiteSpace.Normal;
            row.Add(descriptionLabel);

            _optionalSkillToggles[skill.Id] = toggle;
            return row;
        }
    }
}
