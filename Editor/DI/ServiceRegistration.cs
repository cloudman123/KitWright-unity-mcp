// Copyright (C) KitWright. Licensed under MIT.

using KitWright.Editor.MCP.Server;
using KitWright.Editor.Services;
using KitWright.Editor.Services.UnityLogs;
using KitWright.Editor.Settings;
using KitWright.Editor.State;
using KitWright.Editor.Threading;
using KitWright.Editor.Tools;

namespace KitWright.Editor.DI
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
