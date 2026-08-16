// Copyright (C) KitWright. Licensed under MIT.

using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;

namespace KitWright.Editor.Threading
{
    internal class EditorThreadHelper : IDisposable
    {
        private readonly ConcurrentQueue<(Func<object> func, TaskCompletionSource<object> tcs)> _funcQueue
            = new ConcurrentQueue<(Func<object>, TaskCompletionSource<object>)>();

        private readonly int _mainThreadId;
        private bool _disposed;

        private static long s_lastPumpUtcTicks = DateTime.UtcNow.Ticks;

        // Under the 30s most MCP clients allow, so our explanation beats their bare timeout.
        private const int StallProbeMs = 20_000;

        // A slow tool keeps the pump ticking while it awaits; only a stalled pump means blocked.
        private const int PumpStaleMs = 5_000;

        public bool IsMainThread => Thread.CurrentThread.ManagedThreadId == _mainThreadId;

        internal static TimeSpan SinceLastPump =>
            TimeSpan.FromTicks(DateTime.UtcNow.Ticks - Interlocked.Read(ref s_lastPumpUtcTicks));

        internal static bool LooksBlocked(bool alreadyCompleted, TimeSpan sinceLastPump)
        {
            return !alreadyCompleted && sinceLastPump.TotalMilliseconds >= PumpStaleMs;
        }

        internal static string BlockedMessage(TimeSpan sinceLastPump)
        {
            return $"EDITOR_NOT_PUMPING: the Unity editor loop has not ticked for {sinceLastPump.TotalSeconds:F0}s, " +
                   "so this call is queued and cannot run. The usual cause is a modal dialog waiting for a click " +
                   "in the Unity window - most often 'Scene(s) Have Been Modified' after something tried to replace " +
                   "a scene with unsaved changes. Bring Unity to the front and dismiss it, then retry. " +
                   "The queued call still runs once the editor resumes.";
        }

        public EditorThreadHelper()
        {
            _mainThreadId = Thread.CurrentThread.ManagedThreadId;
            EditorApplication.update += ProcessQueues;
        }

        private static void FailIfEditorIsBlocked<T>(TaskCompletionSource<T> tcs)
        {
            Task.Delay(StallProbeMs).ContinueWith(_ =>
            {
                var idle = SinceLastPump;
                if (!LooksBlocked(tcs.Task.IsCompleted, idle))
                    return;

                tcs.TrySetException(new TimeoutException(BlockedMessage(idle)));
            }, TaskScheduler.Default);
        }

        public Task<T> ExecuteOnEditorThreadAsync<T>(Func<T> func)
        {
            if (_disposed)
                return CreateCanceledTask<T>();

            if (IsMainThread)
            {
                try
                {
                    return Task.FromResult(func());
                }
                catch (Exception ex)
                {
                    return Task.FromException<T>(ex);
                }
            }

            var outerTcs = new TaskCompletionSource<T>();
            var tcs = new TaskCompletionSource<object>();
            tcs.Task.ContinueWith(
                task =>
                {
                    if (task.IsCanceled)
                        outerTcs.TrySetCanceled();
                    else if (task.IsFaulted)
                        outerTcs.TrySetException(task.Exception?.InnerException ?? task.Exception ?? new Exception("Unknown error"));
                    else
                        outerTcs.TrySetResult((T)task.Result);
                },
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);

            _funcQueue.Enqueue((() => func(), tcs));
            FailIfEditorIsBlocked(outerTcs);
            return outerTcs.Task;
        }

        public Task<T> ExecuteAsyncOnEditorThreadAsync<T>(Func<Task<T>> asyncFunc, CancellationToken ct = default)
        {
            if (_disposed || ct.IsCancellationRequested)
                return CreateCanceledTask<T>();

            if (IsMainThread)
            {
                return asyncFunc();
            }

            var outerTcs = new TaskCompletionSource<T>();
            var ctRegistration = ct.CanBeCanceled
                ? ct.Register(() => outerTcs.TrySetCanceled(ct))
                : default(CancellationTokenRegistration?);

            var tcs = new TaskCompletionSource<object>();
            tcs.Task.ContinueWith(
                task =>
                {
                    if (task.IsCanceled)
                        outerTcs.TrySetCanceled();
                    else if (task.IsFaulted)
                        outerTcs.TrySetException(task.Exception?.InnerException ?? task.Exception ?? new Exception("Unknown error"));
                },
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);

            _funcQueue.Enqueue((() =>
            {
                asyncFunc().ContinueWith(task =>
                {
                    if (task.IsFaulted)
                        outerTcs.TrySetException(task.Exception?.InnerException ?? task.Exception ?? new Exception("Unknown error"));
                    else if (task.IsCanceled)
                        outerTcs.TrySetCanceled();
                    else
                        outerTcs.TrySetResult(task.Result);
                });
                return (object)null;
            }, tcs));

            if (ctRegistration.HasValue)
                outerTcs.Task.ContinueWith(_ => ctRegistration.Value.Dispose(), TaskContinuationOptions.ExecuteSynchronously);

            FailIfEditorIsBlocked(outerTcs);
            return outerTcs.Task;
        }

        private void ProcessQueues()
        {
            Interlocked.Exchange(ref s_lastPumpUtcTicks, DateTime.UtcNow.Ticks);
            if (_disposed) return;

            int processedCount = 0;
            const int maxPerFrame = 10;

            while (processedCount < maxPerFrame && _funcQueue.TryDequeue(out var item))
            {
                try
                {
                    var result = item.func();
                    item.tcs.TrySetResult(result);
                }
                catch (Exception ex)
                {
                    item.tcs.TrySetException(ex);
                }
                processedCount++;
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            EditorApplication.update -= ProcessQueues;

            while (_funcQueue.TryDequeue(out var item))
                item.tcs.TrySetCanceled();
        }

        private static Task<T> CreateCanceledTask<T>()
        {
            var tcs = new TaskCompletionSource<T>();
            tcs.SetCanceled();
            return tcs.Task;
        }
    }
}
