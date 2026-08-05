// Copyright (C) GameWright. Licensed under MIT.

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace GameWright.Editor.MCP.Server
{
    /// <summary>
    /// A labelled iOS-style on/off switch (green on, red off) used in place of the default
    /// Unity checkbox across the MCP window tabs.
    /// </summary>
    internal sealed class MCPSwitchToggle : VisualElement
    {
        private static readonly Color OnTrack = new Color(0.30f, 0.66f, 0.36f);
        private static readonly Color OffTrack = new Color(0.62f, 0.26f, 0.26f);

        private readonly Label _label;
        private readonly VisualElement _track;
        private readonly VisualElement _knob;
        private Action<bool> _onChanged;
        private bool _value;

        public MCPSwitchToggle(string label)
        {
            style.flexDirection = FlexDirection.Row;
            style.alignItems = Align.Center;

            _label = new Label(label);
            _label.style.flexGrow = 1;
            _label.style.fontSize = 13;
            _label.style.color = new Color(0.85f, 0.85f, 0.85f);
            Add(_label);

            _track = new VisualElement();
            _track.style.width = 34;
            _track.style.height = 18;
            _track.style.flexShrink = 0;
            _track.style.backgroundColor = OffTrack;
            _track.Rounded(9);
            _track.style.transitionProperty = new List<StylePropertyName> { "background-color" };
            _track.style.transitionDuration = new List<TimeValue> { new TimeValue(0.1f, TimeUnit.Second) };
            Add(_track);

            _knob = new VisualElement();
            _knob.style.position = Position.Absolute;
            _knob.style.width = 14;
            _knob.style.height = 14;
            _knob.style.top = 2;
            _knob.style.left = 2;
            _knob.style.backgroundColor = Color.white;
            _knob.Rounded(7);
            _knob.style.transitionProperty = new List<StylePropertyName> { "left" };
            _knob.style.transitionDuration = new List<TimeValue> { new TimeValue(0.1f, TimeUnit.Second) };
            _knob.style.transitionTimingFunction = new List<EasingFunction> { new EasingFunction(EasingMode.EaseOutCubic) };
            _track.Add(_knob);

            RegisterCallback<ClickEvent>(_ =>
            {
                _value = !_value;
                UpdateVisual();
                _onChanged?.Invoke(_value);
            });

            UpdateVisual();
        }

        public bool value => _value;

        public void SetValueWithoutNotify(bool newValue)
        {
            _value = newValue;
            UpdateVisual();
        }

        public void RegisterValueChangedCallback(Action<bool> callback)
        {
            _onChanged = callback;
        }

        private void UpdateVisual()
        {
            _track.style.backgroundColor = _value ? OnTrack : OffTrack;
            _knob.style.left = _value ? 18 : 2;
        }
    }
}
