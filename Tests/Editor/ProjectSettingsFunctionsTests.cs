// Copyright (C) KitWright. Licensed under MIT.

using KitWright.Editor.Tools.Builtins;
using NUnit.Framework;
using UnityEngine;

namespace KitWright.Editor.Tests
{
    public sealed class ProjectSettingsFunctionsTests
    {
        // TimeManager rather than the more obvious PhysicsManager: the tool works on any settings page,
        // so its test should not disappear along with an optional module.
        private const string Singleton = "TimeManager";

        private float _originalTimeScale;

        [SetUp]
        public void SetUp() => _originalTimeScale = Time.timeScale;

        // Restore through the tool, not through Time.timeScale: the setter moves the live value but
        // leaves the project file holding the test's value until Unity next saves.
        [TearDown]
        public void TearDown() => ProjectSettingsFunctions.SetProjectSettings(
            Singleton, $"{{\"m_TimeScale\": {_originalTimeScale}}}");

        // The whole tool rests on one assumption the compiler cannot check: a SerializedObject over a
        // native settings singleton actually moves the live value, not a detached copy.
        [Test]
        public void SetProjectSettings_WriteReachesTheLiveSingleton()
        {
            var result = ProjectSettingsFunctions.SetProjectSettings(
                Singleton, "{\"m_TimeScale\": 0.25}").ToString();

            StringAssert.Contains("Applied 1 of 1", result);
            Assert.AreEqual(0.25f, Time.timeScale, 0.0001f);
        }

        [Test]
        public void SetProjectSettings_UnknownPathFailsLoudlyInsteadOfReportingSuccess()
        {
            var result = ProjectSettingsFunctions.SetProjectSettings(
                Singleton, "{\"m_NoSuchTimeField\": 1}").ToString();

            StringAssert.Contains("PROPERTY_SET_FAILED", result);
            Assert.AreEqual(_originalTimeScale, Time.timeScale, 0.0001f);
        }

        [Test]
        public void SetProjectSettings_UnknownSingletonIsReportedNotSilentlySkipped()
        {
            StringAssert.Contains("SETTINGS_SINGLETON_NOT_FOUND",
                ProjectSettingsFunctions.SetProjectSettings("NoSuchManager", "{\"m_TimeScale\": 1}").ToString());
        }
    }
}
