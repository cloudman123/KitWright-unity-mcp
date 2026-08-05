// Copyright (C) GameWright. Licensed under MIT.

using GameWright.Editor.MCP.Server;
using GameWright.Editor.Services;
using GameWright.Editor.Services.UnityLogs;
using GameWright.Editor.Settings;
using GameWright.Editor.State;
using GameWright.Editor.Threading;
using GameWright.Editor.Tools;

namespace GameWright.Editor.DI
{
    internal static class ServiceRegistration
    {
        public static ServiceCollection RegisterServices(this ServiceCollection services)
        {
            // Core Infrastructure (Singletons)
            services.AddSingleton<IApplicationPaths, ApplicationPaths>();
            services.AddSingleton<IEditorContextBuilder, EditorContextBuilder>();
            services.AddSingleton<ISettingsController, SettingsController>();
            services.AddSingleton<IEditorThreadHelper, EditorThreadHelper>();

            // Services (Singletons)
            services.AddSingleton<ICompilationService, CompilationService>();
            services.AddSingleton<UnityLogsRepository, UnityLogsRepository>();
            services.AddSingleton<FunctionInvokerController, FunctionInvokerController>();

            // MCP Server (Singleton)
            services.AddSingleton<MCPServerService, MCPServerService>();

            // State (Scoped)
            services.AddScoped<IStateController, StateController>();

            return services;
        }
    }
}
