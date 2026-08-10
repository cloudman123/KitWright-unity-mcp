// Copyright (C) KitWright. Licensed under MIT.

using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace KitWright.Editor.MCP.Server
{
    internal static class ProjectIdentity
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

        // Short, human-readable tail of the identity. Two projects can share a productName
        // (a clone, or the same branch checked out twice), so config entry names need this to
        // stay distinct — the full hash is too long to read in a client's config file.
        public const int PinLength = 8;

        public static string PinFromProjectPath(string projectPath)
        {
            var identity = FromProjectPath(projectPath);
            return identity.Length >= PinLength ? identity.Substring(0, PinLength) : identity;
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
