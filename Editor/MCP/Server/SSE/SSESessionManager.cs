// Copyright (C) KitWright. Licensed under MIT.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace KitWright.Editor.MCP.Server.SSE
{
    public enum AttachStreamResult
    {
        Success,
        SessionNotFound,
        StreamAlreadyAttached
    }

    /// <summary>
    /// Manages active MCP Streamable HTTP / Server-Sent Events sessions.
    /// Handles session tracking, log level filtering, dedup buffer, and direct socket writes.
    /// </summary>
    internal sealed class SSESessionManager
    {
        private static readonly Lazy<SSESessionManager> s_instance =
            new Lazy<SSESessionManager>(() => new SSESessionManager());
        public static SSESessionManager Instance => s_instance.Value;

        // MCP Log Severity ranking (increasing severity order)
        private static readonly Dictionary<string, int> SeverityRanks =
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                { "debug", 0 },
                { "info", 1 },
                { "notice", 2 },
                { "warning", 3 },
                { "error", 4 },
                { "critical", 5 },
                { "alert", 6 },
                { "emergency", 7 }
            };

        public sealed class SSESession
        {
            public string SessionId { get; }
            public DateTime LastActiveAt { get; set; }
            public int? MinSeverityLevel { get; set; }
            public NetworkStream ActiveStream { get; set; }
            public object StreamLock { get; } = new object();
            public TaskCompletionSource<bool> StreamCompletionTcs { get; set; }

            public SSESession(string sessionId)
            {
                SessionId = sessionId;
                LastActiveAt = DateTime.UtcNow;
            }
        }

        private readonly ConcurrentDictionary<string, SSESession> _sessions =
            new ConcurrentDictionary<string, SSESession>(StringComparer.OrdinalIgnoreCase);

        private int? _globalMinSeverityLevel;
        private readonly object _logDedupLock = new object();
        private string _lastLogKey;
        private DateTime _lastLogTime = DateTime.MinValue;
        private const int LogDedupWindowMs = 100;

        public int PingIntervalMs { get; set; } = 15_000;
        public TimeSpan SessionTtl { get; set; } = TimeSpan.FromMinutes(30);

        public SSESession CreateSession()
        {
            CleanupExpiredSessions();
            var sessionId = Guid.NewGuid().ToString("N");
            var session = new SSESession(sessionId);
            _sessions[sessionId] = session;
            return session;
        }

        public bool TryGetSession(string sessionId, out SSESession session)
        {
            if (string.IsNullOrEmpty(sessionId))
            {
                session = null;
                return false;
            }

            if (_sessions.TryGetValue(sessionId, out session))
            {
                session.LastActiveAt = DateTime.UtcNow;
                return true;
            }

            return false;
        }

        public void TouchSession(string sessionId)
        {
            if (!string.IsNullOrEmpty(sessionId) && _sessions.TryGetValue(sessionId, out var session))
            {
                session.LastActiveAt = DateTime.UtcNow;
            }
        }

        public AttachStreamResult TryAttachStream(string sessionId, NetworkStream stream, out SSESession session)
        {
            if (!TryGetSession(sessionId, out session))
                return AttachStreamResult.SessionNotFound;

            lock (session.StreamLock)
            {
                if (session.ActiveStream != null)
                    return AttachStreamResult.StreamAlreadyAttached;

                session.ActiveStream = stream;
                session.StreamCompletionTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                return AttachStreamResult.Success;
            }
        }

        public void DetachStream(SSESession session)
        {
            if (session == null) return;

            lock (session.StreamLock)
            {
                session.ActiveStream = null;
                session.StreamCompletionTcs?.TrySetResult(true);
            }
        }

        public void SetLoggingLevel(string sessionId, string levelName)
        {
            var rank = ParseSeverityRank(levelName);
            if (!string.IsNullOrEmpty(sessionId) && _sessions.TryGetValue(sessionId, out var session))
            {
                session.MinSeverityLevel = rank;
            }
            else
            {
                _globalMinSeverityLevel = rank;
            }
        }

        public static int? ParseSeverityRank(string levelName)
        {
            if (!string.IsNullOrEmpty(levelName) && SeverityRanks.TryGetValue(levelName, out var rank))
                return rank;
            return null;
        }

        public static string MapLogTypeToSeverity(LogType type, out int rank)
        {
            switch (type)
            {
                case LogType.Log:
                    rank = 1;
                    return "info";
                case LogType.Warning:
                    rank = 3;
                    return "warning";
                case LogType.Error:
                case LogType.Assert:
                    rank = 4;
                    return "error";
                case LogType.Exception:
                    rank = 5;
                    return "critical";
                default:
                    rank = 1;
                    return "info";
            }
        }

        public async Task BroadcastLogNotificationAsync(LogType type, string condition, string stackTrace)
        {
            var severity = MapLogTypeToSeverity(type, out var rank);

            // Deduplication within window
            lock (_logDedupLock)
            {
                var now = DateTime.UtcNow;
                var key = $"{type}:{condition}";
                if (string.Equals(_lastLogKey, key, StringComparison.Ordinal) &&
                    (now - _lastLogTime).TotalMilliseconds < LogDedupWindowMs)
                {
                    return;
                }
                _lastLogKey = key;
                _lastLogTime = now;
            }

            var notificationPayload = JsonCodec.Serialize(new
            {
                jsonrpc = "2.0",
                method = "notifications/message",
                @params = new
                {
                    level = severity,
                    logger = "UnityConsole",
                    data = string.IsNullOrEmpty(stackTrace) ? condition : $"{condition}\n{stackTrace}"
                }
            });

            var eventChunk = $"event: message\ndata: {notificationPayload}\n\n";
            var bytes = Encoding.UTF8.GetBytes(eventChunk);

            foreach (var kvp in _sessions)
            {
                var session = kvp.Value;
                var minRank = session.MinSeverityLevel ?? _globalMinSeverityLevel;
                if (!minRank.HasValue || rank < minRank.Value)
                    continue;

                await SendRawBytesDirectAsync(session, bytes).ConfigureAwait(false);
            }
        }

        public async Task BroadcastNotificationAsync(string method, object parameters)
        {
            var notificationPayload = JsonCodec.Serialize(new
            {
                jsonrpc = "2.0",
                method = method,
                @params = parameters
            });

            var eventChunk = $"event: message\ndata: {notificationPayload}\n\n";
            var bytes = Encoding.UTF8.GetBytes(eventChunk);

            foreach (var kvp in _sessions)
            {
                await SendRawBytesDirectAsync(kvp.Value, bytes).ConfigureAwait(false);
            }
        }

        public async Task SendRawBytesDirectAsync(SSESession session, byte[] bytes)
        {
            if (session == null || bytes == null || bytes.Length == 0)
                return;

            NetworkStream stream;
            lock (session.StreamLock)
            {
                stream = session.ActiveStream;
            }

            if (stream == null || !stream.CanWrite)
                return;

            try
            {
                await stream.WriteAsync(bytes, 0, bytes.Length).ConfigureAwait(false);
                await stream.FlushAsync().ConfigureAwait(false);
            }
            catch
            {
                // Disconnected socket will be swept by ping loop
            }
        }

        public async Task RunSsePingLoopAsync(SSESession session, CancellationToken ct)
        {
            var pingBytes = Encoding.UTF8.GetBytes(": ping\n\n");
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    await Task.Delay(PingIntervalMs, ct).ConfigureAwait(false);

                    NetworkStream stream;
                    lock (session.StreamLock)
                    {
                        stream = session.ActiveStream;
                    }

                    if (stream == null || !stream.CanWrite)
                        break;

                    try
                    {
                        await stream.WriteAsync(pingBytes, 0, pingBytes.Length, ct).ConfigureAwait(false);
                        await stream.FlushAsync(ct).ConfigureAwait(false);
                    }
                    catch
                    {
                        // Ping failed -> client dead
                        break;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Normal cancellation
            }
            finally
            {
                DetachStream(session);
            }
        }

        public void CleanupExpiredSessions()
        {
            var now = DateTime.UtcNow;
            foreach (var kvp in _sessions)
            {
                var session = kvp.Value;
                if (session.ActiveStream == null && (now - session.LastActiveAt) > SessionTtl)
                {
                    _sessions.TryRemove(kvp.Key, out _);
                }
            }
        }

        public void ResetForTests()
        {
            foreach (var kvp in _sessions)
                DetachStream(kvp.Value);

            _sessions.Clear();
            _globalMinSeverityLevel = null;
        }
    }
}
