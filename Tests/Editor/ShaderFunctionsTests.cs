// Copyright (C) KitWright. Licensed under MIT.

using KitWright.Editor.Tools.Builtins;
using NUnit.Framework;

namespace KitWright.Editor.Tests
{
    public sealed class ShaderFunctionsTests
    {
        [Test]
        public void ResolvePaths_DefaultFolderIsShaders()
        {
            var (_, relativePath) = ShaderFunctions.ResolvePaths("MyShader", "Shaders");
            Assert.AreEqual("Assets/Shaders/MyShader.shader", relativePath);
        }

        [Test]
        public void ResolvePaths_NullPathFallsBackToShaders()
        {
            var (_, relativePath) = ShaderFunctions.ResolvePaths("MyShader", null);
            Assert.AreEqual("Assets/Shaders/MyShader.shader", relativePath);
        }

        [Test]
        public void ResolvePaths_StripsLeadingAssetsPrefix()
        {
            var (_, relativePath) = ShaderFunctions.ResolvePaths("Foo", "Assets/Custom/Sub");
            Assert.AreEqual("Assets/Custom/Sub/Foo.shader", relativePath);
        }

        [Test]
        public void ResolvePaths_NormalizesBackslashes()
        {
            var (_, relativePath) = ShaderFunctions.ResolvePaths("Foo", "Custom\\Sub");
            Assert.AreEqual("Assets/Custom/Sub/Foo.shader", relativePath);
        }

        [Test]
        public void ResolvePaths_BareAssetsFallsBackToShaders()
        {
            var (_, relativePath) = ShaderFunctions.ResolvePaths("Foo", "Assets");
            Assert.AreEqual("Assets/Shaders/Foo.shader", relativePath);
        }
    }
}
