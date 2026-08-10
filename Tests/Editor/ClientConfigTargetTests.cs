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
    }
}
