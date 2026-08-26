// Copyright (C) KitWright. Licensed under MIT.

using KitWright.Editor.Tools.Builtins;
using NUnit.Framework;
using UnityEngine;

namespace KitWright.Editor.Tests
{
    public sealed class ProjectSettingsFunctionsTests
    {
        private Vector3 _originalGravity;

        [SetUp]
        public void SetUp() => _originalGravity = Physics.gravity;

        // Restore through the tool, not through Physics.gravity: the setter moves the live value but
        // leaves the project file holding the test's value until Unity next saves.
        [TearDown]
        public void TearDown() => ProjectSettingsFunctions.SetProjectSettings("PhysicsManager",
            $"{{\"m_Gravity\": {{\"x\": {_originalGravity.x}, \"y\": {_originalGravity.y}, \"z\": {_originalGravity.z}}}}}");

        // The whole tool rests on one assumption the compiler cannot check: a SerializedObject over a
        // native settings singleton actually moves the live value, not a detached copy.
        [Test]
        public void SetProjectSettings_WriteReachesTheLiveSingleton()
        {
            var result = ProjectSettingsFunctions.SetProjectSettings(
                "PhysicsManager", "{\"m_Gravity\": {\"x\": 0, \"y\": -20.5, \"z\": 0}}").ToString();

            StringAssert.Contains("Applied 1 of 1", result);
            Assert.AreEqual(-20.5f, Physics.gravity.y, 0.0001f);
        }

        [Test]
        public void SetProjectSettings_UnknownPathFailsLoudlyInsteadOfReportingSuccess()
        {
            var result = ProjectSettingsFunctions.SetProjectSettings(
                "PhysicsManager", "{\"m_NoSuchPhysicsField\": 1}").ToString();

            StringAssert.Contains("PROPERTY_SET_FAILED", result);
            Assert.AreEqual(_originalGravity, Physics.gravity);
        }
    }
}
