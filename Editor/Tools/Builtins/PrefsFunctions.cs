// Copyright (C) KitWright. Licensed under MIT.

using DescriptionAttribute = System.ComponentModel.DescriptionAttribute;
using KitWright.Editor.Tools.Helpers;
using UnityEditor;
using UnityEngine;

namespace KitWright.Editor.Tools.Builtins
{
    [ToolProvider("Prefs")]
    internal static class PrefsFunctions
    {
        [Description("Read an EditorPrefs value (editor-only, persists across sessions on this machine). Auto-detects type as string, int, float, or bool.")]
        [ReadOnlyTool]
        public static object GetEditorPref(
            [ToolParam("Preference key")] string key,
            [ToolParam("Type hint: string, int, float, bool, or auto", Required = false)] string type = "auto")
        {
            if (string.IsNullOrEmpty(key))
                return Response.Error("INVALID_ARGUMENT", new { key });

            if (!EditorPrefs.HasKey(key))
                return Response.Error("KEY_NOT_FOUND", new { key });

            var resolved = ResolvePrefType(type);
            object value = resolved switch
            {
                "int" => EditorPrefs.GetInt(key),
                "float" => EditorPrefs.GetFloat(key),
                "bool" => EditorPrefs.GetBool(key),
                _ => EditorPrefs.GetString(key)
            };

            return Response.Success($"EditorPrefs['{key}'] = {value}", new { key, type = resolved, value });
        }

        [Description("Write an EditorPrefs value (editor-only, persists across sessions on this machine).")]
        public static object SetEditorPref(
            [ToolParam("Preference key")] string key,
            [ToolParam("Value to store")] string value,
            [ToolParam("Type: string, int, float, or bool", Required = false)] string type = "string")
        {
            if (string.IsNullOrEmpty(key))
                return Response.Error("INVALID_ARGUMENT", new { key });

            var resolved = ResolvePrefType(type);
            if (!TryWritePref(key, value, resolved, isEditor: true, out var error))
                return Response.Error("INVALID_VALUE", new { key, value, type = resolved, error });

            return Response.Success($"Set EditorPrefs['{key}'] = {value} ({resolved})", new { key, type = resolved, value });
        }

        [Description("Delete an EditorPrefs key. Returns whether the key existed.")]
        public static object DeleteEditorPref(
            [ToolParam("Preference key")] string key)
        {
            if (string.IsNullOrEmpty(key))
                return Response.Error("INVALID_ARGUMENT", new { key });

            bool existed = EditorPrefs.HasKey(key);
            EditorPrefs.DeleteKey(key);
            return Response.Success($"Deleted EditorPrefs['{key}'] (existed: {existed})", new { key, existed });
        }

        [Description("Read a PlayerPrefs value (shipped with the game, persists across runs). Auto-detects type as string, int, or float.")]
        [ReadOnlyTool]
        public static object GetPlayerPref(
            [ToolParam("Preference key")] string key,
            [ToolParam("Type hint: string, int, float, or auto", Required = false)] string type = "auto")
        {
            if (string.IsNullOrEmpty(key))
                return Response.Error("INVALID_ARGUMENT", new { key });

            if (!PlayerPrefs.HasKey(key))
                return Response.Error("KEY_NOT_FOUND", new { key });

            var resolved = ResolvePrefType(type);
            // PlayerPrefs has no bool; treat bool hint as int.
            if (resolved == "bool") resolved = "int";

            object value = resolved switch
            {
                "int" => PlayerPrefs.GetInt(key),
                "float" => PlayerPrefs.GetFloat(key),
                _ => PlayerPrefs.GetString(key)
            };

            return Response.Success($"PlayerPrefs['{key}'] = {value}", new { key, type = resolved, value });
        }

        [Description("Write a PlayerPrefs value (shipped with the game, persists across runs). Calls PlayerPrefs.Save.")]
        public static object SetPlayerPref(
            [ToolParam("Preference key")] string key,
            [ToolParam("Value to store")] string value,
            [ToolParam("Type: string, int, or float", Required = false)] string type = "string")
        {
            if (string.IsNullOrEmpty(key))
                return Response.Error("INVALID_ARGUMENT", new { key });

            var resolved = ResolvePrefType(type);
            if (resolved == "bool") resolved = "int";

            if (!TryWritePref(key, value, resolved, isEditor: false, out var error))
                return Response.Error("INVALID_VALUE", new { key, value, type = resolved, error });

            PlayerPrefs.Save();
            return Response.Success($"Set PlayerPrefs['{key}'] = {value} ({resolved})", new { key, type = resolved, value });
        }

        [Description("Delete a PlayerPrefs key. Returns whether the key existed. Calls PlayerPrefs.Save.")]
        public static object DeletePlayerPref(
            [ToolParam("Preference key")] string key)
        {
            if (string.IsNullOrEmpty(key))
                return Response.Error("INVALID_ARGUMENT", new { key });

            bool existed = PlayerPrefs.HasKey(key);
            PlayerPrefs.DeleteKey(key);
            PlayerPrefs.Save();
            return Response.Success($"Deleted PlayerPrefs['{key}'] (existed: {existed})", new { key, existed });
        }

        [Description("Delete ALL PlayerPrefs keys for this project. Destructive and irreversible. Calls PlayerPrefs.Save.")]
        public static object DeleteAllPlayerPrefs()
        {
            PlayerPrefs.DeleteAll();
            PlayerPrefs.Save();
            return Response.Success("Deleted all PlayerPrefs keys.");
        }

        internal static string ResolvePrefType(string type)
        {
            switch (type?.Trim().ToLowerInvariant())
            {
                case "int": return "int";
                case "float": return "float";
                case "bool": return "bool";
                case "string": return "string";
                default: return "auto";
            }
        }

        internal static bool TryWritePref(string key, string value, string type, bool isEditor, out string error)
        {
            error = null;
            var effective = type == "auto" ? "string" : type;

            switch (effective)
            {
                case "int":
                    if (!int.TryParse(value, out var i)) { error = "not an int"; return false; }
                    if (isEditor) EditorPrefs.SetInt(key, i); else PlayerPrefs.SetInt(key, i);
                    return true;
                case "float":
                    if (!float.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var f))
                    { error = "not a float"; return false; }
                    if (isEditor) EditorPrefs.SetFloat(key, f); else PlayerPrefs.SetFloat(key, f);
                    return true;
                case "bool":
                    if (!bool.TryParse(value, out var b)) { error = "not a bool"; return false; }
                    EditorPrefs.SetBool(key, b);
                    return true;
                default:
                    if (isEditor) EditorPrefs.SetString(key, value); else PlayerPrefs.SetString(key, value);
                    return true;
            }
        }
    }
}
