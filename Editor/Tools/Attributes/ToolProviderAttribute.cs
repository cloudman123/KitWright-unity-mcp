// Copyright (C) KitWright. Licensed under MIT.
using System;

namespace KitWright.Editor.Tools
{
    /// <summary>
    /// Marks a static class whose public static methods are exposed as MCP tools.
    /// Public so project and third-party editor assemblies can declare their own tools;
    /// <see cref="ToolRegistry"/> scans every loaded assembly, not just this package.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class ToolProviderAttribute : Attribute
    {
        public string Category { get; }

        public ToolProviderAttribute(string category = null)
        {
            Category = category;
        }
    }
}
