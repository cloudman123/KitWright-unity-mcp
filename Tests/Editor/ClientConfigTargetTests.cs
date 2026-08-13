// Copyright (C) KitWright. Licensed under MIT.

using System.IO;
using System.Linq;
using KitWright.Editor.MCP.Server;
using NUnit.Framework;
using UnityEngine;

namespace KitWright.Editor.Tests
{
    public class ClientConfigTargetTests
    {
        private static string ProjectRoot =>
            Path.GetDirectoryName(Application.dataPath) ?? Application.dataPath;

        [Test]
        public void ProjectScopedTargetsWriteInsideTheProject()
        {
            var scoped = ClientConfigPanel.GetAllTargets().Where(t => t.ProjectScoped).ToArray();

            Assert.IsNotEmpty(scoped, "Claude Code and Cursor should offer project-scoped targets.");

            foreach (var target in scoped)
            {
                Assert.IsTrue(
                    target.ConfigPath.Replace('\\', '/').StartsWith(ProjectRoot.Replace('\\', '/')),
                    $"{target.Name} must write inside the project, got {target.ConfigPath}.");
            }
        }

        // Scope selection is a display-time choice, so a target must resolve to a real path in
        // whichever scope it is asked for, and fall back when it only supports one.
        [Test]
        public void EveryTargetResolvesAPathInBothScopes()
        {
            foreach (var target in ClientConfigPanel.GetAllTargets())
            {
                Assert.IsTrue(target.Supports(true) || target.Supports(false),
                    $"{target.Name} has no config path at all.");
                Assert.IsNotEmpty(target.ConfigPath, $"{target.Name} resolved to an empty path.");
            }
        }

        [Test]
        public void EveryTargetNameIsUnique()
        {
            var names = ClientConfigPanel.GetAllTargets().Select(t => t.Name).ToArray();

            Assert.AreEqual(names.Length, names.Distinct().Count(),
                "The dropdown selects a target by name, so duplicates would be unreachable.");
        }

        // The sweep used to visit only the project-scoped file, so a client configured in the
        // global file kept pointing at a port the server had already left. Sweeping both is only
        // safe because a config without our entry must come back untouched.
        [Test]
        public void RewriteJson_RepairsTheGlobalFileAndLeavesAForeignOneAlone()
        {
            var dir = Path.Combine(Path.GetTempPath(), "KitWrightConfigRewrite_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                var foreignJson = "{\"mcpServers\":{\"ai-game-developer\":{\"url\":\"http://localhost:23275/\"}}}";
                var projectPath = Path.Combine(dir, "project.json");
                var globalPath = Path.Combine(dir, "global.json");
                File.WriteAllText(projectPath, foreignJson);
                File.WriteAllText(globalPath,
                    "{\"mcpServers\":{\"kitwright\":{\"type\":\"http\",\"url\":\"http://127.0.0.1:8766/\"}}}");

                var url = "http://127.0.0.1:8765/";
                Assert.IsFalse(
                    MCPClientConfigAutoRewrite.RewriteJson(projectPath, "mcpServers", "kitwright", url),
                    "A config without our entry must not be reported as rewritten.");
                Assert.AreEqual(foreignJson, File.ReadAllText(projectPath),
                    "A config without our entry must not be modified.");

                Assert.IsTrue(
                    MCPClientConfigAutoRewrite.RewriteJson(globalPath, "mcpServers", "kitwright", url),
                    "The global file holds the stale entry and must be repaired.");
                StringAssert.Contains("8765", File.ReadAllText(globalPath));
            }
            finally
            {
                try { Directory.Delete(dir, true); } catch { }
            }
        }
    }
}
