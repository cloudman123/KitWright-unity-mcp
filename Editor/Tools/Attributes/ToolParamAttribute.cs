// Copyright (C) KitWright. Licensed under MIT.
using System;

namespace KitWright.Editor.Tools
{
    [AttributeUsage(AttributeTargets.Parameter)]
    internal class ToolParamAttribute : Attribute
    {
        public string Description { get; }
        public bool Required { get; set; } = true;
        public string DefaultValue { get; set; }

        public ToolParamAttribute(string description)
        {
            Description = description;
        }
    }
}
