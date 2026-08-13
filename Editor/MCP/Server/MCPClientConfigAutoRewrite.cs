// Copyright (C) KitWright. Licensed under MIT.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace KitWright.Editor.MCP.Server
{
    /// <summary>
    /// After the MCP server starts, sweeps all known client config files and repairs any
    /// existing entry for this project whose URL no longer matches the live server —
    /// stale ports happen when the server falls forward to a free port (multi-editor)
    /// or the user changes the port setting. Never creates new entries: only files the
    /// user already configured are touched.
    /// </summary>
    internal static class MCPClientConfigAutoRewrite
    {
        /// <summary>Schedule a sweep on the editor thread. Safe to call from any thread.</summary>
        public static void Schedule(int port)
        {
            var serverUrl = ClientConfigPanel.BuildServerUrl(port);
            EditorApplication.delayCall += () => Run(serverUrl);
        }

        public static void Run(string serverUrl)
        {
            var rewritten = new List<string>();

            foreach (var target in ClientConfigPanel.GetAllTargets())
            {
                // Sweep both scopes rather than the target's currently selected one: a client
                // configured globally would otherwise keep a stale URL forever, because the
                // selected scope resolves to the project file whenever one is possible.
                // Repairing is safe in both places — an entry is only ever updated, never added.
                foreach (var path in new[] { target.ProjectConfigPath, target.GlobalConfigPath })
                {
                    try
                    {
                        if (string.IsNullOrEmpty(path) || !File.Exists(path))
                            continue;

                        var serverName = ClientConfigPanel.ServerEntryName;
                        var changed = target.IsToml
                            ? RewriteToml(path, serverName, serverUrl)
                            : RewriteJson(path, target.RootKey, serverName, serverUrl);

                        if (changed)
                            rewritten.Add($"{target.Name} ({path})");
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[KitWright MCP Server] Config auto-rewrite failed for {target.Name} ({path}): {ex.Message}");
                    }
                }
            }

            if (rewritten.Count > 0)
                Debug.Log($"[KitWright MCP Server] Updated stale MCP config URL to {serverUrl} for:\n{string.Join("\n", rewritten)}\nRestart or reload the client(s) to reconnect.");
        }

        internal static bool RewriteJson(
            string configPath, string targetRootKey, string serverName, string serverUrl)
        {
            var json = File.ReadAllText(configPath);
            if (!(SimpleJsonHelper.Deserialize(json) is Dictionary<string, object> root))
                return false;

            var rootKey = string.IsNullOrEmpty(targetRootKey) ? "mcpServers" : targetRootKey;
            if (!(root.TryGetValue(rootKey, out var serversObj) && serversObj is Dictionary<string, object> servers))
                return false;
            if (!(servers.TryGetValue(serverName, out var entryObj) && entryObj is Dictionary<string, object> entry))
                return false;

            // Clients diverge on the URL property name ("url" / "serverUrl" / "httpUrl");
            // update whichever the entry actually uses.
            var changed = false;
            foreach (var key in new[] { "url", "serverUrl", "httpUrl" })
            {
                if (entry.TryGetValue(key, out var value) &&
                    value is string existing &&
                    !UrlsEqual(existing, serverUrl))
                {
                    entry[key] = serverUrl;
                    changed = true;
                }
            }

            if (changed)
                File.WriteAllText(configPath, SimpleJsonHelper.Serialize(root));

            return changed;
        }

        private static bool RewriteToml(string path, string serverName, string serverUrl)
        {
            var content = File.ReadAllText(path);
            var header = "[mcp_servers." + serverName + "]";
            var start = content.IndexOf(header, StringComparison.Ordinal);
            if (start < 0)
                return false;

            var end = content.IndexOf("\n[", start + header.Length, StringComparison.Ordinal);
            if (end < 0)
                end = content.Length;

            var section = content.Substring(start, end - start);
            var updated = Regex.Replace(section, "url\\s*=\\s*\"[^\"]*\"", "url = \"" + serverUrl + "\"");
            if (updated == section)
                return false;

            File.WriteAllText(path, content.Substring(0, start) + updated + content.Substring(end));
            return true;
        }

        private static bool UrlsEqual(string a, string b)
        {
            return string.Equals(
                a?.Trim().TrimEnd('/'),
                b?.Trim().TrimEnd('/'),
                StringComparison.OrdinalIgnoreCase);
        }
    }
}
