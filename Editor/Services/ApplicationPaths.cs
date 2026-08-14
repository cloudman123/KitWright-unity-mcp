// Copyright (C) KitWright. Licensed under MIT.

using System.IO;
using UnityEngine;

namespace KitWright.Editor.Services
{
    internal interface IApplicationPaths
    {
        string ProjectPath { get; }
        string AssetsPath { get; }
    }

    internal class ApplicationPaths : IApplicationPaths
    {
        // Static twin for the call sites that are not DI-resolved (static tool providers,
        // UI panels), so the project root is derived in exactly one place.
        public static string ProjectRoot => Path.GetDirectoryName(Application.dataPath) ?? Application.dataPath;

        public string ProjectPath => ProjectRoot;
        public string AssetsPath => Application.dataPath;
    }
}
