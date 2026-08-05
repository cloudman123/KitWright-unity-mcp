// Copyright (C) GameWright. Licensed under MIT.
using System;
using DescriptionAttribute = System.ComponentModel.DescriptionAttribute;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using GameWright.Editor.Tools.Helpers;
using UnityEditor;

namespace GameWright.Editor.Tools.Builtins
{
    [ToolProvider("Code")]
    internal static class CodeFunctions
    {
        [Description("Create a new C# script with the specified content")]
        public static string CreateScript(
            [ToolParam("Script file name (without .cs)")]
            string name,
            [ToolParam("C# source code content")] string content,
            [ToolParam("Path to save (e.g. 'Assets/Scripts/')", Required = false)]
            string save_path = "Assets/Scripts/")
        {
            if (!Directory.Exists(save_path))
                Directory.CreateDirectory(save_path);

            var fullPath = Path.Combine(save_path, $"{name}.cs");
            File.WriteAllText(fullPath, content);
            AssetDatabase.Refresh();

            return $"Created script '{name}.cs' at {fullPath}";
        }

        [Description("Get the SHA256 hash of a script file's current contents. " +
                     "Pass the returned sha256 as expected_sha256 to edit_script/patch_script so the edit is rejected " +
                     "if the file changed since you read it (prevents overwriting concurrent edits).")]
        [ReadOnlyTool]
        public static object GetScriptSha(
            [ToolParam("Path to the script file")] string path)
        {
            var fullPath = PathSafety.ResolveProjectPath(path);
            if (!File.Exists(fullPath))
                return Response.Error("SCRIPT_NOT_FOUND", new { path });

            var content = File.ReadAllText(fullPath);
            return Response.Success($"SHA256 for {path}.", new
            {
                path,
                sha256 = ComputeSha256(content),
                length = content.Length
            });
        }

        [Description("Edit/replace the contents of an existing script. " +
                     "Optionally pass expected_sha256 (from get_script_sha) to reject the write if the file changed since you read it.")]
        public static string EditScript(
            [ToolParam("Path to the script file")] string path,
            [ToolParam("New full content for the script")]
            string content,
            [ToolParam("SHA256 from get_script_sha; write is rejected with STALE_FILE if the file changed", Required = false)]
            string expected_sha256 = null)
        {
            var fullPath = PathSafety.ResolveProjectPath(path);
            if (!File.Exists(fullPath))
                return ToolResultFormatter.Error("SCRIPT_NOT_FOUND", new { path });

            var original = File.ReadAllText(fullPath);
            var staleError = CheckPrecondition(path, original, expected_sha256);
            if (staleError != null) return staleError;

            var braceError = CheckBraceRegression(path, original, content);
            if (braceError != null) return braceError;

            File.WriteAllText(fullPath, content);
            AssetDatabase.Refresh();

            return $"Updated script at {path} (sha256: {ComputeSha256(content)})";
        }

        [Description("Patch a script by finding and replacing specific text. " +
                     "Safer than edit_script for small changes since it doesn't require sending the entire file content. " +
                     "The old_text must match exactly (including whitespace and indentation). " +
                     "Optionally pass expected_sha256 (from get_script_sha) to reject the patch if the file changed since you read it.")]
        public static string PatchScript(
            [ToolParam("Path to the script file")] string path,
            [ToolParam("Exact text to find in the file")] string old_text,
            [ToolParam("Replacement text")] string new_text,
            [ToolParam("Replace all occurrences (default: false, only first)", Required = false)]
            bool replace_all = false,
            [ToolParam("SHA256 from get_script_sha; patch is rejected with STALE_FILE if the file changed", Required = false)]
            string expected_sha256 = null)
        {
            var fullPath = PathSafety.ResolveProjectPath(path);
            if (!File.Exists(fullPath))
                return ToolResultFormatter.Error("SCRIPT_NOT_FOUND", new { path });

            var content = File.ReadAllText(fullPath);

            var staleError = CheckPrecondition(path, content, expected_sha256);
            if (staleError != null) return staleError;

            if (!content.Contains(old_text))
                return ToolResultFormatter.Error("PATCH_TEXT_NOT_FOUND",
                    new { path, hint = "Make sure old_text matches exactly, including whitespace and indentation." });

            int occurrences = 0;
            int index = 0;
            while ((index = content.IndexOf(old_text, index, StringComparison.Ordinal)) >= 0)
            {
                occurrences++;
                index += old_text.Length;
            }

            string newContent;
            if (replace_all)
            {
                newContent = content.Replace(old_text, new_text);
            }
            else
            {
                int firstIndex = content.IndexOf(old_text, StringComparison.Ordinal);
                newContent = content.Substring(0, firstIndex) +
                             new_text +
                             content.Substring(firstIndex + old_text.Length);
            }

            var braceError = CheckBraceRegression(path, content, newContent);
            if (braceError != null) return braceError;

            File.WriteAllText(fullPath, newContent);
            AssetDatabase.Refresh();

            string replacedInfo = replace_all
                ? $"Replaced all {occurrences} occurrence(s)"
                : $"Replaced first occurrence (of {occurrences} total)";

            return $"Patched script at {path}. {replacedInfo}. (sha256: {ComputeSha256(newContent)})";
        }

        // Optimistic-lock check: reject when the caller's snapshot no longer matches the file on disk.
        private static string CheckPrecondition(string path, string currentContent, string expectedSha256)
        {
            if (string.IsNullOrEmpty(expectedSha256))
                return null;

            var currentSha = ComputeSha256(currentContent);
            if (expectedSha256.Equals(currentSha, StringComparison.OrdinalIgnoreCase))
                return null;

            return ToolResultFormatter.Error("STALE_FILE", new
            {
                path,
                expected_sha256 = expectedSha256,
                current_sha256 = currentSha,
                hint = "File changed since you read it. Re-read the file (or call get_script_sha) and resend the edit."
            });
        }

        // Only reject when the edit turns a balanced file into an unbalanced one, so files that
        // were already unbalanced (e.g. mid-refactor) can still be fixed with further edits.
        private static string CheckBraceRegression(string path, string originalContent, string newContent)
        {
            if (!TryGetBraceImbalance(originalContent, out _) && TryGetBraceImbalance(newContent, out var line))
            {
                int startLine = Math.Max(1, line - 5);
                return ToolResultFormatter.Error("UNBALANCED_BRACES", new
                {
                    path,
                    line,
                    hint = $"This edit makes braces unbalanced around line {line}. Re-read lines {startLine}-{line + 5} and resend a corrected edit."
                });
            }

            return null;
        }

        internal static string ComputeSha256(string contents)
        {
            using (var sha = SHA256.Create())
            {
                var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(contents ?? string.Empty));
                return BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
            }
        }

        // Counts { } outside strings/chars/comments. Returns true when unbalanced;
        // line = where the count first goes negative, or the last line for a missing close.
        internal static bool TryGetBraceImbalance(string text, out int line)
        {
            line = 0;
            if (string.IsNullOrEmpty(text))
                return false;

            int depth = 0, currentLine = 1;
            bool inString = false, inVerbatim = false, inChar = false, inSingleComment = false, inMultiComment = false;

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                char next = i + 1 < text.Length ? text[i + 1] : '\0';

                if (c == '\n')
                {
                    currentLine++;
                    inSingleComment = false;
                    continue;
                }

                if (inSingleComment) continue;
                if (inMultiComment)
                {
                    if (c == '*' && next == '/') { inMultiComment = false; i++; }
                    continue;
                }
                if (inString)
                {
                    if (inVerbatim)
                    {
                        if (c == '"' && next == '"') { i++; continue; }
                        if (c == '"') { inString = false; inVerbatim = false; }
                    }
                    else
                    {
                        if (c == '\\') { i++; continue; }
                        if (c == '"') inString = false;
                    }
                    continue;
                }
                if (inChar)
                {
                    if (c == '\\') { i++; continue; }
                    if (c == '\'') inChar = false;
                    continue;
                }

                switch (c)
                {
                    case '/':
                        if (next == '/') inSingleComment = true;
                        else if (next == '*') { inMultiComment = true; i++; }
                        break;
                    case '@':
                        if (next == '"') { inString = true; inVerbatim = true; i++; }
                        break;
                    case '"':
                        inString = true;
                        break;
                    case '\'':
                        inChar = true;
                        break;
                    case '{':
                        depth++;
                        break;
                    case '}':
                        depth--;
                        if (depth < 0)
                        {
                            line = currentLine;
                            return true;
                        }
                        break;
                }
            }

            if (depth > 0)
            {
                line = currentLine;
                return true;
            }

            return false;
        }

    }
}
