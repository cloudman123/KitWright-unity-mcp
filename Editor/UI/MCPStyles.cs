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

        public static Foldout Persist(this Foldout f, string key)
        {
            f.value = SessionState.GetBool("GameWright.MCP.Foldout." + key, f.value);
            f.RegisterValueChangedCallback(evt => SessionState.SetBool("GameWright.MCP.Foldout." + key, evt.newValue));
            return f;
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
