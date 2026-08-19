// Copyright (C) KitWright. Licensed under MIT.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using KitWright.Editor.DI;
using KitWright.Editor.Settings;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace KitWright.Editor.MCP.Server.Security
{
    /// <summary>
    /// First-connect approval for MCP clients. Loopback alone lets any local process call
    /// every tool (including execute_code and write_file); this gate identifies the client
    /// process behind the TCP connection and asks the user once per executable.
    /// </summary>
    [InitializeOnLoad]
    internal static class ClientApprovalGate
    {
        // Test seams.
        internal static Func<bool> RequireApprovalOverride;
        internal static Func<int, int, TcpClientProcessResolver.ClientProcessInfo> ResolverOverride;

        private static readonly object s_lock = new object();
        private static readonly Dictionary<string, Task<bool>> s_pendingPrompts =
            new Dictionary<string, Task<bool>>(StringComparer.OrdinalIgnoreCase);

        private static readonly SynchronizationContext s_mainContext;
        private static readonly bool s_isBatchMode;
        private static readonly int s_editorPid;

        static ClientApprovalGate()
        {
            s_mainContext = SynchronizationContext.Current;
            s_isBatchMode = Application.isBatchMode;
            s_editorPid = Process.GetCurrentProcess().Id;
        }

        public static Task<bool> AuthorizeAsync(TcpClient client, int serverPort)
        {
            if (!RequireApproval() || s_isBatchMode)
                return Task.FromResult(true);

            TcpClientProcessResolver.ClientProcessInfo info;
            try
            {
                var clientPort = ((IPEndPoint)client.Client.RemoteEndPoint).Port;
                info = (ResolverOverride ?? TcpClientProcessResolver.Resolve)(clientPort, serverPort);
            }
            catch
            {
                info = null;
            }

            // The editor calling its own server (in-editor tests, broker) needs no prompt.
            if (info != null && info.Pid == s_editorPid)
                return Task.FromResult(true);

            var identity = info?.ExecutablePath ?? info?.ProcessName ?? "unidentified process";
            // The stdio broker is spawned by this package with the mono path from settings.
            if (IsConfiguredBrokerPath(identity) || ClientApprovalStore.IsApproved(identity))
                return Task.FromResult(true);

            return PromptAsync(identity, info);
        }

        private static Task<bool> PromptAsync(string identity, TcpClientProcessResolver.ClientProcessInfo info)
        {
            lock (s_lock)
            {
                if (s_pendingPrompts.TryGetValue(identity, out var pending))
                    return pending;

                var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                s_pendingPrompts[identity] = tcs.Task;

                if (s_mainContext == null)
                {
                    Finish(identity, tcs, false);
                    return tcs.Task;
                }

                // ponytail: modal DisplayDialog blocks the editor loop until clicked; upgrade to a
                // non-modal approval window with a timeout if unattended connects become common.
                s_mainContext.Post(_ =>
                {
                    bool approved;
                    try
                    {
                        var name = info?.ProcessName ?? "Unknown process";
                        var detail = info?.ExecutablePath ?? "(could not identify the executable; approving allows every unidentified local process)";
                        approved = EditorUtility.DisplayDialog(
                            "KitWright MCP: new client",
                            $"\"{name}\" is connecting to this project's MCP server.\n\n{detail}\n\nAllow it to call Unity editor tools? This is remembered for all projects.",
                            "Allow",
                            "Deny");
                    }
                    catch
                    {
                        approved = false;
                    }

                    if (approved)
                        ClientApprovalStore.Approve(identity);
                    Finish(identity, tcs, approved);
                }, null);

                return tcs.Task;
            }
        }

        private static void Finish(string identity, TaskCompletionSource<bool> tcs, bool approved)
        {
            lock (s_lock)
                s_pendingPrompts.Remove(identity);
            tcs.TrySetResult(approved);
        }

        private static bool IsConfiguredBrokerPath(string identity)
        {
            var settings = RootScopeServices.Services?.GetService(typeof(SettingsController)) as SettingsController;
            var brokerPath = settings?.MCPBrokerMonoPath;
            return !string.IsNullOrEmpty(brokerPath) &&
                   string.Equals(identity, brokerPath, StringComparison.OrdinalIgnoreCase);
        }

        private static bool RequireApproval()
        {
            if (RequireApprovalOverride != null)
                return RequireApprovalOverride();

            var settings = RootScopeServices.Services?.GetService(typeof(SettingsController)) as SettingsController;
            return settings?.RequireClientApprovalEnabled ?? true;
        }
    }

    /// <summary>
    /// Per-user list of client executables approved to call this MCP server.
    /// Stored beside the instance registry so one approval covers every project.
    /// </summary>
    internal static class ClientApprovalStore
    {
        // Test seam: point the store at a temp directory instead of the user profile.
        internal static string RootOverride;

        private static readonly object s_lock = new object();

        // Windows paths are case-insensitive; identities are exe paths.
        private static readonly StringComparer PathComparer = StringComparer.OrdinalIgnoreCase;

        public static bool IsApproved(string identity)
        {
            if (string.IsNullOrEmpty(identity))
                return false;

            lock (s_lock)
                return Load().Contains(identity, PathComparer);
        }

        public static void Approve(string identity)
        {
            if (string.IsNullOrEmpty(identity))
                return;

            lock (s_lock)
            {
                var entries = Load();
                if (entries.Contains(identity, PathComparer))
                    return;

                entries.Add(identity);
                Save(entries);
            }
        }

        private static string FilePath =>
            Path.Combine(
                RootOverride ?? Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KitWright"),
                "approved-clients.json");

        private static List<string> Load()
        {
            try
            {
                if (!File.Exists(FilePath))
                    return new List<string>();

                return JsonConvert.DeserializeObject<List<string>>(File.ReadAllText(FilePath)) ?? new List<string>();
            }
            catch
            {
                return new List<string>();
            }
        }

        private static void Save(List<string> entries)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                File.WriteAllText(FilePath, JsonConvert.SerializeObject(entries, Formatting.Indented));
            }
            catch (Exception ex)
            {
                PluginDebugLogger.Log($"[KitWright] Could not save approved clients: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Resolves which local process owns the client side of a loopback TCP connection,
    /// via GetExtendedTcpTable. Windows-only; other platforms return null and the caller
    /// treats the client as unidentified.
    /// </summary>
    internal static class TcpClientProcessResolver
    {
        internal sealed class ClientProcessInfo
        {
            public int Pid;
            public string ExecutablePath;
            public string ProcessName;
        }

        public static ClientProcessInfo Resolve(int clientPort, int serverPort)
        {
#if UNITY_EDITOR_WIN
            try
            {
                var pid = FindOwningPid(clientPort, serverPort);
                if (pid <= 0)
                    return null;

                var info = new ClientProcessInfo { Pid = pid };
                try
                {
                    using (var process = Process.GetProcessById(pid))
                    {
                        info.ProcessName = process.ProcessName;
                        // MainModule throws for elevated/protected processes; name alone still identifies.
                        try { info.ExecutablePath = process.MainModule?.FileName; }
                        catch { }
                    }
                }
                catch
                {
                    return null;
                }
                return info;
            }
            catch
            {
                return null;
            }
#else
            return null;
#endif
        }

        // Row ports are the low 16 bits in network byte order.
        internal static int DecodePort(uint rawPort)
        {
            return (int)(((rawPort & 0xFF) << 8) | ((rawPort >> 8) & 0xFF));
        }

#if UNITY_EDITOR_WIN
        private const int AF_INET = 2;
        private const int TCP_TABLE_OWNER_PID_CONNECTIONS = 4;

        [StructLayout(LayoutKind.Sequential)]
        private struct MIB_TCPROW_OWNER_PID
        {
            public uint state;
            public uint localAddr;
            public uint localPort;
            public uint remoteAddr;
            public uint remotePort;
            public uint owningPid;
        }

        [DllImport("iphlpapi.dll", SetLastError = true)]
        private static extern uint GetExtendedTcpTable(
            IntPtr pTcpTable, ref int dwOutBufLen, bool sort, int ipVersion, int tableClass, uint reserved);

        private static int FindOwningPid(int clientPort, int serverPort)
        {
            int bufferSize = 0;
            GetExtendedTcpTable(IntPtr.Zero, ref bufferSize, false, AF_INET, TCP_TABLE_OWNER_PID_CONNECTIONS, 0);
            if (bufferSize <= 0)
                return -1;

            var buffer = Marshal.AllocHGlobal(bufferSize);
            try
            {
                if (GetExtendedTcpTable(buffer, ref bufferSize, false, AF_INET, TCP_TABLE_OWNER_PID_CONNECTIONS, 0) != 0)
                    return -1;

                int rowCount = Marshal.ReadInt32(buffer);
                var rowPtr = buffer + 4;
                var rowSize = Marshal.SizeOf<MIB_TCPROW_OWNER_PID>();

                for (int i = 0; i < rowCount; i++)
                {
                    var row = Marshal.PtrToStructure<MIB_TCPROW_OWNER_PID>(rowPtr + i * rowSize);
                    if (DecodePort(row.localPort) == clientPort && DecodePort(row.remotePort) == serverPort)
                        return (int)row.owningPid;
                }
                return -1;
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }
#endif
    }
}
