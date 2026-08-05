// Copyright (C) GameWright. Licensed under MIT.

using System;
using UnityEngine.UIElements;

namespace GameWright.Editor.MCP.Server
{
    internal interface IMCPWindowPanel : IDisposable
    {
        void Build(VisualElement container);
    }
}
