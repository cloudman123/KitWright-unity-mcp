// Copyright (C) KitWright. Licensed under MIT.
using System;

namespace KitWright.Editor.Tools
{
    [AttributeUsage(AttributeTargets.Class)]
    internal class ToolProviderAttribute : Attribute
    {
        public string Category { get; }

        public ToolProviderAttribute(string category = null)
        {
            Category = category;
        }
    }
}
