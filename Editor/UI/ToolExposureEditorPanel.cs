// Copyright (C) KitWright. Licensed under MIT.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using KitWright.Editor.Settings;
using KitWright.Editor.Tools;

namespace KitWright.Editor.MCP.Server
{
    internal sealed class ToolExposureEditorPanel : IMCPWindowPanel
    {
        private static readonly List<string> ProfileChoices = new List<string>(MCPToolExportPolicy.AllProfiles);

        private readonly ISettingsController _settingsController;
        private readonly MCPServerService _mcpServer;
        private readonly Dictionary<string, Button> _segmentButtons = new Dictionary<string, Button>(StringComparer.OrdinalIgnoreCase);
        private VisualElement _root;
        private Label _statusLabel;
        private Label _descriptionLabel;
        private Label _unsavedLabel;
        private Button _saveButton;
        private ScrollView _toolScrollView;
        private List<string> _allToolNames = new List<string>();
        private readonly Dictionary<string, string> _toolCategories = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _toolDescriptions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly List<Foldout> _categoryFoldouts = new List<Foldout>();
        private string _searchFilter = string.Empty;
        private HashSet<string> _editingTools = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private string _editingProfile = "core";

        private static readonly Color SegmentActive = new Color(0.24f, 0.42f, 0.58f);
        private static readonly Color SegmentInactive = new Color(0.20f, 0.20f, 0.21f);
        private static readonly Color SwitchOnTrack = new Color(0.30f, 0.66f, 0.36f);
        private static readonly Color SwitchOffTrack = new Color(0.62f, 0.26f, 0.26f);
        private static readonly Color RowOnBg = new Color(0.17f, 0.21f, 0.17f);
        private static readonly Color RowOffBg = new Color(0.165f, 0.165f, 0.17f);
        private static readonly Color RowOnText = new Color(0.92f, 0.95f, 0.90f);
        private static readonly Color RowOffText = new Color(0.72f, 0.72f, 0.72f);

        public ToolExposureEditorPanel(ISettingsController settingsController, MCPServerService mcpServer)
        {
            _settingsController = settingsController;
            _mcpServer = mcpServer;
        }

        public void Build(VisualElement container)
        {
            _root = container;
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
            _root.Clear();

            _root.Add(MCPSection.PanelTitle("Tool Exposure"));
            _root.Add(MCPSection.PanelHint("Edit exactly which tools each MCP profile exposes. Choose the active profile from the Server tab. Saving changes restarts the running server automatically."));

            LoadAllTools();

            var activeProfile = GetActiveProfile();
            _editingProfile = ProfileChoices.Contains(activeProfile) ? activeProfile : "core";

            var controlsSection = new VisualElement();
            controlsSection.style.backgroundColor = new Color(0.165f, 0.165f, 0.17f);
            controlsSection.Rounded(6).Border(1, new Color(0.10f, 0.10f, 0.10f)).Padding(6, 8, 6, 8);
            controlsSection.style.marginBottom = 10;

            var foldout = new Foldout { value = true }.Persist("EditToolList");
            foldout.style.marginBottom = 0;

            var headerRow = new VisualElement();
            headerRow.style.flexDirection = FlexDirection.Row;
            headerRow.style.alignItems = Align.Center;
            headerRow.style.flexGrow = 1;

            headerRow.Add(SectionTitle("Edit Tool List"));

            AttachHeader(foldout, headerRow);

            var body = new VisualElement();
            body.style.marginTop = 4;

            var segmented = BuildSegmentedProfile(_editingProfile);
            segmented.style.marginBottom = 8;
            body.Add(segmented);

            var buttonRow = new VisualElement();
            buttonRow.style.flexDirection = FlexDirection.Row;
            buttonRow.style.alignItems = Align.Center;
            buttonRow.style.marginBottom = 10;

            var selectAllButton = CreateActionButton("Select All", SelectAllTools, 88, new Color(0.24f, 0.42f, 0.58f));
            selectAllButton.style.marginLeft = 0;
            buttonRow.Add(selectAllButton);

            var clearButton = CreateActionButton("Clear", ClearTools, 64, new Color(0.46f, 0.36f, 0.24f));
            clearButton.style.marginLeft = 6;
            buttonRow.Add(clearButton);

            var defaultButton = CreateActionButton("Restore Default", UseDefaultTools, 106, new Color(0.34f, 0.34f, 0.34f));
            defaultButton.style.marginLeft = 6;
            buttonRow.Add(defaultButton);

            _saveButton = CreateActionButton("Save", SaveEditingTools, 64, new Color(0.2f, 0.5f, 0.3f));
            _saveButton.style.marginLeft = 6;
            buttonRow.Add(_saveButton);

            body.Add(buttonRow);

            _statusLabel = new Label();
            _statusLabel.style.fontSize = 13;
            _statusLabel.style.marginBottom = 4;
            body.Add(_statusLabel);

            _unsavedLabel = new Label();
            _unsavedLabel.style.fontSize = 12;
            _unsavedLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _unsavedLabel.style.marginBottom = 6;
            _unsavedLabel.style.display = DisplayStyle.None;
            body.Add(_unsavedLabel);

            _descriptionLabel = new Label();
            _descriptionLabel.style.fontSize = 13;
            _descriptionLabel.style.color = new Color(0.68f, 0.68f, 0.68f);
            _descriptionLabel.style.whiteSpace = WhiteSpace.Normal;
            body.Add(_descriptionLabel);

            foldout.Add(body);
            foldout.contentContainer.style.marginRight = 11;
            controlsSection.Add(foldout);
            _root.Add(controlsSection);


            var toolsSection = new VisualElement().Card();
            toolsSection.style.flexGrow = 1;

            var toolsFoldout = new Foldout { value = true }.Persist("AvailableTools");
            toolsFoldout.style.marginBottom = 0;
            toolsFoldout.style.flexGrow = 1;

            var toolsHeaderRow = new VisualElement();
            toolsHeaderRow.style.flexDirection = FlexDirection.Row;
            toolsHeaderRow.style.alignItems = Align.Center;
            toolsHeaderRow.style.flexGrow = 1;

            toolsHeaderRow.Add(SectionTitle("Available Tools"));

            AttachHeader(toolsFoldout, toolsHeaderRow);

            var toolsBody = new VisualElement();
            toolsBody.style.marginTop = 4;
            toolsBody.style.flexGrow = 1;

            var searchContainer = new VisualElement();
            searchContainer.style.flexDirection = FlexDirection.Row;
            searchContainer.style.alignItems = Align.Center;
            searchContainer.style.backgroundColor = new Color(0.11f, 0.11f, 0.12f);
            searchContainer.Rounded(6);
            searchContainer.Border(1, new Color(0.20f, 0.20f, 0.22f));
            searchContainer.Padding(0, 8, 0, 8);
            searchContainer.style.height = 28;
            searchContainer.style.marginTop = 4;
            searchContainer.style.marginBottom = 6;
            searchContainer.style.transitionProperty = new System.Collections.Generic.List<StylePropertyName> { "border-color" };
            searchContainer.style.transitionDuration = new System.Collections.Generic.List<TimeValue> { new TimeValue(0.15f, TimeUnit.Second) };

            var searchIcon = new Image();
            var searchTex = EditorGUIUtility.IconContent("d_Search Icon")?.image as Texture2D;
            if (searchTex != null)
            {
                searchIcon.image = searchTex;
                searchIcon.scaleMode = ScaleMode.ScaleToFit;
            }
            searchIcon.style.width = 14;
            searchIcon.style.height = 14;
            searchIcon.style.marginRight = 4;
            searchIcon.style.flexShrink = 0;
            searchIcon.style.opacity = 0.5f;
            searchContainer.Add(searchIcon);

            var searchField = new TextField();
            searchField.value = _searchFilter;
            searchField.style.flexGrow = 1;
            searchField.style.fontSize = 12;
            searchField.style.color = new Color(0.85f, 0.85f, 0.88f);
            searchField.style.marginTop = 0;
            searchField.style.marginBottom = 0;
            var searchInput = searchField.Q<VisualElement>(className: "unity-text-field__input");
            if (searchInput != null)
            {
                searchInput.style.backgroundColor = Color.clear;
                searchInput.style.borderTopWidth = searchInput.style.borderBottomWidth =
                    searchInput.style.borderLeftWidth = searchInput.style.borderRightWidth = 0;
                searchInput.style.paddingLeft = 0;
                searchInput.style.paddingTop = 0;
                searchInput.style.paddingBottom = 0;
            }
            searchContainer.Add(searchField);

            var placeholder = new Label("Search tools or categories...");
            placeholder.style.position = Position.Absolute;
            placeholder.style.left = 30;
            placeholder.style.top = 0;
            placeholder.style.bottom = 0;
            placeholder.style.unityTextAlign = TextAnchor.MiddleLeft;
            placeholder.style.fontSize = 12;
            placeholder.style.color = new Color(0.40f, 0.40f, 0.43f);
            placeholder.style.unityFontStyleAndWeight = FontStyle.Italic;
            placeholder.pickingMode = PickingMode.Ignore;
            placeholder.style.display = string.IsNullOrEmpty(_searchFilter) ? DisplayStyle.Flex : DisplayStyle.None;
            searchContainer.Add(placeholder);

            var clearBtn = new Label("✕");
            clearBtn.style.fontSize = 12;
            clearBtn.style.color = new Color(0.50f, 0.50f, 0.52f);
            clearBtn.style.unityTextAlign = TextAnchor.MiddleCenter;
            clearBtn.style.flexShrink = 0;
            clearBtn.style.width = 18;
            clearBtn.style.height = 18;
            clearBtn.Rounded(9);
            clearBtn.style.display = string.IsNullOrEmpty(_searchFilter) ? DisplayStyle.None : DisplayStyle.Flex;
            clearBtn.RegisterCallback<ClickEvent>(_ =>
            {
                searchField.value = string.Empty;
            });
            clearBtn.RegisterCallback<MouseEnterEvent>(_ => clearBtn.style.color = new Color(0.85f, 0.85f, 0.88f));
            clearBtn.RegisterCallback<MouseLeaveEvent>(_ => clearBtn.style.color = new Color(0.50f, 0.50f, 0.52f));
            searchContainer.Add(clearBtn);

            var focusBorder = new Color(0.30f, 0.55f, 0.85f);
            var normalBorder = new Color(0.20f, 0.20f, 0.22f);
            searchField.RegisterCallback<FocusInEvent>(_ =>
            {
                searchContainer.style.borderTopColor = searchContainer.style.borderBottomColor =
                    searchContainer.style.borderLeftColor = searchContainer.style.borderRightColor = focusBorder;
                searchIcon.style.opacity = 0.85f;
            });
            searchField.RegisterCallback<FocusOutEvent>(_ =>
            {
                searchContainer.style.borderTopColor = searchContainer.style.borderBottomColor =
                    searchContainer.style.borderLeftColor = searchContainer.style.borderRightColor = normalBorder;
                searchIcon.style.opacity = 0.5f;
            });

            searchField.RegisterValueChangedCallback(evt =>
            {
                _searchFilter = evt.newValue ?? string.Empty;
                placeholder.style.display = string.IsNullOrEmpty(_searchFilter) ? DisplayStyle.Flex : DisplayStyle.None;
                clearBtn.style.display = string.IsNullOrEmpty(_searchFilter) ? DisplayStyle.None : DisplayStyle.Flex;
                RebuildToolList();
            });
            toolsBody.Add(searchContainer);

            _toolScrollView = new ScrollView(ScrollViewMode.Vertical);
            _toolScrollView.style.flexGrow = 1;
            _toolScrollView.style.backgroundColor = new Color(0.14f, 0.14f, 0.14f);
            _toolScrollView.Rounded(4).Padding(5, 6, 5, 6);
            toolsBody.Add(_toolScrollView);

            toolsFoldout.Add(toolsBody);
            toolsFoldout.contentContainer.style.marginRight = 11;
            toolsFoldout.contentContainer.style.flexGrow = 1;
            toolsSection.Add(toolsFoldout);
            _root.Add(toolsSection);

            LoadEditingTools();
            RebuildToolList();
            RefreshStatus();
        }

        private static Label SectionTitle(string text)
        {
            var label = new Label(text);
            label.style.fontSize = 13;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.color = new Color(0.88f, 0.88f, 0.9f);
            label.style.unityTextAlign = TextAnchor.MiddleLeft;
            return label;
        }

        // Moves headerRow onto the foldout's own toggle row so it reads as a header. The toggle's
        // input holds the fold arrow and defaults to flexGrow:1, which would push the header into
        // the right half — pin it so the arrow hugs the left and the header fills the rest.
        private static void AttachHeader(Foldout foldout, VisualElement headerRow)
        {
            var toggle = foldout.Q<Toggle>();
            if (toggle == null)
            {
                foldout.Add(headerRow);
                return;
            }

            var toggleLabel = toggle.Q<Label>();
            if (toggleLabel != null)
                toggleLabel.style.display = DisplayStyle.None;

            var toggleInput = toggle.Q(className: "unity-toggle__input");
            if (toggleInput != null)
            {
                toggleInput.style.flexGrow = 0;
                toggleInput.style.flexShrink = 0;
            }

            toggle.style.marginBottom = 2;
            toggle.Add(headerRow);
        }

        private Button CreateActionButton(string text, Action action, int width, Color color)
        {
            var button = new Button(action);
            button.text = text;
            button.style.height = 26;
            button.style.minWidth = width;
            button.style.paddingLeft = 8;
            button.style.paddingRight = 8;
            button.style.marginTop = 0;
            button.style.marginBottom = 0;
            button.style.whiteSpace = WhiteSpace.NoWrap;
            button.style.unityTextAlign = TextAnchor.MiddleCenter;
            button.style.backgroundColor = color;
            button.style.color = Color.white;
            return button;
        }

        private VisualElement BuildSegmentedProfile(string currentProfile)
        {
            var group = new VisualElement();
            group.style.flexDirection = FlexDirection.Row;
            group.style.height = 22;
            group.style.width = new Length(100, LengthUnit.Percent);
            group.style.overflow = Overflow.Hidden;
            group.Rounded(5).Border(1, new Color(0.08f, 0.08f, 0.08f));

            _segmentButtons.Clear();
            for (var i = 0; i < ProfileChoices.Count; i++)
            {
                var profile = ProfileChoices[i];
                var button = new Button(() => SelectProfile(profile))
                {
                    text = char.ToUpperInvariant(profile[0]) + profile.Substring(1)
                };
                button.AddToClassList(MCPWindow.FlatButtonClass);
                button.style.flexGrow = 1;
                button.style.flexBasis = 0;
                button.style.height = 20;
                button.style.marginTop = 0;
                button.style.marginBottom = 0;
                button.style.marginLeft = 0;
                button.style.marginRight = 0;
                button.Rounded(0);
                button.style.borderLeftWidth = i == 0 ? 0 : 1;
                button.style.borderRightWidth = 0;
                button.style.borderTopWidth = 0;
                button.style.borderBottomWidth = 0;
                button.style.borderLeftColor = new Color(0.08f, 0.08f, 0.08f);
                button.style.fontSize = 12;
                button.style.transitionProperty = new List<StylePropertyName> { "background-color" };
                button.style.transitionDuration = new List<TimeValue> { new TimeValue(0.1f, TimeUnit.Second) };
                _segmentButtons[profile] = button;
                group.Add(button);
            }

            UpdateSegmentStyles(currentProfile);
            return group;
        }

        private void SelectProfile(string profile)
        {
            _editingProfile = profile;
            _settingsController.MCPToolExportProfile = profile;
            UpdateSegmentStyles(profile);
            LoadEditingTools();
            RebuildToolList();
            RefreshStatus();
        }

        private void UpdateSegmentStyles(string activeProfile)
        {
            foreach (var entry in _segmentButtons)
            {
                var profile = entry.Key;
                var isActive = string.Equals(profile, activeProfile, StringComparison.OrdinalIgnoreCase);
                var isCustom = _settingsController != null && _settingsController.IsProfileConfigured(profile);

                var labelName = char.ToUpperInvariant(profile[0]) + profile.Substring(1);
                entry.Value.text = isCustom ? $"{labelName} •" : labelName;

                entry.Value.style.backgroundColor = isActive ? SegmentActive : SegmentInactive;
                entry.Value.style.color = isActive
                    ? (isCustom ? new Color(1f, 0.9f, 0.55f) : Color.white)
                    : (isCustom ? new Color(0.95f, 0.8f, 0.45f) : new Color(0.7f, 0.7f, 0.7f));
                entry.Value.style.unityFontStyleAndWeight = isActive ? FontStyle.Bold : FontStyle.Normal;
                entry.Value.tooltip = isCustom
                    ? $"{labelName} profile (customized list)"
                    : $"{labelName} profile (default list)";
            }
        }

        private void LoadAllTools()
        {
            _toolCategories.Clear();
            _toolDescriptions.Clear();

            var definitions = ToolSchemaBuilder.BuildAll();
            foreach (var def in definitions)
            {
                var name = def.function?.name;
                if (string.IsNullOrWhiteSpace(name))
                    continue;

                if (!_toolDescriptions.ContainsKey(name) && !string.IsNullOrWhiteSpace(def.function.description))
                    _toolDescriptions[name] = def.function.description.Trim();
            }

            _allToolNames = definitions
                .Select(tool => tool.function.name)
                .Where(name => !string.IsNullOrWhiteSpace(name) && !MCPToolExportPolicy.IsHiddenFromExposurePanel(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var toolName in _allToolNames)
                _toolCategories[toolName] = GetToolCategory(toolName);
        }

        private void LoadEditingTools()
        {
            _editingTools = new HashSet<string>(GetEffectiveTools(_editingProfile), StringComparer.OrdinalIgnoreCase);
        }

        private IEnumerable<string> GetEffectiveTools(string profile)
        {
            if (_settingsController.IsProfileConfigured(profile))
                return _settingsController.GetProfileTools(profile);

            return MCPToolExportPolicy.DefaultToolsFor(MCPToolExportPolicy.Parse(profile), _allToolNames);
        }

        private bool IsEditingProfileConfigured()
        {
            return _settingsController.IsProfileConfigured(_editingProfile);
        }

        private string GetActiveProfile()
        {
            var currentProfile = MCPToolExportPolicy.ToSettingValue(
                MCPToolExportPolicy.Parse(_settingsController.MCPToolExportProfile));
            return ProfileChoices.Contains(currentProfile) ? currentProfile : "core";
        }

        private void RebuildToolList()
        {
            if (_toolScrollView == null)
                return;

            _toolScrollView.contentContainer.Clear();
            _categoryFoldouts.Clear();

            var filter = (_searchFilter ?? string.Empty).Trim();
            var hasFilter = filter.Length > 0;

            var groupedTools = _allToolNames
                .GroupBy(GetCachedToolCategory)
                .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase);

            foreach (var group in groupedTools)
            {
                var categoryMatch = hasFilter && group.Key.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;
                var categoryTools = group
                    .Where(name => !hasFilter || categoryMatch || name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
                    .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                    .ToList();
                if (categoryTools.Count == 0) continue;
                var section = CreateCategorySection(group.Key, categoryTools);
                if (hasFilter)
                {
                    var sectionFoldout = section.Q<Foldout>();
                    if (sectionFoldout != null) sectionFoldout.value = true;
                }
                _toolScrollView.Add(section);
            }
        }

        private VisualElement CreateCategorySection(string category, IReadOnlyList<string> categoryTools)
        {
            var selectedCount = categoryTools.Count(tool => _editingTools.Contains(tool));
            var allOn = selectedCount == categoryTools.Count && categoryTools.Count > 0;
            var noneOn = selectedCount == 0;

            var card = new VisualElement();
            card.style.backgroundColor = new Color(0.165f, 0.165f, 0.17f);
            card.Rounded(6).Border(1, new Color(0.10f, 0.10f, 0.10f)).Padding(6, 8, 6, 8);
            card.style.marginBottom = 6;

            var foldout = new Foldout { value = true }.Persist("Category." + category);
            foldout.style.marginBottom = 0;
            _categoryFoldouts.Add(foldout);

            // Style the foldout toggle row: category name on the left, a count badge and quick
            // Select/Clear actions on the right, all on the toggle's own row so it reads as a header.
            var headerRow = new VisualElement();
            headerRow.style.flexDirection = FlexDirection.Row;
            headerRow.style.alignItems = Align.Center;
            headerRow.style.flexGrow = 1;

            var nameLabel = SectionTitle(HighlightMatch(category, _searchFilter));
            nameLabel.enableRichText = true;
            nameLabel.style.flexShrink = 0;
            headerRow.Add(nameLabel);

            var spacer = new VisualElement();
            spacer.style.flexGrow = 1;
            headerRow.Add(spacer);

            var countBadge = new Label($"{selectedCount}/{categoryTools.Count}");
            countBadge.style.fontSize = 11;
            countBadge.style.unityFontStyleAndWeight = FontStyle.Bold;
            countBadge.style.color = allOn ? new Color(0.6f, 0.9f, 0.6f)
                : noneOn ? new Color(0.6f, 0.6f, 0.62f)
                : new Color(0.95f, 0.8f, 0.45f);
            countBadge.style.marginRight = 8;
            headerRow.Add(countBadge);

            var selectButton = CreateCategoryButton("All", () => SetCategoryTools(categoryTools, true));
            headerRow.Add(selectButton);

            var clearButton = CreateCategoryButton("None", () => SetCategoryTools(categoryTools, false));
            clearButton.style.marginLeft = 4;
            headerRow.Add(clearButton);

            AttachHeader(foldout, headerRow);

            var body = new VisualElement();
            body.style.marginTop = 4;
            foreach (var toolName in categoryTools)
                body.Add(CreateToolRow(toolName));
            foldout.Add(body);

            card.Add(foldout);
            return card;
        }

        private VisualElement CreateToolRow(string toolName)
        {
            var isOn = _editingTools.Contains(toolName);

            var baseBg = isOn ? RowOnBg : RowOffBg;
            var hoverBg = isOn ? new Color(0.20f, 0.25f, 0.20f) : new Color(0.205f, 0.205f, 0.215f);

            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginBottom = 3;
            row.Padding(5, 6, 5, 8);
            row.style.backgroundColor = baseBg;
            row.Rounded(5);
            row.style.transitionProperty = new List<StylePropertyName> { "background-color" };
            row.style.transitionDuration = new List<TimeValue> { new TimeValue(0.1f, TimeUnit.Second) };
            row.RegisterCallback<MouseEnterEvent>(_ => row.style.backgroundColor = hoverBg);
            row.RegisterCallback<MouseLeaveEvent>(_ => row.style.backgroundColor = baseBg);

            var hasDescription = _toolDescriptions.TryGetValue(toolName, out var description)
                                 && !string.IsNullOrWhiteSpace(description);

            var label = new Label(HighlightMatch(toolName, _searchFilter));
            label.enableRichText = true;
            label.style.flexShrink = 0;
            label.style.fontSize = 13;
            label.style.color = isOn ? RowOnText : RowOffText;
            if (!hasDescription)
                label.style.flexGrow = 1;
            row.Add(label);

            if (hasDescription)
            {
                var descLabel = new Label(description);
                descLabel.Ellipsize();
                descLabel.style.flexGrow = 1;
                descLabel.style.marginLeft = 10;
                descLabel.style.fontSize = 11;
                descLabel.style.color = new Color(0.55f, 0.55f, 0.58f);
                descLabel.style.whiteSpace = WhiteSpace.NoWrap;
                descLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
                row.Add(descLabel);
            }

            row.tooltip = hasDescription ? description : toolName;

            row.Add(CreateSwitch(isOn));

            row.RegisterCallback<ClickEvent>(_ =>
            {
                if (_editingTools.Contains(toolName))
                    _editingTools.Remove(toolName);
                else
                    _editingTools.Add(toolName);

                RebuildToolList();
                RefreshStatus();
            });

            return row;
        }

        private static string HighlightMatch(string text, string filter)
        {
            if (string.IsNullOrEmpty(filter) || string.IsNullOrEmpty(text))
                return text;

            int idx = text.IndexOf(filter, StringComparison.OrdinalIgnoreCase);
            if (idx < 0)
                return text;

            var before = text.Substring(0, idx);
            var match = text.Substring(idx, filter.Length);
            var after = text.Substring(idx + filter.Length);
            return $"{before}<color=#FFD54F><b>{match}</b></color>{after}";
        }

        private static VisualElement CreateSwitch(bool isOn)
        {
            var track = new VisualElement();
            track.style.width = 34;
            track.style.height = 18;
            track.style.flexShrink = 0;
            track.style.backgroundColor = isOn ? SwitchOnTrack : SwitchOffTrack;
            track.Rounded(9);
            track.style.justifyContent = Justify.Center;

            var knob = new VisualElement();
            knob.style.position = Position.Absolute;
            knob.style.width = 14;
            knob.style.height = 14;
            knob.style.top = 2;
            knob.style.left = isOn ? 18 : 2;
            knob.style.backgroundColor = Color.white;
            knob.Rounded(7);
            knob.style.transitionProperty = new List<StylePropertyName> { "left" };
            knob.style.transitionDuration = new List<TimeValue> { new TimeValue(0.1f, TimeUnit.Second) };
            knob.style.transitionTimingFunction = new List<EasingFunction> { new EasingFunction(EasingMode.EaseOutCubic) };
            track.Add(knob);

            return track;
        }

        private Button CreateCategoryButton(string text, Action action)
        {
            var button = new Button { text = text };
            button.style.height = 18;
            button.style.width = 46;
            button.style.fontSize = 10;
            button.style.marginTop = 0;
            button.style.marginBottom = 0;
            button.style.paddingLeft = 0;
            button.style.paddingRight = 0;
            button.style.unityTextAlign = TextAnchor.MiddleCenter;
            button.style.backgroundColor = new Color(0.24f, 0.24f, 0.26f);
            button.style.color = new Color(0.85f, 0.85f, 0.85f);
            // Sits inside the foldout toggle: stop the click from also toggling the foldout.
            button.RegisterCallback<ClickEvent>(evt =>
            {
                evt.StopPropagation();
                action();
            });
            return button;
        }

        private void SetCategoryTools(IEnumerable<string> categoryTools, bool enabled)
        {
            foreach (var toolName in categoryTools)
            {
                if (enabled)
                    _editingTools.Add(toolName);
                else
                    _editingTools.Remove(toolName);
            }

            RebuildToolList();
            RefreshStatus();
        }

        private void SetAllToolToggles(bool enabled)
        {
            if (enabled)
                _editingTools = new HashSet<string>(_allToolNames, StringComparer.OrdinalIgnoreCase);
            else
                _editingTools.Clear();

            RebuildToolList();
            RefreshStatus();
        }

        private void SelectAllTools()
        {
            SetAllToolToggles(true);
        }

        private void ClearTools()
        {
            SetAllToolToggles(false);
        }

        private void UseDefaultTools()
        {
            _settingsController.SetProfileTools(_editingProfile, null);

            LoadEditingTools();
            RebuildToolList();
            RefreshStatus();
        }

        private void SaveEditingTools()
        {
            var selected = _editingTools
                .Where(tool => _allToolNames.Contains(tool, StringComparer.OrdinalIgnoreCase))
                .OrderBy(tool => tool, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            _settingsController.SetProfileTools(_editingProfile, selected);

            RefreshStatus();
        }

        private void RefreshStatus()
        {
            if (_settingsController == null)
                return;

            var activeProfile = GetActiveProfile();
            UpdateSegmentStyles(_editingProfile);

            var selectedCount = _editingTools?.Count ?? 0;
            var totalCount = _allToolNames?.Count ?? 0;
            var source = IsEditingProfileConfigured() ? "Custom" : "Default";
            var dirty = HasUnsavedChanges();

            if (_statusLabel != null)
            {
                var displayProfile = char.ToUpperInvariant(_editingProfile[0]) + _editingProfile.Substring(1);
                var displayActive = char.ToUpperInvariant(activeProfile[0]) + activeProfile.Substring(1);
                _statusLabel.text = $"Active: {displayActive} | Editing {displayProfile}: {selectedCount}/{totalCount} tools ({source})";
                _statusLabel.style.color = string.Equals(activeProfile, "full", StringComparison.OrdinalIgnoreCase)
                    ? new Color(0.55f, 0.75f, 1f)
                    : new Color(0.55f, 0.85f, 0.55f);
            }

            if (_unsavedLabel != null)
            {
                if (dirty)
                {
                    _unsavedLabel.text = "⚠ You have unsaved changes. Click Save to apply.";
                    _unsavedLabel.style.color = new Color(1f, 0.75f, 0.3f);
                    _unsavedLabel.style.display = DisplayStyle.Flex;
                }
                else
                {
                    _unsavedLabel.style.display = DisplayStyle.None;
                }
            }

            if (_saveButton != null)
            {
                _saveButton.SetEnabled(dirty);
                _saveButton.style.backgroundColor = dirty
                    ? new Color(0.2f, 0.55f, 0.3f)
                    : new Color(0.25f, 0.25f, 0.27f);
                _saveButton.style.color = dirty ? Color.white : new Color(0.55f, 0.55f, 0.55f);
            }

            if (_descriptionLabel != null)
                _descriptionLabel.text = DescribeProfile(_editingProfile);
        }

        private bool HasUnsavedChanges()
        {
            var saved = new HashSet<string>(GetEffectiveTools(_editingProfile), StringComparer.OrdinalIgnoreCase);
            if (_editingTools.Count != saved.Count)
                return true;
            return !_editingTools.SetEquals(saved);
        }

        private static string DescribeProfile(string profile)
        {
            switch (profile?.ToLowerInvariant())
            {
                case "minimal":
                    return "Minimal defaults to a tiny essential set (execute_code + a few reads). Select tools below and click Save to override that list.";
                case "extended":
                    return "Extended defaults to every tool except niche families (addressables, terrain, docs, reflection, assembly, snapshots). Select tools below and click Save to override.";
                case "full":
                    return "Full defaults to every registered MCP tool. Select tools below and click Save to make full expose only that custom list.";
                default:
                    return "Core defaults to the focused Unity workflow tool set. Select tools below and click Save to override that list.";
            }
        }

        private string GetCachedToolCategory(string toolName)
        {
            return _toolCategories.TryGetValue(toolName, out var category) ? category : "Other";
        }

        private string GetToolCategory(string toolName)
        {
            if (ToolRegistry.MethodCache.TryGetValue(toolName, out var method))
            {
                var provider = method.DeclaringType?.GetCustomAttribute<ToolProviderAttribute>();
                return FormatCategory(provider?.Category ?? method.DeclaringType?.Name ?? "Other");
            }

            if (ToolRegistry.ManualTools.ContainsKey(toolName))
                return "Manual";

            return "Other";
        }

        private static string FormatCategory(string category)
        {
            if (string.IsNullOrWhiteSpace(category))
                return "Other";

            var trimmed = category.Trim();
            var result = new System.Text.StringBuilder();
            for (var i = 0; i < trimmed.Length; i++)
            {
                var current = trimmed[i];
                if (i > 0 &&
                    char.IsUpper(current) &&
                    !char.IsWhiteSpace(trimmed[i - 1]) &&
                    (char.IsLower(trimmed[i - 1]) || (i + 1 < trimmed.Length && char.IsLower(trimmed[i + 1]))))
                {
                    result.Append(' ');
                }

                result.Append(current);
            }

            return result.ToString();
        }
    }
}
