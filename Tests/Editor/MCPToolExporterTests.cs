// Copyright (C) KitWright. Licensed under MIT.

using System.Collections.Generic;
using System.Linq;
using KitWright.Editor.MCP.Server;
using NUnit.Framework;

namespace KitWright.Editor.Tests
{
    public sealed class MCPToolExporterTests
    {
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
    }
}
