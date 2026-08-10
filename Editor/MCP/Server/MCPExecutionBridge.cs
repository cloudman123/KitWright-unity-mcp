// Copyright (C) KitWright. Licensed under MIT.

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using KitWright.Editor.Settings;
using KitWright.Editor.State;
using KitWright.Editor.Threading;
using KitWright.Editor.Tools;
using KitWright.Editor.Tools.Helpers;
using UnityEngine;

namespace KitWright.Editor.MCP.Server
{
    /// <summary>
    /// Bridges MCP tool calls to KitWright's FunctionInvokerController.
    /// Handles thread marshalling and approval workflow.
    /// </summary>
    internal class MCPExecutionBridge
    {
        private readonly IEditorThreadHelper _threadHelper;
        private readonly ISettingsController _settings;
        private readonly IStateController _stateController;
        private readonly FunctionInvokerController _invoker;
        private readonly MCPInteractionLog _interactionLog;

        public MCPExecutionBridge(
            IEditorThreadHelper threadHelper,
            ISettingsController settings,
            IStateController stateController,
            FunctionInvokerController invoker,
            MCPInteractionLog interactionLog)
        {
            _threadHelper = threadHelper ?? throw new ArgumentNullException(nameof(threadHelper));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _stateController = stateController ?? throw new ArgumentNullException(nameof(stateController));
            _invoker = invoker ?? throw new ArgumentNullException(nameof(invoker));
            _interactionLog = interactionLog;
        }

        public async Task<string> ExecuteToolAsync(
            string toolName,
            Dictionary<string, object> arguments,
            CancellationToken ct)
        {
            return await _threadHelper.ExecuteAsyncOnEditorThreadAsync(async () =>
            {
                try
                {
                    ct.ThrowIfCancellationRequested();
                    var functionCall = new FunctionCall
                    {
                        FunctionName = toolName
                    };

                    foreach (var kvp in arguments)
                        functionCall.Parameters[kvp.Key] = ConvertArgumentToString(kvp.Value);

                    ToolRegistry.ManualTools.TryGetValue(toolName, out var manualTool);
                    var method = ToolRegistry.GetMethod(toolName);
                    if (method == null && manualTool == null)
                    {
                        var error = ToolResultFormatter.Error("UNKNOWN_TOOL", new { tool = toolName });
                        _interactionLog?.Add(toolName, MCPToolCallStatus.Error, error);
                        return error;
                    }

                    var profile = MCPToolExportPolicy.Parse(_settings.MCPToolExportProfile);
                    var profileKey = MCPToolExportPolicy.ToSettingValue(profile);
                    if (!MCPToolExportPolicy.IsToolAllowed(
                            toolName,
                            profile,
                            _settings.IsProfileConfigured(profileKey),
                            _settings.GetProfileTools(profileKey)))
                    {
                        var error = ToolResultFormatter.Error("TOOL_NOT_EXPOSED", new
                        {
                            tool = toolName,
                            profile = MCPToolExportPolicy.ToSettingValue(profile)
                        });
                        _interactionLog?.Add(toolName, MCPToolCallStatus.Error, error);
                        return error;
                    }

                    DomainReloadHandler.ResetResumeCounter();
                    _stateController.SetState(KitWrightState.ExecutingFunction);
                    DomainReloadHandler.SavePendingFunction(functionCall);

                    PluginDebugLogger.Log($"[KitWright MCP Server] Executing tool: {toolName}");
                    var result = await _invoker.InvokeAsync(functionCall);
                    DomainReloadHandler.CompletePendingFunction(_stateController);

                    var resultText = result ?? "Completed successfully";
                    _interactionLog?.Add(toolName,
                        ToolResultFormatter.IsError(resultText) ? MCPToolCallStatus.Error : MCPToolCallStatus.Success,
                        resultText);
                    return resultText;
                }
                catch (Exception ex)
                {
                    DomainReloadHandler.ClearPendingFunction();
                    _stateController.ClearState();
                    var exError = ToolResultFormatter.Error("TOOL_EXCEPTION",
                        new { tool = toolName, message = ex.Message });
                    Debug.LogError($"[KitWright MCP Server] Error executing tool '{toolName}': {ex.Message}\n{ex.StackTrace}");
                    _interactionLog?.Add(toolName, MCPToolCallStatus.Error, exError);
                    return exError;
                }
            }, ct);
        }

        private string ConvertArgumentToString(object value)
        {
            if (value == null) return string.Empty;
            if (value is string strValue) return strValue;
            if (value is bool boolValue) return boolValue ? "true" : "false";
            if (value is int || value is long || value is float || value is double) return value.ToString();
            if (value is Dictionary<string, object> dict) return SimpleJsonHelper.Serialize(dict);
            if (value is System.Collections.IList list)
            {
                var items = new List<object>();
                foreach (var item in list) items.Add(item);
                return SimpleJsonHelper.Serialize(items);
            }
            return value.ToString();
        }
    }
}
