// Copyright (C) KitWright. Licensed under MIT.

using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace KitWright.Editor.Tools.Helpers
{
    /// <summary>
    /// Standardized response wrapper for MCP tool returns. Tools that return
    /// <see cref="Response"/> objects (or any non-string object) get serialized
    /// to JSON by <c>FunctionInvoker</c> so MCP clients can reliably
    /// parse <c>{ success, message, data }</c> instead of free-form strings.
    ///
    /// Success: { success: true, message: "...", data?: {...} }
    /// Error:   { success: false, code: "...", error: "...", data?: {...} }
    /// </summary>
    internal static class Response
    {
        public static object Success(string message, object data = null)
        {
            if (data != null)
                return new { success = true, message, data };
            return new { success = true, message };
        }

        // Use for machine-parsable error codes (UPPERCASE_SNAKE_CASE) plus optional details.
        // The same string is echoed in both `code` and `error` fields so old clients still see a message.
        public static object Error(string errorCodeOrMessage, object data = null)
        {
            if (data != null)
                return new { success = false, code = errorCodeOrMessage, error = errorCodeOrMessage, data };
            return new { success = false, code = errorCodeOrMessage, error = errorCodeOrMessage };
        }
    }

    /// <summary>
    /// Pre-serialized <see cref="Response"/> errors, for call sites that must return a JSON
    /// string rather than an object.
    /// </summary>
    internal static class ToolResultFormatter
    {
        public static string Error(string code, object data = null)
        {
            try
            {
                return JsonConvert.SerializeObject(Response.Error(code, data));
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[KitWright] Failed to serialize tool error response: {ex.Message}");
                return JsonConvert.SerializeObject(Response.Error(code, new { serialization_error = ex.Message }));
            }
        }

        public static string ErrorMessage(string code, string message)
        {
            return Error(code, new { message });
        }

        public static string Exception(Exception ex)
        {
            return Error("TOOL_EXCEPTION", new { message = ex?.Message ?? "Unknown exception" });
        }

        public static bool IsError(string result)
        {
            if (string.IsNullOrEmpty(result))
                return false;

            try
            {
                var obj = JObject.Parse(result);
                return obj.TryGetValue("success", out var successToken) &&
                    successToken.Type == JTokenType.Boolean &&
                    !successToken.Value<bool>();
            }
            catch
            {
                return false;
            }
        }
    }
}
