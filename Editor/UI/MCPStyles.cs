// Copyright (C) GameWright. Licensed under MIT.

using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace GameWright.Editor.MCP.Server
{
    internal static class MCPStyleExt
    {
        public static VisualElement Rounded(this VisualElement e, float r)
        {
            e.style.borderTopLeftRadius = r;
            e.style.borderTopRightRadius = r;
            e.style.borderBottomLeftRadius = r;
            e.style.borderBottomRightRadius = r;
            return e;
        }

        public static VisualElement Border(this VisualElement e, float w, Color c)
        {
            e.style.borderTopWidth = w;
            e.style.borderRightWidth = w;
            e.style.borderBottomWidth = w;
            e.style.borderLeftWidth = w;
            e.style.borderTopColor = c;
            e.style.borderRightColor = c;
            e.style.borderBottomColor = c;
            e.style.borderLeftColor = c;
            return e;
        }

        public static VisualElement Padding(this VisualElement e, float t, float r, float b, float l)
        {
            e.style.paddingTop = t;
            e.style.paddingRight = r;
            e.style.paddingBottom = b;
            e.style.paddingLeft = l;
            return e;
        }

        public static VisualElement Margin(this VisualElement e, float t, float r, float b, float l)
        {
            e.style.marginTop = t;
            e.style.marginRight = r;
            e.style.marginBottom = b;
            e.style.marginLeft = l;
            return e;
        }

        public static Foldout Persist(this Foldout f, string key)
        {
            f.value = SessionState.GetBool("GameWright.MCP.Foldout." + key, f.value);
            f.RegisterValueChangedCallback(evt => SessionState.SetBool("GameWright.MCP.Foldout." + key, evt.newValue));
            return f;
        }

        public static VisualElement Card(this VisualElement e)
        {
            e.style.backgroundColor = new Color(0.155f, 0.155f, 0.16f);
            e.style.marginBottom = 8;
            return e.Rounded(6).Border(1, new Color(0.09f, 0.09f, 0.09f)).Padding(8, 10, 8, 10);
        }

        // UITK defaults Label/TextField to flex-shrink:0, so a long string (a config path) keeps
        // its full width, pushes the row's trailing buttons out of the window, and never gets
        // squeezed enough for Ellipsis to trigger.
        public static VisualElement Shrinkable(this VisualElement e)
        {
            e.style.flexShrink = 1;
            e.style.minWidth = 0;
            return e;
        }

        public static VisualElement Ellipsize(this VisualElement e)
        {
            e.Shrinkable();
            e.style.overflow = Overflow.Hidden;
            e.style.textOverflow = TextOverflow.Ellipsis;
            return e;
        }
    }

    internal static class MCPSection
    {
        public static Label PanelTitle(string text)
        {
            var label = new Label(text);
            label.style.fontSize = 18;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.color = Color.white;
            label.style.marginBottom = 4;
            return label;
        }

        public static Label PanelHint(string text)
        {
            var label = new Label(text);
            label.style.fontSize = 12;
            label.style.color = new Color(0.65f, 0.65f, 0.65f);
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.marginBottom = 10;
            return label;
        }

        public static (VisualElement Section, Foldout Foldout) Create(string title, string persistKey, string countText = null, Color? labelColor = null)
        {
            var section = new VisualElement().Card();

            var foldout = new Foldout { text = title, value = true }.Persist(persistKey);
            var toggle = foldout.Q<Toggle>();

            var label = toggle?.Q<Label>();
            if (label != null)
            {
                label.style.fontSize = 13;
                label.style.unityFontStyleAndWeight = FontStyle.Bold;
                label.style.color = labelColor ?? new Color(0.85f, 0.85f, 0.88f);
                label.style.flexGrow = 1;
            }

            if (toggle != null && !string.IsNullOrEmpty(countText))
            {
                var badge = new Label(countText);
                badge.style.fontSize = 10;
                badge.style.unityFontStyleAndWeight = FontStyle.Bold;
                badge.style.color = new Color(0.5f, 0.85f, 0.55f);
                badge.style.backgroundColor = new Color(0.16f, 0.28f, 0.18f);
                badge.Rounded(8).Padding(2, 7, 2, 7);
                badge.style.marginRight = 0;
                toggle.Add(badge);
            }

            section.Add(foldout);
            return (section, foldout);
        }
    }

    internal static class MCPDropdownStyle
    {
        private static readonly Color Background = new Color(0.20f, 0.20f, 0.21f);
        private static readonly Color Border = new Color(0.09f, 0.09f, 0.09f);
        private static readonly Color Text = new Color(0.85f, 0.85f, 0.85f);

        public static void Apply(VisualElement dropdown)
        {
            if (dropdown == null)
                return;

            // PopupField/DropdownField carry default left/right margins that push the control
            // out of alignment with sibling rows. Zero them so it lines up flush.
            dropdown.style.marginLeft = 0;
            dropdown.style.marginRight = 0;

            var input = dropdown.Q(className: "unity-base-popup-field__input") ?? dropdown;
            input.style.marginLeft = 0;
            input.style.marginRight = 0;
            input.style.backgroundColor = Background;
            input.Rounded(5).Border(1, Border);
            input.style.paddingLeft = 8;
            input.style.paddingRight = 6;
            input.style.height = 22;

            var text = input.Q<Label>(className: "unity-base-popup-field__text");
            if (text != null)
                text.style.color = Text;
        }
    }
}
