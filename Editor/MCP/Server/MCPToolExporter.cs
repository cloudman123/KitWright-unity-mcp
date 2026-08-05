// Copyright (C) GameWright. Licensed under MIT.

using System;
using System.Collections.Generic;
using GameWright.Editor.Tools;
using GameWright.Editor.Settings;

namespace GameWright.Editor.MCP.Server
{
    /// <summary>
    /// Exports GameWright tool definitions to MCP tool schema format.
    /// </summary>
    internal class MCPToolExporter
    {
        private readonly ISettingsController _settings;

        public MCPToolExporter(ISettingsController settings)
        {
            _settings = settings;
        }

        public List<Dictionary<string, object>> ExportTools()
        {
            var mcpTools = new List<Dictionary<string, object>>();
            var tools = ToolSchemaBuilder.BuildAll();
            var profile = MCPToolExportPolicy.Parse(_settings?.MCPToolExportProfile);
            var profileKey = MCPToolExportPolicy.ToSettingValue(profile);
            var profileConfigured = _settings?.IsProfileConfigured(profileKey) ?? false;
            var profileTools = _settings?.GetProfileTools(profileKey);
            var compact = _settings?.MCPCompactSchemaEnabled ?? false;

            tools.Sort((left, right) =>
            {
                var leftRank = MCPToolExportPolicy.GetSortRank(left.function.name, profile);
                var rightRank = MCPToolExportPolicy.GetSortRank(right.function.name, profile);
                var compareRank = leftRank.CompareTo(rightRank);
                return compareRank != 0
                    ? compareRank
                    : string.Compare(left.function.name, right.function.name, StringComparison.OrdinalIgnoreCase);
            });

            PluginDebugLogger.Log($"[GameWright MCP Server] Exporting tools with profile '{MCPToolExportPolicy.ToSettingValue(profile)}'");

            foreach (var tool in tools)
            {
                if (!MCPToolExportPolicy.IsToolAllowed(
                        tool.function.name,
                        profile,
                        profileConfigured,
                        profileTools))
                    continue;

                var description = compact ? FirstSentence(tool.function.description) : tool.function.description;

                var mcpTool = new Dictionary<string, object>
                {
                    ["name"] = tool.function.name,
                    ["description"] = MCPToolExportPolicy.BuildDescriptionPrefix(tool.function.name, profile) + description,
                    ["inputSchema"] = ConvertParametersToJsonSchema(tool.function.parameters, compact)
                };
                mcpTools.Add(mcpTool);
            }

            return mcpTools;
        }

        private static string FirstSentence(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            var end = text.IndexOf(". ", StringComparison.Ordinal);
            if (end < 0)
                return text.TrimEnd().TrimEnd('.');

            return text.Substring(0, end + 1);
        }

        private Dictionary<string, object> ConvertParametersToJsonSchema(
            GameWright.Editor.Api.Models.ToolParametersDef parameters,
            bool compact)
        {
            var properties = new Dictionary<string, object>();

            foreach (var prop in parameters.properties)
            {
                var propertySchema = new Dictionary<string, object>
                {
                    ["type"] = prop.Value.type
                };

                if (!compact)
                    propertySchema["description"] = prop.Value.description;

                if (prop.Value.@enum != null && prop.Value.@enum.Count > 0)
                    propertySchema["enum"] = prop.Value.@enum;

                if (!string.IsNullOrEmpty(prop.Value.@default))
                    propertySchema["default"] = prop.Value.@default;

                properties[prop.Key] = propertySchema;
            }

            var schema = new Dictionary<string, object>
            {
                ["type"] = "object",
                ["properties"] = properties
            };

            if (parameters.required != null && parameters.required.Count > 0)
                schema["required"] = parameters.required;

            return schema;
        }
    }
}
