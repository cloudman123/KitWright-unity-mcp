// Copyright (C) GameWright. Licensed under MIT.

using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace GameWright.Editor.MCP.Server
{
    internal static class GameWrightProjectIdentity
    {
        public const string IdentityVersion = "project-path-sha256-v1";

        public static string FromProjectPath(string projectPath)
        {
            var normalized = NormalizeProjectPath(projectPath);
            if (string.IsNullOrEmpty(normalized))
                return string.Empty;

            using (var sha256 = SHA256.Create())
            {
                var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(normalized));
                return BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
            }
        }

        private static string NormalizeProjectPath(string projectPath)
        {
            if (string.IsNullOrWhiteSpace(projectPath))
                return string.Empty;

            var fullPath = Path.GetFullPath(projectPath)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Replace('\\', '/');

            return fullPath.ToLowerInvariant();
        }
    }
}
