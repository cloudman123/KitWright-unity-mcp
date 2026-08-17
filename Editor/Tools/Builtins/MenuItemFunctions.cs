// Copyright (C) KitWright. Licensed under MIT.

using System;
using DescriptionAttribute = System.ComponentModel.DescriptionAttribute;
using KitWright.Editor.Tools.Helpers;
using UnityEditor;

namespace KitWright.Editor.Tools.Builtins
{
    /// <summary>
    /// Invoke arbitrary editor menu items. This is the cheap-and-broad fallback when no
    /// dedicated tool exists — Unity itself, all engine modules, and every third-party
    /// package already publish their commands as menu items, so an agent can drive most
    /// editor functionality through this without us writing wrappers.
    /// </summary>
    [ToolProvider("MenuItem")]
    internal static class MenuItemFunctions
    {
        [Description("Execute an editor menu item by its full path. " +
                     "Examples: 'GameObject/2D Object/Sprites/Square', 'Window/Layouts/Default', 'Edit/Undo'. " +
                     "Menu paths ending in '...' open a modal dialog or file picker, which freezes the editor " +
                     "until a human dismisses it; those are refused unless allow_modal is true. " +
                     "Returns success/failure based on whether the menu item exists and was triggered.")]
        public static object ExecuteMenuItem(
            [ToolParam("Full menu path, e.g. 'GameObject/2D Object/Sprite'")] string menu_path,
            [ToolParam("Run the item even if it looks like it opens a modal dialog (only when a human is at the editor)", Required = false)] bool allow_modal = false)
        {
            if (string.IsNullOrWhiteSpace(menu_path))
                return Response.Error("MENU_PATH_REQUIRED", new { message = "menu_path cannot be empty." });

            // ponytail: trailing "..." (or the single-character ellipsis, which [MenuItem] attributes
            // outside the editor's own menus often use) is the editor's own convention for "opens a
            // dialog" — good enough to catch Save As.../Import New Asset...; swap for a real per-item
            // probe if it misses cases. It does not catch an item that prompts without the dots.
            var trimmed = menu_path.TrimEnd();

            // Ends the session outright rather than blocking it, so allow_modal is not the way in:
            // the reply would never be delivered. execute_code refuses EditorApplication.Exit too.
            if (string.Equals(trimmed, "File/Exit", StringComparison.OrdinalIgnoreCase))
                return Response.Error("MENU_ITEM_QUITS_EDITOR", new
                {
                    menu_path,
                    hint = "This closes the editor, which drops this connection. Quit Unity by hand if that is what you want."
                });

            if (!allow_modal && (trimmed.EndsWith("...") || trimmed.EndsWith("…")))
                return Response.Error("MENU_ITEM_OPENS_MODAL", new
                {
                    menu_path,
                    hint = "This opens a modal dialog that would block the editor and hang this request. " +
                           "Use a dedicated tool instead, or pass allow_modal=true if a human is watching the editor."
                });

            try
            {
                var ok = EditorApplication.ExecuteMenuItem(menu_path);
                if (!ok)
                    return Response.Error("MENU_ITEM_NOT_FOUND",
                        new { menu_path, hint = "Verify the path matches the editor menu hierarchy exactly (case sensitive, '/' separated)." });
                return Response.Success($"Executed menu item '{menu_path}'.");
            }
            catch (Exception ex)
            {
                return Response.Error("MENU_EXECUTION_FAILED", new { message = ex.Message });
            }
        }
    }
}
