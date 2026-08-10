// Copyright (C) KitWright. Licensed under MIT.

using KitWright.Editor.Tools.Builtins;
using NUnit.Framework;

namespace KitWright.Editor.Tests
{
    public sealed class DocsFunctionsTests
    {
        [Test]
        public void DocVersion_StripsPatchAndSuffix()
        {
            Assert.AreEqual("2022.3", DocsFunctions.DocVersion("2022.3.15f1"));
            Assert.AreEqual("6000.0", DocsFunctions.DocVersion("6000.0.23f1"));
        }

        [Test]
        public void DocVersion_HandlesMajorMinorOnly()
        {
            Assert.AreEqual("2021.2", DocsFunctions.DocVersion("2021.2"));
        }

        [Test]
        public void DocVersion_NoDotReturnsAsIs()
        {
            Assert.AreEqual("2022", DocsFunctions.DocVersion("2022"));
        }
    }
}
