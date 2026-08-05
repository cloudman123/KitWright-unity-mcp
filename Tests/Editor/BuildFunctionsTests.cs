// Copyright (C) GameWright. Licensed under MIT.

using GameWright.Editor.Tools.Builtins;
using NUnit.Framework;
using UnityEditor;

namespace GameWright.Editor.Tests
{
    public sealed class BuildFunctionsTests
    {
        [Test]
        public void TryResolveTarget_EmptyUsesActiveTarget()
        {
            Assert.IsTrue(BuildFunctions.TryResolveTarget(null, out var target));
            Assert.AreEqual(EditorUserBuildSettings.activeBuildTarget, target);
        }

        [Test]
        public void TryResolveTarget_KnownAliases()
        {
            Assert.IsTrue(BuildFunctions.TryResolveTarget("windows64", out var win));
            Assert.AreEqual(BuildTarget.StandaloneWindows64, win);

            Assert.IsTrue(BuildFunctions.TryResolveTarget("android", out var android));
            Assert.AreEqual(BuildTarget.Android, android);

            Assert.IsTrue(BuildFunctions.TryResolveTarget("webgl", out var webgl));
            Assert.AreEqual(BuildTarget.WebGL, webgl);
        }

        [Test]
        public void TryResolveTarget_CaseInsensitive()
        {
            Assert.IsTrue(BuildFunctions.TryResolveTarget("WINDOWS64", out var target));
            Assert.AreEqual(BuildTarget.StandaloneWindows64, target);
        }

        [Test]
        public void TryResolveTarget_UnknownReturnsFalse()
        {
            Assert.IsFalse(BuildFunctions.TryResolveTarget("nintendo_switch_2", out _));
        }

        [Test]
        public void DefaultOutputPath_WindowsHasExeExtension()
        {
            var path = BuildFunctions.DefaultOutputPath(BuildTarget.StandaloneWindows64, "MyGame");
            Assert.AreEqual("Builds/StandaloneWindows64/MyGame.exe", path);
        }

        [Test]
        public void DefaultOutputPath_WebGLIsDirectory()
        {
            var path = BuildFunctions.DefaultOutputPath(BuildTarget.WebGL, "MyGame");
            Assert.AreEqual("Builds/WebGL/MyGame", path);
        }
    }
}
