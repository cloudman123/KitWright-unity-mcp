// Copyright (C) GameWright. Licensed under MIT.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GameWright.Editor.MCP;
using GameWright.Editor.Services;
using GameWright.Editor.Settings;
using GameWright.Editor.State;
using GameWright.Editor.Threading;
using GameWright.Editor.Tools;
using GameWright.Editor.Tools.Builtins;
using GameWright.Editor.Tools.Helpers;
using UnityEditor;
using UnityEngine;

namespace GameWright.Editor.MCP.Server
{
    /// <summary>
    /// Main MCP server service singleton.
    /// Manages server lifecycle, coordinates transport, handler, exporter, and bridge.
    /// </summary>
    internal class MCPServerService : IDisposable
    {
        private const int ToolCallTimeoutMs = 30000;

        private readonly ISettingsController _settings;
        private readonly IEditorThreadHelper _threadHelper;
        private readonly IStateController _stateController;
        private readonly IEditorContextBuilder _contextBuilder;
        private readonly IApplicationPaths _applicationPaths;
        private readonly ICompilationService _compilationService;
        private readonly FunctionInvokerController _invoker;
        private readonly object _lifecycleLock = new object();

        private IMCPTransport _transport;
        private MCPRequestHandler _requestHandler;
        private MCPResourceProvider _resourceProvider;
        private Task<bool> _startTask;
        private CancellationTokenSource _startCts;
        private int _lifecycleVersion;
        private bool _isRunning;
        private bool _disposed;
        private bool _recoveryChecked;
        private bool _restartScheduled;
        private bool _restartInProgress;
        private string _toolExposureSetting;
        private string _transportSetting;

        public bool IsRunning
        {
            get
            {
                lock (_lifecycleLock)
                {
                    return _isRunning;
                }
            }
        }
        public bool IsAttachedToExistingTransport
        {
            get
            {
                lock (_lifecycleLock)
                {
                    return _transport?.IsAttachedToExistingServer == true;
                }
            }
        }
        public int Port { get; private set; }
        public MCPInteractionLog InteractionLog { get; }

        public MCPServerService(
            ISettingsController settings,
            IEditorThreadHelper threadHelper,
            IStateController stateController,
            IEditorContextBuilder contextBuilder,
            IApplicationPaths applicationPaths,
            ICompilationService compilationService,
            FunctionInvokerController invoker)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _threadHelper = threadHelper ?? throw new ArgumentNullException(nameof(threadHelper));
            _stateController = stateController ?? throw new ArgumentNullException(nameof(stateController));
            _contextBuilder = contextBuilder;
            _applicationPaths = applicationPaths ?? throw new ArgumentNullException(nameof(applicationPaths));
            _compilationService = compilationService ?? throw new ArgumentNullException(nameof(compilationService));
            _invoker = invoker ?? throw new ArgumentNullException(nameof(invoker));

            Port = _settings.MCPServerPort;
            _toolExposureSetting = BuildToolExposureSetting();
            _transportSetting = BuildTransportSetting();
            InteractionLog = new MCPInteractionLog(_settings.ActivityLogCapacity);
            _settings.OnSettingsChanged += () => InteractionLog?.SetCapacity(_settings.ActivityLogCapacity);
            _settings.OnSettingsChanged += HandleSettingsChanged;
            DomainReloadHandler.Register(_stateController);
        }

        public Task<bool> StartAsync(CancellationToken ct = default)
        {
            if (Application.isBatchMode)
            {
                Debug.LogWarning("[GameWright MCP Server] Skipping server start in Unity batch mode process.");
                return Task.FromResult(false);
            }

            bool cleanupStaleState = false;
            lock (_lifecycleLock)
            {
                if (_disposed)
                {
                    Debug.LogWarning("[GameWright MCP Server] Cannot start: service is disposed");
                    return Task.FromResult(false);
                }

                if (_isRunning && _transport?.IsRunning == true)
                {
                    PluginDebugLogger.Log("[GameWright MCP Server] Server is already running");
                    return Task.FromResult(true);
                }

                if (_startTask != null)
                {
                    PluginDebugLogger.Log("[GameWright MCP Server] Server start is already in progress");
                    return _startTask;
                }

                cleanupStaleState = _isRunning || _transport != null || _requestHandler != null || _resourceProvider != null;
            }

            if (cleanupStaleState)
            {
                Debug.LogWarning("[GameWright MCP Server] Server lifecycle state was stale; cleaning up before restart.");
                StopSync();
            }

            lock (_lifecycleLock)
            {
                if (_disposed)
                {
                    Debug.LogWarning("[GameWright MCP Server] Cannot start: service is disposed");
                    return Task.FromResult(false);
                }

                if (_isRunning && _transport?.IsRunning == true)
                {
                    PluginDebugLogger.Log("[GameWright MCP Server] Server is already running");
                    return Task.FromResult(true);
                }

                if (_startTask != null)
                {
                    PluginDebugLogger.Log("[GameWright MCP Server] Server start is already in progress");
                    return _startTask;
                }

                _lifecycleVersion++;
                _startCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                var startCts = _startCts;
                var startTask = StartCoreAsync(_lifecycleVersion, startCts);
                _startTask = startTask;
                _ = startTask.ContinueWith(
                    _ => ClearCompletedStartTask(startTask, startCts),
                    CancellationToken.None,
                    TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default);
                return startTask;
            }
        }

        private async Task<bool> StartCoreAsync(int lifecycleVersion, CancellationTokenSource startCts)
        {
            IMCPTransport transport = null;
            MCPResourceProvider resourceProvider = null;
            var assigned = false;
            try
            {
                var startupPort = ResolveStartupPort(NormalizePort(_settings.MCPServerPort));
                var toolExposureSetting = BuildToolExposureSetting();
                PluginDebugLogger.Log("[GameWright MCP Server] Starting server...");

                var serverName = "GameWright MCP Server - " + Application.productName;
                var projectIdentity = GameWrightProjectIdentity.FromProjectPath(_applicationPaths.ProjectPath);
                transport = CreateTransport(startupPort, serverName, projectIdentity);
                var toolExporter = new MCPToolExporter(_settings);
                MCPToolListChangeNotifier.CheckForChanges(toolExporter);
                var executionBridge = new MCPExecutionBridge(_threadHelper, _settings, _stateController, _invoker, InteractionLog);
                resourceProvider = new MCPResourceProvider(_contextBuilder, _applicationPaths, InteractionLog);
                var promptProvider = new MCPPromptProvider(Application.productName, _applicationPaths.ProjectPath);
                var requestHandler = new MCPRequestHandler(
                    toolExporter,
                    executionBridge,
                    resourceProvider,
                    promptProvider,
                    serverName,
                    PackageVersionUtility.CurrentVersion,
                    projectIdentity);

                transport.OnRequestReceived += HandleRequestReceived;

                lock (_lifecycleLock)
                {
                    if (!_disposed && lifecycleVersion == _lifecycleVersion)
                    {
                        Port = startupPort;
                        _toolExposureSetting = toolExposureSetting;
                        _transportSetting = BuildTransportSetting();
                        _transport = transport;
                        _resourceProvider = resourceProvider;
                        _requestHandler = requestHandler;
                        assigned = true;
                    }
                }

                if (!assigned)
                {
                    DisposeUnassignedStartState(transport, resourceProvider);
                    return false;
                }

                var started = await transport.StartAsync(startCts.Token);
                if (started)
                {
                    var shouldDisposeStartedTransport = false;
                    lock (_lifecycleLock)
                    {
                        if (_disposed || lifecycleVersion != _lifecycleVersion || !ReferenceEquals(_transport, transport))
                            shouldDisposeStartedTransport = true;
                        else
                            _isRunning = true;
                    }

                    if (shouldDisposeStartedTransport)
                    {
                        CleanupServerState(transport);
                        return false;
                    }

                    if (transport.IsAttachedToExistingServer)
                    {
                        PluginDebugLogger.Log($"[GameWright] MCP Server attached to existing listener on http://127.0.0.1:{Port}/");
                    }
                    else
                    {
                        PluginDebugLogger.Log($"[GameWright] MCP Server started on http://127.0.0.1:{Port}/ If this tool saves you time, please consider giving it a Star on GitHub: https://github.com/gamewrightdev/gamewright-unity-mcp");
                    }
                    MCPClientConfigAutoRewrite.Schedule(Port);
                    ExternalSyncRecoveryTracker.TryCompletePendingRecovery();
                    CheckForInterruptedExecution();
                    return true;
                }

                CleanupServerState(transport);
                Debug.LogError("[GameWright MCP Server] Failed to start transport");
                return false;
            }
            catch (OperationCanceledException)
            {
                if (assigned)
                    CleanupServerState(transport);
                else
                    DisposeUnassignedStartState(transport, resourceProvider);
                return false;
            }
            catch (Exception ex)
            {
                if (assigned)
                    CleanupServerState(transport);
                else
                    DisposeUnassignedStartState(transport, resourceProvider);
                Debug.LogError($"[GameWright MCP Server] Failed to start: {ex.Message}\n{ex.StackTrace}");
                return false;
            }
        }

        private void ClearCompletedStartTask(Task<bool> completedTask, CancellationTokenSource startCts)
        {
            lock (_lifecycleLock)
            {
                if (ReferenceEquals(_startTask, completedTask))
                    _startTask = null;

                if (ReferenceEquals(_startCts, startCts))
                    _startCts = null;
            }

            startCts.Dispose();
        }

        public Task StopAsync()
        {
            StopSync();
            return Task.CompletedTask;
        }

        /// <summary>
        /// Synchronously stop the server. Required during
        /// <c>AssemblyReloadEvents.beforeAssemblyReload</c> and from <see cref="Dispose"/>:
        /// Unity unloads the AppDomain immediately after these callbacks return and does not
        /// await fire-and-forget tasks, which would leave the transport bound to the port.
        /// </summary>
        public void StopSync()
        {
            CancellationTokenSource startCtsToCancel;
            lock (_lifecycleLock)
            {
                _lifecycleVersion++;
                startCtsToCancel = _startCts;
                _startCts = null;
                _startTask = null;
            }

            startCtsToCancel?.Cancel();

            if (!CleanupServerState())
                return;

            try
            {
                PluginDebugLogger.Log("[GameWright] MCP Server stopped");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GameWright MCP Server] Error stopping server: {ex.Message}");
            }
        }

        private bool CleanupServerState(IMCPTransport expectedTransport = null)
        {
            IMCPTransport transportToDispose;
            MCPResourceProvider resourceProviderToDispose;
            bool hadState;

            lock (_lifecycleLock)
            {
                if (expectedTransport != null &&
                    _transport != null &&
                    !ReferenceEquals(_transport, expectedTransport))
                {
                    return false;
                }

                transportToDispose = _transport ?? expectedTransport;
                resourceProviderToDispose = _resourceProvider;
                hadState = _isRunning || _transport != null || _requestHandler != null || _resourceProvider != null || expectedTransport != null;

                _transport = null;
                _requestHandler = null;
                _resourceProvider = null;
                _isRunning = false;
            }

            if (transportToDispose != null)
            {
                transportToDispose.OnRequestReceived -= HandleRequestReceived;
                transportToDispose.Stop();
                transportToDispose.Dispose();
            }

            resourceProviderToDispose?.Dispose();
            return hadState;
        }

        private void DisposeUnassignedStartState(IMCPTransport transport, MCPResourceProvider resourceProvider)
        {
            if (transport != null)
            {
                transport.OnRequestReceived -= HandleRequestReceived;
                transport.Stop();
                transport.Dispose();
            }

            resourceProvider?.Dispose();
        }

        private async void HandleRequestReceived(MCPRequest request, Action<MCPResponse> sendResponse)
        {
            try
            {
                MCPRequestHandler requestHandler;
                lock (_lifecycleLock)
                {
                    requestHandler = _requestHandler;
                }

                if (requestHandler == null)
                {
                    sendResponse(new MCPResponse
                    {
                        Id = request?.Id,
                        Error = new MCPError { Code = -32000, Message = "MCP server is stopping or not ready." }
                    });
                    return;
                }

                // Metadata requests skip the editor thread so reconnect works while it's throttled.
                if (!RequiresEditorThread(request?.Method))
                {
                    var metaResponse = await requestHandler.HandleRequestAsync(request, default);
                    sendResponse(metaResponse);
                    return;
                }

                var editorThreadTask = _threadHelper.ExecuteAsyncOnEditorThreadAsync(
                    async () =>
                    {
                        var redeliveryResponse = TryCreateBrokerRedeliveryResponse(request);
                        if (redeliveryResponse != null)
                            return redeliveryResponse;

                        return await requestHandler.HandleRequestAsync(request, default);
                    });

                var completed = await Task.WhenAny(editorThreadTask, Task.Delay(ToolCallTimeoutMs));
                if (completed != editorThreadTask)
                {
                    // Editor thread job keeps running (a sync method.Invoke can't be aborted mid-flight);
                    // this only stops the client from hanging on a request that will never return in time.
                    sendResponse(new MCPResponse
                    {
                        Id = request?.Id,
                        Error = new MCPError { Code = -32001, Message = $"Tool call timed out after {ToolCallTimeoutMs}ms." }
                    });
                    return;
                }

                sendResponse(editorThreadTask.Result);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GameWright MCP Server] Error handling request: {ex.Message}");
                sendResponse(new MCPResponse
                {
                    Id = request?.Id,
                    Error = new MCPError { Code = -32603, Message = $"Internal error: {ex.Message}" }
                });
            }
        }

        // Only tools/call touches live scene/asset state; metadata requests skip the editor thread.
        private static bool RequiresEditorThread(string method)
        {
            return string.Equals(method, "tools/call", StringComparison.Ordinal)
                || string.Equals(method, "resources/read", StringComparison.Ordinal);
        }

        private void HandleSettingsChanged()
        {
            if (_disposed) return;

            var portChanged = _settings.MCPServerPort != Port;
            var toolExposureSetting = BuildToolExposureSetting();
            var transportSetting = BuildTransportSetting();
            var toolExposureChanged = !string.Equals(toolExposureSetting, _toolExposureSetting, StringComparison.Ordinal);
            var transportChanged = !string.Equals(transportSetting, _transportSetting, StringComparison.Ordinal);

            if ((portChanged || toolExposureChanged || transportChanged) && _isRunning)
            {
                PluginDebugLogger.Log("[GameWright MCP Server] Server settings changed, restarting MCP transport...");
                Port = _settings.MCPServerPort;
                _toolExposureSetting = toolExposureSetting;
                _transportSetting = transportSetting;
                ScheduleRestart();
            }
        }

        private const int PortFallbackRange = 10;

        // Multi-editor support: when the configured port is held by another process
        // (another Unity editor's MCP server, or an orphaned listener from a previous
        // domain), fall forward to the next free port instead of failing to start.
        private int ResolveStartupPort(int basePort)
        {
            if (_settings.MCPBrokerModeEnabled &&
                MCPBrokerProcessManager.TryGetConnectionInfo(basePort, out _))
            {
                return basePort;
            }

            for (var offset = 0; offset < PortFallbackRange; offset++)
            {
                var candidate = basePort + offset;
                if (!IsPortBindable(candidate))
                    continue;

                if (offset > 0)
                {
                    Debug.LogWarning(
                        $"[GameWright MCP Server] Port {basePort} is in use by another process; " +
                        $"using port {candidate} instead. Update your MCP client configuration if needed.");
                }
                return candidate;
            }

            return basePort;
        }

        private static bool IsPortBindable(int port)
        {
            try
            {
                var probe = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, port);
                probe.Start();
                probe.Stop();
                return true;
            }
            catch
            {
                return false;
            }
        }

        private IMCPTransport CreateTransport(int startupPort, string serverName, string projectIdentity)
        {
            if (_settings.MCPBrokerModeEnabled)
            {
                if (MCPBrokerProcessManager.EnsureRunning(startupPort, _settings.MCPBrokerMonoPath) &&
                    MCPBrokerProcessManager.TryGetConnectionInfo(startupPort, out var broker))
                {
                    return new MCPBrokerClientTransport(startupPort, broker.Token);
                }

                Debug.LogWarning(
                    "[GameWright MCP Server] Broker mode requested but broker could not start (" +
                    (MCPBrokerProcessManager.LastError ?? "unknown error") +
                    "); falling back to in-process HTTP transport.");
            }
            else
            {
                MCPBrokerProcessManager.Stop();
            }

            return new HttpMCPTransport(startupPort, serverName, projectIdentity);
        }

        private string BuildToolExposureSetting()
        {
            var parts = new List<string> { _settings.MCPToolExportProfile ?? string.Empty };
            foreach (var profile in MCPToolExportPolicy.AllProfiles)
            {
                var configured = _settings.IsProfileConfigured(profile);
                parts.Add($"{profile}={(configured ? "custom" : "default")}");
                parts.Add(string.Join(",", _settings.GetProfileTools(profile) ?? Array.Empty<string>()));
            }
            return string.Join("|", parts);
        }

        private string BuildTransportSetting()
        {
            return string.Join("|",
                _settings.MCPBrokerModeEnabled ? "broker=on" : "broker=off",
                _settings.MCPBrokerMonoPath ?? string.Empty);
        }

        internal static MCPResponse TryCreateBrokerRedeliveryResponse(MCPRequest request)
        {
            if (request == null ||
                !request.IsBrokerRedelivery ||
                !string.Equals(request.Method, "tools/call", StringComparison.Ordinal))
            {
                return null;
            }

            var toolName = GetToolName(request);
            if (string.Equals(toolName, "get_reload_recovery_status", StringComparison.OrdinalIgnoreCase))
                return null;

            var recovery = DomainReloadHandler.GetLastRecoveryInfo(false);
            string summary;
            bool isError;

            if (recovery != null &&
                (string.IsNullOrEmpty(toolName) ||
                 string.Equals(recovery.ToolName, toolName, StringComparison.OrdinalIgnoreCase)) &&
                (DateTime.Now - recovery.Timestamp).TotalMinutes <= 10)
            {
                summary =
                    "Broker mode recovered a tool call that was interrupted by Unity domain reload.\n" +
                    "Tool: " + recovery.ToolName + "\n" +
                    "Status: " + recovery.Status + "\n" +
                    recovery.Summary;
                isError = string.Equals(recovery.Status, MCPToolCallStatus.Error.ToString(), StringComparison.OrdinalIgnoreCase);
            }
            else
            {
                summary =
                    "Tool '" + (string.IsNullOrEmpty(toolName) ? "unknown" : toolName) +
                    "' was interrupted by Unity domain reload. Broker mode kept the HTTP request alive, " +
                    "but the original Unity AppDomain was unloaded before it could send a response. " +
                    "The tool was not re-run automatically to avoid duplicate side effects. " +
                    "Call get_reload_recovery_status, then retry only if more work is needed.";
                isError = true;
            }

            return new MCPResponse
            {
                Id = request.Id,
                Result = new Dictionary<string, object>
                {
                    ["content"] = new List<Dictionary<string, object>>
                    {
                        new Dictionary<string, object>
                        {
                            ["type"] = "text",
                            ["text"] = summary
                        }
                    },
                    ["isError"] = isError
                }
            };
        }

        private static string GetToolName(MCPRequest request)
        {
            if (request?.Params == null)
                return string.Empty;

            return request.Params.TryGetValue("name", out var value) ? value?.ToString() ?? string.Empty : string.Empty;
        }


        private static int NormalizePort(int port)
        {
            return port > 0 ? port : 8765;
        }

        private void ScheduleRestart()
        {
            if (_disposed || _restartScheduled)
                return;

            _restartScheduled = true;
            EditorApplication.update -= RestartTransportAfterSettingsChange;
            EditorApplication.delayCall -= RestartTransportAfterSettingsChange;
            EditorApplication.delayCall += RestartTransportAfterSettingsChange;
            EditorApplication.update += RestartTransportAfterSettingsChange;
        }

        private async void RestartTransportAfterSettingsChange()
        {
            EditorApplication.update -= RestartTransportAfterSettingsChange;
            EditorApplication.delayCall -= RestartTransportAfterSettingsChange;
            _restartScheduled = false;

            if (_disposed)
                return;

            if (_restartInProgress)
            {
                ScheduleRestart();
                return;
            }

            _restartInProgress = true;
            try
            {
                await StopAsync();

                if (_disposed)
                    return;

                ScheduleStartAfterSettingsChange();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GameWright MCP Server] Failed while restarting after settings change: {ex.Message}");
                _restartInProgress = false;
            }
        }

        private void ScheduleStartAfterSettingsChange()
        {
            EditorApplication.update -= StartTransportAfterSettingsChange;
            EditorApplication.delayCall -= StartTransportAfterSettingsChange;
            EditorApplication.delayCall += StartTransportAfterSettingsChange;
            EditorApplication.update += StartTransportAfterSettingsChange;
        }

        private async void StartTransportAfterSettingsChange()
        {
            EditorApplication.update -= StartTransportAfterSettingsChange;
            EditorApplication.delayCall -= StartTransportAfterSettingsChange;

            try
            {
                if (!_disposed)
                {
                    HttpMCPTransport.ArmOrphanReclaim();
                    await StartAsync();
                }
            }
            finally
            {
                _restartInProgress = false;
                if (_restartScheduled && !_disposed)
                    EditorApplication.delayCall += RestartTransportAfterSettingsChange;
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _settings.OnSettingsChanged -= HandleSettingsChanged;
            StopSync();
        }

        private void CheckForInterruptedExecution()
        {
            if (_recoveryChecked)
                return;

            _recoveryChecked = true;

            var interrupted = DomainReloadHandler.ConsumeInterruptedState();
            if (interrupted == null)
                return;

            if (!DomainReloadHandler.CanAutoResume())
            {
                var summary = interrupted.GetDescription() +
                              " Auto-recovery paused after too many consecutive recompilations. Retry the tool manually.";
                PublishRecoverySummary(interrupted, summary, MCPToolCallStatus.Error);
                DomainReloadHandler.ResetResumeCounter();
                return;
            }

            DomainReloadHandler.RecordAutoResume();
            WaitForCompilationThen(() =>
            {
                _stateController.ClearState();

                var summary = interrupted.GetDescription();
                if (IsSyncExternalChanges(interrupted))
                {
                    var compilationSummary = BuildSyncExternalChangesRecoverySummary();
                    summary += "\n" + compilationSummary.Summary;
                    PublishRecoverySummary(interrupted, summary, compilationSummary.Status);
                    return;
                }

                summary += " The MCP server recovered after reload. Re-run the tool if more work is needed.";
                PublishRecoverySummary(interrupted, summary, MCPToolCallStatus.Interrupted);
            });
        }

        private bool IsSyncExternalChanges(DomainReloadHandler.InterruptedState interrupted)
        {
            return string.Equals(
                interrupted?.PendingFunction?.FunctionName,
                "request_recompile",
                StringComparison.OrdinalIgnoreCase);
        }

        private (string Summary, MCPToolCallStatus Status) BuildSyncExternalChangesRecoverySummary()
        {
            var issues = _compilationService.GetCompilationErrors(includeWarnings: true);
            var hasIssues = !string.Equals(issues, "No compilation errors or warnings detected.", StringComparison.Ordinal) &&
                            !string.Equals(issues, "No compilation errors detected.", StringComparison.Ordinal);

            if (hasIssues)
            {
                return ("External changes were imported, but compilation reported issues.\n" + issues, MCPToolCallStatus.Error);
            }

            return ("External changes were imported and script compilation finished successfully after domain reload.", MCPToolCallStatus.Success);
        }

        private void PublishRecoverySummary(
            DomainReloadHandler.InterruptedState interrupted,
            string summary,
            MCPToolCallStatus status)
        {
            var toolName = interrupted.PendingFunction?.FunctionName;
            if (string.IsNullOrEmpty(toolName))
                toolName = "domain_reload";

            DomainReloadHandler.StoreRecoveryInfo(toolName, status.ToString(), summary);
            InteractionLog.Add(toolName, status, summary);

            if (status == MCPToolCallStatus.Success || status == MCPToolCallStatus.Interrupted)
                PluginDebugLogger.Log($"[GameWright MCP Server] Recovery completed for '{toolName}'. {summary}");
            else
                Debug.LogWarning($"[GameWright MCP Server] Recovery detected for '{toolName}'. {summary}");
        }

        internal static MCPToolCallStatus DetermineInterruptedToolRecoveryStatus(string scriptResult)
        {
            if (ToolResultFormatter.IsError(scriptResult))
                return MCPToolCallStatus.Error;

            return string.IsNullOrEmpty(scriptResult)
                ? MCPToolCallStatus.Interrupted
                : MCPToolCallStatus.Success;
        }

        private static void WaitForCompilationThen(Action onReady)
        {
            if (!EditorApplication.isCompiling)
            {
                EditorApplication.delayCall += () => onReady();
                return;
            }

            void CheckCompilation()
            {
                if (EditorApplication.isCompiling)
                    return;

                EditorApplication.update -= CheckCompilation;
                EditorApplication.delayCall += () => onReady();
            }

            EditorApplication.update += CheckCompilation;
        }
    }
}
