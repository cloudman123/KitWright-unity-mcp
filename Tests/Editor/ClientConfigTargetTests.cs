// Copyright (C) GameWright. Licensed under MIT.

using System.IO;
using System.Linq;
using GameWright.Editor.MCP.Server;
using NUnit.Framework;
using UnityEngine;

namespace GameWright.Editor.Tests
{
    public class ClientConfigTargetTests
    {
        private static string ProjectRoot =>
            Path.GetDirectoryName(Application.dataPath) ?? Application.dataPath;

        [Test]
        public void ProjectScopedTargetsWriteInsideTheProjectAndUseThePlainName()
        {
            var scoped = ClientConfigPanel.GetAllTargets().Where(t => t.ProjectScoped).ToArray();

            Assert.IsNotEmpty(scoped, "Claude Code and Cursor should offer project-scoped targets.");

            foreach (var target in scoped)
            {
                Assert.AreEqual("gamewright", ClientConfigPanel.GetServerEntryName(target),
                    $"{target.Name} owns its config file, so the entry needs no pin suffix.");
                Assert.IsTrue(
                    target.ConfigPath.Replace('\\', '/').StartsWith(ProjectRoot.Replace('\\', '/')),
                    $"{target.Name} must write inside the project, got {target.ConfigPath}.");
            }
        }

        [Test]
        public void GlobalTargetsKeepThePinSoSiblingProjectsDoNotCollide()
        {
            var global = ClientConfigPanel.GetAllTargets().Where(t => !t.ProjectScoped).ToArray();

            Assert.IsNotEmpty(global);

            var pin = ProjectIdentity.PinFromProjectPath(ProjectRoot);
            foreach (var target in global)
            {
                StringAssert.EndsWith(pin, ClientConfigPanel.GetServerEntryName(target),
                    $"{target.Name} shares one file with every other project, so it needs the pin.");
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
