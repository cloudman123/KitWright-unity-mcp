// Copyright (C) KitWright. Licensed under MIT.

using System.Collections.Generic;
using System.Linq;
using DescriptionAttribute = System.ComponentModel.DescriptionAttribute;
using KitWright.Editor.MCP.Server;
using KitWright.Editor.Tools;
using NUnit.Framework;

namespace KitWright.Editor.Tests
{
    // Stands in for a tool declared by project code: the attributes are public, and this
    // assembly is not one of the package assemblies, so the registry must treat it as custom.
    [ToolProvider("Test")]
    public static class CustomToolExposureProbeProvider
    {
        [Description("Test-only probe verifying that project-declared tools are discovered and exposed.")]
        [ReadOnlyTool]
        public static object CustomToolExposureProbe(
            [ToolParam("Ignored")] string value = null) => value;
    }

    public sealed class MCPToolExporterTests
    {
        private const string ProbeTool = "custom_tool_exposure_probe";

        // A null settings controller falls back to the default core profile.
        private static List<Dictionary<string, object>> ExportCoreTools() =>
            new MCPToolExporter(null).ExportTools();

        private static Dictionary<string, object> Tool(string name) =>
            ExportCoreTools().FirstOrDefault(t => (string)t["name"] == name);

        private static bool HasReadOnlyHint(Dictionary<string, object> tool)
        {
            if (tool == null || !tool.TryGetValue("annotations", out var raw))
                return false;

            return raw is Dictionary<string, object> annotations &&
                   annotations.TryGetValue("readOnlyHint", out var hint) &&
                   hint is bool value && value;
        }

        [Test]
        public void ReadOnlyToolsCarryReadOnlyHint()
        {
            Assert.IsTrue(HasReadOnlyHint(Tool("get_hierarchy")));
            Assert.IsTrue(HasReadOnlyHint(Tool("get_console_logs")));
            Assert.IsTrue(HasReadOnlyHint(Tool("reflect_api")));
        }

        [Test]
        public void MutatingToolsHaveNoReadOnlyHint()
        {
            Assert.IsFalse(HasReadOnlyHint(Tool("execute_code")));
            Assert.IsFalse(HasReadOnlyHint(Tool("set_component_property")));
        }

        [Test]
        public void CoreProfileExposesReflectApi()
        {
            Assert.IsNotNull(Tool("reflect_api"));
        }

        [Test]
        public void ProjectDeclaredToolIsDiscovered()
        {
            Assert.IsNotNull(ToolRegistry.GetMethod(ProbeTool));
        }

        [Test]
        public void ProjectDeclaredToolIsMarkedCustom()
        {
            Assert.IsTrue(ToolRegistry.IsCustomTool(ProbeTool));
            Assert.IsFalse(ToolRegistry.IsCustomTool("get_hierarchy"), "Built-in tools are not custom.");
        }

        [Test]
        public void PackageAssemblyDetectionSeparatesBuiltInFromProject()
        {
            Assert.IsTrue(ToolRegistry.IsPackageAssembly(typeof(ToolRegistry).Assembly));
            Assert.IsFalse(ToolRegistry.IsPackageAssembly(typeof(MCPToolExporterTests).Assembly));
        }

        // Without this, a project tool would be invisible under the default core profile,
        // which is the profile most clients connect with.
        [Test]
        public void CustomToolIsExposedUnderNonFullProfiles()
        {
            foreach (var profile in new[]
            {
                MCPToolExportProfile.Minimal,
                MCPToolExportProfile.Core,
                MCPToolExportProfile.Extended
            })
            {
                Assert.IsTrue(
                    MCPToolExportPolicy.IsToolAllowed(ProbeTool, profile, profileConfigured: false, profileTools: null),
                    $"Custom tool should be exposed under the {profile} profile.");
            }
        }

        [Test]
        public void ExplicitProfileConfigurationStillWinsOverCustomTool()
        {
            Assert.IsFalse(MCPToolExportPolicy.IsToolAllowed(
                ProbeTool,
                MCPToolExportProfile.Core,
                profileConfigured: true,
                profileTools: new[] { "execute_code" }));
        }

        [Test]
        public void BuiltInToolOutsideCoreStaysHiddenUnderCore()
        {
            Assert.IsFalse(MCPToolExportPolicy.IsToolAllowed(
                "create_terrain",
                MCPToolExportProfile.Core,
                profileConfigured: false,
                profileTools: null));
        }
    }
}
