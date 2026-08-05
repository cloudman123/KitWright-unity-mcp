// Copyright (C) GameWright. Licensed under MIT.

using System;
using System.IO;
using UnityEngine;

namespace GameWright.Editor.Tools.Helpers
{
    internal static class PathSafety
    {
        public static bool IsInsideDirectory(string path, string directory)
        {
            var normalizedPath = Path.GetFullPath(path);
            var normalizedDirectory = EnsureTrailingSeparator(Path.GetFullPath(directory));
            return normalizedPath.StartsWith(normalizedDirectory, StringComparison.OrdinalIgnoreCase);
        }

        public static string ResolveProjectPath(string path)
        {
            if (Path.IsPathRooted(path)) return path;
            var projectRoot = Path.GetDirectoryName(Application.dataPath);
            return Path.Combine(projectRoot, path);
        }

        private static string EnsureTrailingSeparator(string path)
        {
            if (string.IsNullOrEmpty(path))
                return path;

            var last = path[path.Length - 1];
            if (last == Path.DirectorySeparatorChar || last == Path.AltDirectorySeparatorChar)
                return path;

            return path + Path.DirectorySeparatorChar;
        }
    }
}
