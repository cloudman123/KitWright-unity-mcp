// Copyright (C) GameWright. Licensed under MIT.
using System.Collections.Generic;
using System.Threading.Tasks;
using DescriptionAttribute = System.ComponentModel.DescriptionAttribute;
using GameWright.Editor.Tools.Helpers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace GameWright.Editor.Tools.Builtins
{
    [ToolProvider("Batch")]
    internal static class BatchFunctions
    {
        [Description("Run multiple MCP tool calls sequentially in a single request, on the main thread, saving round-trips. " +
                     "Pass a JSON array of {\"name\": \"<tool_name>\", \"params\": {..}} objects. Each result is returned in order. " +
                     "By default a failing call stops the batch; set stop_on_error=false to continue past failures.")]
        public static async Task<object> BatchExecute(
            [ToolParam("JSON array of commands, e.g. [{\"name\":\"create_primitive\",\"params\":{\"primitive_type\":\"Cube\"}},{\"name\":\"get_hierarchy\",\"params\":{}}]")] string commands,
            [ToolParam("Stop the batch when a call fails (default true). If false, remaining calls still run.", Required = false)] bool stop_on_error = true)
        {
            JArray parsed;
            try
            {
                parsed = JArray.Parse(commands);
            }
            catch (System.Exception ex)
            {
                return Response.Error("INVALID_COMMANDS", new { message = ex.Message, expected = "a JSON array of {name, params} objects" });
            }

            if (parsed.Count == 0)
                return Response.Error("EMPTY_BATCH", new { message = "commands array is empty" });
            if (parsed.Count > 100)
                return Response.Error("BATCH_TOO_LARGE", new { count = parsed.Count, max = 100 });

            var invoker = new FunctionInvokerController();
            var results = new List<object>();
            bool aborted = false;

            for (int i = 0; i < parsed.Count; i++)
            {
                var name = (parsed[i] as JObject)?["name"]?.ToString();
                if (string.IsNullOrEmpty(name))
                {
                    results.Add(new { index = i, success = false, error = "MISSING_NAME" });
                    if (stop_on_error) { aborted = true; break; }
                    continue;
                }

                var fc = new FunctionCall
                {
                    FunctionName = name,
                    Parameters = ExtractParams(parsed[i]["params"])
                };

                // Await instead of blocking: async tools pump their state
                // machine on EditorApplication.update — a sync .GetAwaiter().GetResult() here
                // deadlocks the editor main thread against that update loop.
                var raw = await invoker.InvokeAsync(fc);
                var resultToken = TryParse(raw);
                bool ok = (resultToken as JObject)?["success"]?.Type == JTokenType.Boolean
                          && (resultToken as JObject)["success"].Value<bool>();

                results.Add(new { index = i, name, result = resultToken });

                if (!ok && stop_on_error) { aborted = true; break; }
            }

            return Response.Success(
                aborted ? $"Batch stopped after {results.Count} of {parsed.Count} command(s) due to an error." : $"Batch executed {results.Count} command(s).",
                new { count = results.Count, total = parsed.Count, aborted, results });
        }

        private static Dictionary<string, string> ExtractParams(JToken paramsToken)
        {
            var dict = new Dictionary<string, string>();
            if (!(paramsToken is JObject obj)) return dict;

            foreach (var prop in obj.Properties())
            {
                dict[prop.Name] = prop.Value.Type == JTokenType.String
                    ? prop.Value.ToString()
                    : prop.Value.ToString(Formatting.None);
            }
            return dict;
        }

        private static object TryParse(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return raw;
            try { return JToken.Parse(raw); }
            catch { return raw; }
        }
    }
}
