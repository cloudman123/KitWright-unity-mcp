// Copyright (C) GameWright. Licensed under MIT.

using UnityEditor;
using UnityEngine;

namespace GameWright.Editor.MCP.Server
{
    internal static class GameWrightIcon
    {
        private static readonly System.Collections.Generic.Dictionary<string, Texture2D> _cache =
            new System.Collections.Generic.Dictionary<string, Texture2D>();

        public static Texture2D Texture => Load("gamewright_icon");

        public static Texture2D TabTexture => Load("gamewright_icon_tab");

        private static Texture2D Load(string assetName)
        {
            if (_cache.TryGetValue(assetName, out var cached))
                return cached;

            Texture2D found = null;
            foreach (var guid in AssetDatabase.FindAssets($"{assetName} t:Texture2D"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (System.IO.Path.GetFileNameWithoutExtension(path) != assetName)
                    continue;
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (tex != null)
                {
                    found = tex;
                    break;
                }
            }

            _cache[assetName] = found;
            return found;
        }
    }
}
