// Copyright (C) GameWright. Licensed under MIT.

using System.IO;
using UnityEngine;

namespace GameWright.Editor.Services
{
    internal interface IApplicationPaths
    {
        string ProjectPath { get; }
        string AssetsPath { get; }
    }

    internal class ApplicationPaths : IApplicationPaths
    {
        public string ProjectPath => Path.GetDirectoryName(Application.dataPath) ?? Application.dataPath;
        public string AssetsPath => Application.dataPath;
    }
}
