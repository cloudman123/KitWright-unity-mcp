// Copyright (C) KitWright. Licensed under MIT.

using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;

namespace KitWright.Editor.Threading
{
    internal interface IEditorThreadHelper : IDisposable
    {
        Task<T> ExecuteOnEditorThreadAsync<T>(Func<T> func);
        Task<T> ExecuteAsyncOnEditorThreadAsync<T>(Func<Task<T>> asyncFunc, CancellationToken ct = default);
    }

    internal class EditorThreadHelper : IEditorThreadHelper
    {
        private readonly ConcurrentQueue<(Func<object> func, TaskCompletionSource<object> tcs)> _funcQueue
            = new ConcurrentQueue<(Func<object>, TaskCompletionSource<object>)>();

        private readonly int _mainThreadId;
        private bool _disposed;

        public bool IsMainThread => Thread.CurrentThread.ManagedThreadId == _mainThreadId;

        public EditorThreadHelper()
        {
            _mainThreadId = Thread.CurrentThread.ManagedThreadId;
            EditorApplication.update += ProcessQueues;
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

            return outerTcs.Task;
        }

        private void ProcessQueues()
        {
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
