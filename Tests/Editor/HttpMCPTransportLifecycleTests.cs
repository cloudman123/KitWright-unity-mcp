// Copyright (C) KitWright. Licensed under MIT.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using KitWright.Editor.MCP.Server;
using KitWright.Editor.Tools.Helpers;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace KitWright.Editor
{
    public sealed class HttpMCPTransportLifecycleTests
    {
        private const string ServerName = "KitWright MCP Server - Test Project";
        private const string ProjectIdentityA = "project-a";

        // A full 64-hex identity; only the first ProjectIdentity.PinLength chars form the pin.
        private const string IdentityAaaa = "aaaa1111" + "00000000000000000000000000000000000000000000000000000000";

        [Test]
        public void ExtractPin_ReadsThePSegmentAndIgnoresTheQuery()
        {
            Assert.AreEqual("aaaa1111", HttpMCPTransport.ExtractPin("/p/aaaa1111/"));
            Assert.AreEqual("aaaa1111", HttpMCPTransport.ExtractPin("/p/aaaa1111"));
            Assert.AreEqual("aaaa1111", HttpMCPTransport.ExtractPin("/p/aaaa1111/?x=1"));
            Assert.AreEqual(string.Empty, HttpMCPTransport.ExtractPin("/"));
            Assert.AreEqual(string.Empty, HttpMCPTransport.ExtractPin("/p"));
            Assert.AreEqual(string.Empty, HttpMCPTransport.ExtractPin(null));
        }

        [Test]
        public void PinnedPathForAnotherProjectIsRefused()
        {
            var transport = new HttpMCPTransport(0, IdentityAaaa);

            Assert.IsTrue(transport.PathTargetsAnotherProject("/p/bbbb2222/"),
                "A request pinned to another project must be refused, not answered.");
            Assert.IsFalse(transport.PathTargetsAnotherProject("/p/aaaa1111/"));
            Assert.IsFalse(transport.PathTargetsAnotherProject("/p/AAAA1111/"), "Pin match is case-insensitive.");
            Assert.IsFalse(transport.PathTargetsAnotherProject("/"),
                "An unpinned path stays accepted so configs written before pinning keep working.");
        }

        [Test]
        public void ServerWithoutAnIdentityAcceptsEveryPath()
        {
            var transport = new HttpMCPTransport(0, null);

            Assert.IsFalse(transport.PathTargetsAnotherProject("/p/bbbb2222/"));
            Assert.IsFalse(transport.PathTargetsAnotherProject("/"));
        }

        [Test]
        public void ClientDisconnectDetection_CoversExpectedResponseWriteFailures()
        {
            Assert.IsTrue(HttpMCPTransport.IsClientDisconnectException(
                new IOException("Unable to read data from the transport connection: The socket has been shut down.")));
            Assert.IsTrue(HttpMCPTransport.IsClientDisconnectException(
                new ObjectDisposedException("NetworkStream")));
            Assert.IsFalse(HttpMCPTransport.IsClientDisconnectException(
                new InvalidOperationException("Unexpected transport failure.")));
        }

        [Test]
        public void RecentActivityBadge_InterruptedIsNotDisplayedAsOk()
        {
            Assert.AreEqual("OK", RecentActivityPanel.GetBadgeText(MCPToolCallStatus.Success));
            Assert.AreEqual("INT", RecentActivityPanel.GetBadgeText(MCPToolCallStatus.Interrupted));
            Assert.AreEqual("ERR", RecentActivityPanel.GetBadgeText(MCPToolCallStatus.Error));
        }

        [Test]
        public void InterruptedToolRecoveryStatus_EmptyContinuationIsInterrupted()
        {
            Assert.AreEqual(
                MCPToolCallStatus.Interrupted,
                MCPServerService.DetermineInterruptedToolRecoveryStatus(null));
            Assert.AreEqual(
                MCPToolCallStatus.Success,
                MCPServerService.DetermineInterruptedToolRecoveryStatus("Continuation completed."));
            Assert.AreEqual(
                MCPToolCallStatus.Error,
                MCPServerService.DetermineInterruptedToolRecoveryStatus(ToolResultFormatter.Error("TEST_ERROR")));
        }

        [UnityTest]
        public IEnumerator StartAsync_WhenPortIsAlreadyOwned_ReturnsFalseWithoutStoppingOwner()
        {
            var port = GetFreeTcpPort();
            var firstTransport = new HttpMCPTransport(port, ProjectIdentityA);
            var secondTransport = new HttpMCPTransport(port, ProjectIdentityA);

            firstTransport.OnRequestReceived += (request, sendResponse) =>
                HandleInitializeRequest(request, sendResponse, ProjectIdentityA);

            try
            {
                var firstStart = firstTransport.StartAsync();
                yield return WaitForTask(firstStart);
                Assert.IsTrue(firstStart.Result, "The first transport should bind a free port.");

                var stopwatch = Stopwatch.StartNew();
                using (var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(900)))
                {
                    var secondStart = secondTransport.StartAsync(cts.Token);
                    yield return WaitForTask(secondStart);
                    Assert.IsFalse(secondStart.Result, "A second transport must not report running when it does not own the listener.");
                }
                stopwatch.Stop();

                Assert.IsFalse(secondTransport.IsAttachedToExistingServer);
                Assert.Less(stopwatch.Elapsed, TimeSpan.FromSeconds(2));

                secondTransport.Stop();

                var probeTask = SendInitializeRequestAsync(port);
                yield return WaitForTask(probeTask);
                Assert.That(
                    probeTask.Result,
                    Does.Contain(ProjectIdentityA),
                    "Stopping a failed second transport must not stop the owning listener.");
            }
            finally
            {
                secondTransport.Dispose();
                firstTransport.Dispose();
            }
        }

        [UnityTest]
        public IEnumerator Stop_ReleasesOwnedPortForRestart()
        {
            var port = GetFreeTcpPort();
            var firstTransport = new HttpMCPTransport(port, ProjectIdentityA);
            var secondTransport = new HttpMCPTransport(port, ProjectIdentityA);

            firstTransport.OnRequestReceived += (request, sendResponse) =>
                HandleInitializeRequest(request, sendResponse, ProjectIdentityA);

            try
            {
                var firstStart = firstTransport.StartAsync();
                yield return WaitForTask(firstStart);
                Assert.IsTrue(firstStart.Result);

                firstTransport.Stop();

                var secondStart = secondTransport.StartAsync();
                yield return WaitForTask(secondStart);
                Assert.IsTrue(secondStart.Result, "Stopping the owner should release the port for a fresh transport.");
            }
            finally
            {
                secondTransport.Dispose();
                firstTransport.Dispose();
            }
        }

        [UnityTest]
        public IEnumerator StartAsync_UnresponsivePortOwnerFailsWithoutReportingRunning()
        {
            var port = GetFreeTcpPort();
            using (var listener = CreateHttpListener(port))
            using (var listenerCts = new CancellationTokenSource())
            {
                listener.Start();
                var serverTask = HoldRequestsOpenAsync(listener, listenerCts.Token);
                var transport = new HttpMCPTransport(port, ProjectIdentityA);

                try
                {
                    using (var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(1200)))
                    {
                        var startTask = transport.StartAsync(cts.Token);
                        yield return WaitForTask(startTask);
                        Assert.IsFalse(startTask.Result);
                    }

                    Assert.IsFalse(transport.IsRunning);
                }
                finally
                {
                    transport.Dispose();
                    listenerCts.Cancel();
                    listener.Close();
                    serverTask.Wait(100);
                }
            }
        }

        [UnityTest]
        public IEnumerator RequestWithoutSubscriber_ReturnsServerNotReadyErrorWithoutWaitingForTimeout()
        {
            var port = GetFreeTcpPort();
            var transport = new HttpMCPTransport(port, ProjectIdentityA);

            try
            {
                var startTask = transport.StartAsync();
                yield return WaitForTask(startTask);
                Assert.IsTrue(startTask.Result);

                var stopwatch = Stopwatch.StartNew();
                var probeTask = SendInitializeRequestAsync(port);
                yield return WaitForTask(probeTask, 2f);
                stopwatch.Stop();

                Assert.That(probeTask.Result, Does.Contain("MCP server is stopping or not ready."));
                Assert.Less(stopwatch.Elapsed, TimeSpan.FromSeconds(2));
            }
            finally
            {
                transport.Dispose();
            }
        }

        [UnityTest]
        public IEnumerator SseAcceptingRequest_DeliversToolsListChangedOnce()
        {
            var port = GetFreeTcpPort();
            var transport = new HttpMCPTransport(port, ProjectIdentityA);
            transport.OnRequestReceived += (request, sendResponse) =>
            {
                sendResponse(new MCPResponse
                {
                    Id = request.Id,
                    Result = new Dictionary<string, object> { ["ok"] = true }
                });
            };

            try
            {
                var startTask = transport.StartAsync();
                yield return WaitForTask(startTask);
                Assert.IsTrue(startTask.Result);

                MCPToolListChangeNotifier.RestorePending();

                var firstRequest = SendToolListRequestAsync(port, acceptSse: true);
                yield return WaitForTask(firstRequest, 2f);
                Assert.AreEqual("text/event-stream", firstRequest.Result.ContentType);
                Assert.That(firstRequest.Result.Body, Does.Contain(MCPToolListChangeNotifier.NotificationJson));
                Assert.That(firstRequest.Result.Body, Does.Contain("\"id\":\"test\""));
                Assert.Less(
                    firstRequest.Result.Body.IndexOf(MCPToolListChangeNotifier.NotificationJson, StringComparison.Ordinal),
                    firstRequest.Result.Body.IndexOf("\"id\":\"test\"", StringComparison.Ordinal));

                var secondRequest = SendToolListRequestAsync(port, acceptSse: true);
                yield return WaitForTask(secondRequest, 2f);
                Assert.AreEqual("application/json", secondRequest.Result.ContentType);
                Assert.That(secondRequest.Result.Body, Does.Not.Contain(MCPToolListChangeNotifier.NotificationJson));
                Assert.That(secondRequest.Result.Body, Does.Contain("\"id\":\"test\""));
            }
            finally
            {
                while (MCPToolListChangeNotifier.TryConsumePending())
                {
                }

                transport.Dispose();
            }
        }

        [UnityTest]
        public IEnumerator StartStopSamePort_RebindsAndServesAcrossRepeatedCycles()
        {
            var port = GetFreeTcpPort();

            for (var cycle = 1; cycle <= 5; cycle++)
            {
                var transport = new HttpMCPTransport(port, ProjectIdentityA);
                transport.OnRequestReceived += (request, sendResponse) =>
                    HandleInitializeRequest(request, sendResponse, ProjectIdentityA);

                try
                {
                    var startTask = transport.StartAsync();
                    yield return WaitForTask(startTask);
                    Assert.IsTrue(startTask.Result, $"Cycle {cycle}: transport must rebind the port.");

                    var probeTask = SendInitializeRequestAsync(port);
                    yield return WaitForTask(probeTask, 3f);
                    Assert.That(
                        probeTask.Result,
                        Does.Contain(ProjectIdentityA),
                        $"Cycle {cycle}: rebound port must serve requests, not hang.");
                }
                finally
                {
                    transport.Stop();
                    transport.Dispose();
                }
            }
        }

        private static IEnumerator WaitForTask(Task task, float timeoutSeconds = 5f)
        {
            var start = Time.realtimeSinceStartup;
            while (!task.IsCompleted)
            {
                if (Time.realtimeSinceStartup - start > timeoutSeconds)
                    throw new TimeoutException("Timed out waiting for async test task.");

                yield return null;
            }

            if (task.IsFaulted)
                throw task.Exception;
        }

        private static void HandleInitializeRequest(
            MCPRequest request,
            Action<MCPResponse> sendResponse,
            string projectIdentity)
        {
            if (request.Method != "initialize")
            {
                sendResponse(new MCPResponse
                {
                    Id = request.Id,
                    Error = new MCPError { Code = -32601, Message = "Method not found" }
                });
                return;
            }

            sendResponse(new MCPResponse
            {
                Id = request.Id,
                Result = new Dictionary<string, object>
                {
                    ["serverInfo"] = new Dictionary<string, object>
                    {
                        ["name"] = ServerName,
                        ["version"] = "test"
                    },
                    ["kitwright"] = new Dictionary<string, object>
                    {
                        ["projectIdentity"] = projectIdentity,
                        ["projectIdentityVersion"] = ProjectIdentity.IdentityVersion
                    }
                }
            });
        }

        private static int GetFreeTcpPort()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            try
            {
                return ((IPEndPoint)listener.LocalEndpoint).Port;
            }
            finally
            {
                listener.Stop();
            }
        }

        private static HttpListener CreateHttpListener(int port)
        {
            var listener = new HttpListener();
            listener.Prefixes.Add($"http://127.0.0.1:{port}/");
            listener.Prefixes.Add($"http://localhost:{port}/");
            return listener;
        }

        private static async Task<string> SendInitializeRequestAsync(int port)
        {
            using (var client = new HttpClient { Timeout = TimeSpan.FromSeconds(1) })
            using (var content = new StringContent(
                       "{\"jsonrpc\":\"2.0\",\"id\":\"test\",\"method\":\"initialize\",\"params\":{}}",
                       Encoding.UTF8,
                       "application/json"))
            {
                var response = await client.PostAsync($"http://127.0.0.1:{port}/", content);
                return await response.Content.ReadAsStringAsync();
            }
        }

        private static async Task<HttpResult> SendToolListRequestAsync(int port, bool acceptSse)
        {
            using (var client = new HttpClient { Timeout = TimeSpan.FromSeconds(2) })
            using (var request = new HttpRequestMessage(HttpMethod.Post, $"http://127.0.0.1:{port}/"))
            {
                request.Content = new StringContent(
                    "{\"jsonrpc\":\"2.0\",\"id\":\"test\",\"method\":\"tools/list\",\"params\":{}}",
                    Encoding.UTF8,
                    "application/json");

                if (acceptSse)
                    request.Headers.Accept.ParseAdd("text/event-stream");

                var response = await client.SendAsync(request);
                return new HttpResult
                {
                    ContentType = response.Content.Headers.ContentType?.MediaType,
                    Body = await response.Content.ReadAsStringAsync()
                };
            }
        }

        private static async Task HoldRequestsOpenAsync(HttpListener listener, CancellationToken ct)
        {
            try
            {
                while (!ct.IsCancellationRequested && listener.IsListening)
                {
                    var context = await listener.GetContextAsync();
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await Task.Delay(TimeSpan.FromSeconds(5), ct);
                            context.Response.StatusCode = 204;
                            context.Response.Close();
                        }
                        catch
                        {
                            try { context.Response.Close(); } catch { }
                        }
                    }, ct);
                }
            }
            catch
            {
                // Listener shutdown during test cleanup.
            }
        }

        private static bool IsBindable(int port)
        {
            try
            {
                var probe = new TcpListener(IPAddress.Loopback, port);
                probe.Start();
                probe.Stop();
                return true;
            }
            catch (SocketException)
            {
                return false;
            }
        }

        [DllImport("kernel32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetHandleInformation(IntPtr handle, out int flags);

        // Regression: the post-reload port used to be written back into settings, so every reload
        // that fell forward raised the configured base permanently (8765 -> 8767 -> ...).
        [Test]
        public void SelectStartupBasePort_HintIsConsumedOnceAndLeavesConfiguredPortIntact()
        {
            MCPServerService.PreferredStartupPort = 8770;

            Assert.AreEqual(8770, MCPServerService.SelectStartupBasePort(8765));
            Assert.AreEqual(8765, MCPServerService.SelectStartupBasePort(8765));
        }

        // A P/Invoke that silently no-ops looks exactly like a working one: the port only leaks
        // once Unity exits with a child process still holding the inherited handle.
        [Test]
        public void DisableHandleInheritance_ClearsTheInheritFlagOnTheListeningSocket()
        {
            if (Application.platform != RuntimePlatform.WindowsEditor)
                Assert.Ignore("Handle inheritance is a Windows-only concern.");

            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            try
            {
                Assert.IsTrue(GetHandleInformation(listener.Server.Handle, out var before));
                Assume.That(before & 0x1, Is.EqualTo(0x1),
                    "Runtime already binds sockets non-inheritable; the production call is then a no-op.");

                HttpMCPTransport.DisableHandleInheritance(listener.Server);

                Assert.IsTrue(GetHandleInformation(listener.Server.Handle, out var after));
                Assert.AreEqual(0, after & 0x1,
                    "Listening socket is still inheritable, so children will keep the port bound.");
            }
            finally
            {
                listener.Stop();
            }
        }

        // The leak this guards against only shows up when the reload lands mid-start, so the
        // service-level stop is not enough: the transport must be closed off its own static.
        [UnityTest]
        public IEnumerator CloseActiveListener_FreesAPortTheServiceNeverMarkedRunning()
        {
            var port = GetFreeTcpPort();
            var transport = new HttpMCPTransport(port);

            try
            {
                var start = transport.StartAsync();
                yield return WaitForTask(start);
                Assert.IsTrue(start.Result, "Transport failed to bind a free port.");
                Assert.IsFalse(IsBindable(port), "Port should be held while the transport is up.");

                HttpMCPTransport.CloseActiveListener();

                Assert.IsTrue(IsBindable(port), "Listener survived the reload hook and orphaned the port.");
            }
            finally
            {
                transport.Dispose();
            }
        }

        private sealed class HttpResult
        {
            public string ContentType;
            public string Body;
        }
    }
}
