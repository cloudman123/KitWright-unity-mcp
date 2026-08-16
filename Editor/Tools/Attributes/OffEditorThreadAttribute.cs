// Copyright (C) KitWright. Licensed under MIT.
using System;

namespace KitWright.Editor.Tools
{
    /// <summary>
    /// Runs the tool on the request thread instead of queueing it for the editor loop.
    /// Only for tools that touch no Unity API, because the point is to still answer while a
    /// modal dialog owns the editor loop and nothing queued can run.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    internal sealed class OffEditorThreadAttribute : Attribute { }
}
