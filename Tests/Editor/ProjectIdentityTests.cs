// Copyright (C) GameWright. Licensed under MIT.

using GameWright.Editor.MCP.Server;
using NUnit.Framework;

namespace GameWright.Editor.Tests
{
    public class ProjectIdentityTests
    {
        [Test]
        public void Pin_DiffersForProjectsSharingAProductName()
        {
            Assert.AreNotEqual(
                ProjectIdentity.PinFromProjectPath(@"C:\Unity\PROJECT_TEST"),
                ProjectIdentity.PinFromProjectPath(@"C:\Unity\clone\PROJECT_TEST"));
        }

        [Test]
        public void Pin_IgnoresSeparatorStyleCaseAndTrailingSlash()
        {
            var expected = ProjectIdentity.PinFromProjectPath(@"C:\Unity\PROJECT_TEST");

            Assert.AreEqual(expected, ProjectIdentity.PinFromProjectPath(@"C:/Unity/PROJECT_TEST"));
            Assert.AreEqual(expected, ProjectIdentity.PinFromProjectPath(@"c:\unity\project_test"));
            Assert.AreEqual(expected, ProjectIdentity.PinFromProjectPath(@"C:\Unity\PROJECT_TEST\"));
        }

        [Test]
        public void Pin_IsThePrefixOfTheFullIdentity()
        {
            const string path = @"C:\Unity\PROJECT_TEST";
            var pin = ProjectIdentity.PinFromProjectPath(path);

            Assert.AreEqual(ProjectIdentity.PinLength, pin.Length);
            StringAssert.StartsWith(pin, ProjectIdentity.FromProjectPath(path));
        }
    }
}
