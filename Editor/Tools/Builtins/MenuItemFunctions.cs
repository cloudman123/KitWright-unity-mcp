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
        // Menu items that open a modal, a file picker, or quit. A modal runs its own message loop,
        // so the editor stops pumping and this very call can never return -- the user has to click
        // the dialog before anything recovers.
        private static readonly string[] BlockingMenuPaths =
        {
            "File/New Scene",
            "File/Open Scene",
            "File/Open Recent Scene",
            "File/Save As",
            "File/Save Scene As",
            "File/Build And Run",
            "File/Build Profiles",
            "File/Build Settings",
            "File/Exit",
            "File/Quit",
            "Assets/Import New Asset",
            "Assets/Import Package",
            "Assets/Export Package",
            "Help/About Unity",
        };

        internal static string MatchBlockingMenuPath(string menuPath)
        {
            if (string.IsNullOrWhiteSpace(menuPath))
                return null;

            var trimmed = menuPath.Trim().TrimEnd('.', '…');
            foreach (var blocked in BlockingMenuPaths)
            {
                if (trimmed.StartsWith(blocked, StringComparison.OrdinalIgnoreCase))
                    return blocked;
            }
            return null;
        }

        [Description("Execute an editor menu item by its full path. " +
                     "Examples: 'GameObject/2D Object/Sprites/Square', 'Window/Layouts/Default', 'Edit/Undo'. " +
                     "Returns success/failure based on whether the menu item exists and was triggered. " +
                     "Paths known to open a modal dialog or file picker are refused, because a modal stops the " +
                     "editor loop this call returns on and only a human clicking the dialog can unstick it; " +
                     "pass allow_modal=true to run one anyway when someone is watching the editor.")]
        public static object ExecuteMenuItem(
            [ToolParam("Full menu path, e.g. 'GameObject/2D Object/Sprite'")] string menu_path,
            [ToolParam("Run a menu item that is known to open a modal dialog or file picker. Only with a human at the editor.", Required = false)] bool allow_modal = false)
        {
            if (string.IsNullOrWhiteSpace(menu_path))
                return Response.Error("MENU_PATH_REQUIRED", new { message = "menu_path cannot be empty." });

            var blocking = allow_modal ? null : MatchBlockingMenuPath(menu_path);
            if (blocking != null)
            {
                return Response.Error("MENU_ITEM_OPENS_MODAL", new { menu_path, matched = blocking },
                    $"'{blocking}' opens a modal dialog or file picker. That blocks the editor loop this call " +
                    "returns on, so the call would hang until a human dismissed it. Use a dedicated tool instead " +
                    "(save_scene, open_scene, create_new_scene, build_player, install_package), or pass " +
                    "allow_modal=true if someone is at the editor to click it.");
            }

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
