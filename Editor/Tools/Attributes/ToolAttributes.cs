// Copyright (C) KitWright. Licensed under MIT.
using System;

namespace KitWright.Editor.Tools
{
    /// Marks a static class whose public static methods are exposed as MCP tools.
    /// Public so project and third-party editor assemblies can declare their own tools;
    /// <see cref="ToolRegistry"/> scans every loaded assembly, not just this package.
    [AttributeUsage(AttributeTargets.Class)]
    public class ToolProviderAttribute : Attribute
    {
        public string Category { get; }

        public ToolProviderAttribute(string category = null)
        {
            Category = category;
        }
    }

    [AttributeUsage(AttributeTargets.Parameter)]
    public class ToolParamAttribute : Attribute
    {
        public string Description { get; }
        public bool Required { get; set; } = true;
        public string DefaultValue { get; set; }

        public ToolParamAttribute(string description)
        {
            Description = description;
        }
    }

    /// Functions with this attribute do not modify the scene or project.
    [AttributeUsage(AttributeTargets.Method)]
    public class ReadOnlyToolAttribute : Attribute { }

    /// Runs the tool on the request thread instead of queueing it for the editor loop.
    /// Only for tools that touch no Unity API, because the point is to still answer while a
    /// modal dialog owns the editor loop and nothing queued can run.
    [AttributeUsage(AttributeTargets.Method)]
    internal sealed class OffEditorThreadAttribute : Attribute { }
}
