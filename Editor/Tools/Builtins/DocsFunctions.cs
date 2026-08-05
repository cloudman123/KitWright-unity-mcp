// Copyright (C) GameWright. Licensed under MIT.

using DescriptionAttribute = System.ComponentModel.DescriptionAttribute;
using GameWright.Editor.Tools.Helpers;
using UnityEngine;

namespace GameWright.Editor.Tools.Builtins
{
    [ToolProvider("Docs")]
    internal static class DocsFunctions
    {
        [Description("Get the Unity documentation URL for a scripting API type or member. Returns the ScriptReference link for the current Unity version (e.g. 'Rigidbody', 'GameObject.SetActive', 'AI.NavMeshAgent').")]
        [ReadOnlyTool]
        public static object GetScriptReferenceUrl(
            [ToolParam("Type or member, e.g. 'Rigidbody' or 'Transform.Rotate'. Namespace dots are stripped except the member separator.")] string symbol)
        {
            if (string.IsNullOrWhiteSpace(symbol)) return Response.Error("EMPTY_SYMBOL");

            var page = symbol.Trim().Replace("UnityEngine.", "").Replace("UnityEditor.", "");
            var url = $"https://docs.unity3d.com/{DocVersion()}/Documentation/ScriptReference/{page}.html";

            return Response.Success($"ScriptReference URL for '{symbol}'.", new { symbol, url, unityVersion = Application.unityVersion });
        }

        [Description("Get a Unity Manual search URL for a topic (e.g. 'lightmapping', 'addressables'). Returns a docs.unity3d.com Manual search link for the current Unity version.")]
        [ReadOnlyTool]
        public static object SearchManual(
            [ToolParam("Topic or keyword to search the Unity Manual for")] string query)
        {
            if (string.IsNullOrWhiteSpace(query)) return Response.Error("EMPTY_QUERY");

            var encoded = UnityEngine.Networking.UnityWebRequest.EscapeURL(query.Trim());
            var url = $"https://docs.unity3d.com/{DocVersion()}/Documentation/Manual/30_search.html?q={encoded}";

            return Response.Success($"Manual search URL for '{query}'.", new { query, url, unityVersion = Application.unityVersion });
        }

        internal static string DocVersion(string unityVersion)
        {
            var v = unityVersion;
            int first = v.IndexOf('.');
            if (first < 0) return v;
            int second = v.IndexOf('.', first + 1);
            return second < 0 ? v : v.Substring(0, second);
        }

        private static string DocVersion() => DocVersion(Application.unityVersion);
    }
}
