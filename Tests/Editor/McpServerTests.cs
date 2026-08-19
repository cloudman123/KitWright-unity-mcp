// Copyright (C) KitWright. Licensed under MIT.

using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using KitWright.Editor.MCP.Server;
using KitWright.Editor.MCP.Server.Security;
using KitWright.Editor.Tools.Builtins;
using NUnit.Framework;

namespace KitWright.Editor.Tests
{
    public sealed class McpServerTests
    {
        private string _tempRoot;

        [SetUp]
        public void SetUp()
        {
            _tempRoot = Path.Combine(Path.GetTempPath(), "kitwright-approval-" + Guid.NewGuid().ToString("N"));
            ClientApprovalStore.RootOverride = _tempRoot;
        }

        [TearDown]
        public void TearDown()
        {
            ClientApprovalStore.RootOverride = null;
            ClientApprovalGate.RequireApprovalOverride = null;
            ClientApprovalGate.ResolverOverride = null;
            if (Directory.Exists(_tempRoot))
                Directory.Delete(_tempRoot, recursive: true);
        }

        [Test]
        public void Store_ApproveThenIsApproved_RoundTrips()
        {
            Assert.IsFalse(ClientApprovalStore.IsApproved(@"C:\clients\claude.exe"));

            ClientApprovalStore.Approve(@"C:\clients\claude.exe");

            Assert.IsTrue(ClientApprovalStore.IsApproved(@"C:\clients\claude.exe"));
            Assert.IsTrue(ClientApprovalStore.IsApproved(@"c:\CLIENTS\CLAUDE.EXE"), "paths compare case-insensitively");
        }

        [Test]
        public void Store_NullOrEmptyIdentity_NeverApproved()
        {
            ClientApprovalStore.Approve(null);
            ClientApprovalStore.Approve("");

            Assert.IsFalse(ClientApprovalStore.IsApproved(null));
            Assert.IsFalse(ClientApprovalStore.IsApproved(""));
        }

        [Test]
        public void Resolver_DecodePort_SwapsNetworkByteOrder()
        {
            // 8765 = 0x223D -> stored as 0x3D22 in the low word.
            Assert.AreEqual(8765, TcpClientProcessResolver.DecodePort(0x3D22));
            Assert.AreEqual(80, TcpClientProcessResolver.DecodePort(0x5000));
        }

        [Test]
        public async Task Gate_ApprovalDisabled_Allows()
        {
            ClientApprovalGate.RequireApprovalOverride = () => false;

            using (var pair = await LoopbackPair.CreateAsync())
                Assert.IsTrue(await ClientApprovalGate.AuthorizeAsync(pair.ServerSide, pair.ServerPort));
        }

        [Test]
        public async Task Gate_OwnEditorProcess_AllowsWithoutPrompt()
        {
            ClientApprovalGate.RequireApprovalOverride = () => true;
            ClientApprovalGate.ResolverOverride = (clientPort, serverPort) =>
                new TcpClientProcessResolver.ClientProcessInfo { Pid = Process.GetCurrentProcess().Id };

            using (var pair = await LoopbackPair.CreateAsync())
                Assert.IsTrue(await ClientApprovalGate.AuthorizeAsync(pair.ServerSide, pair.ServerPort));
        }

        [Test]
        public async Task Gate_PreviouslyApprovedExecutable_AllowsWithoutPrompt()
        {
            ClientApprovalGate.RequireApprovalOverride = () => true;
            ClientApprovalGate.ResolverOverride = (clientPort, serverPort) =>
                new TcpClientProcessResolver.ClientProcessInfo
                {
                    Pid = 99999,
                    ExecutablePath = @"C:\clients\approved.exe",
                    ProcessName = "approved"
                };
            ClientApprovalStore.Approve(@"C:\clients\approved.exe");

            using (var pair = await LoopbackPair.CreateAsync())
                Assert.IsTrue(await ClientApprovalGate.AuthorizeAsync(pair.ServerSide, pair.ServerPort));
        }

        [Test]
        public void TryParseEnvelope_SuccessEnvelope()
        {
            var found = MCPRequestHandler.TryParseEnvelope(
                "{\"success\":true,\"message\":\"ok\",\"data\":{\"n\":1}}", out var envelope, out var isError);

            Assert.IsTrue(found);
            Assert.IsFalse(isError);
            Assert.IsNotNull(envelope);
        }

        [Test]
        public void TryParseEnvelope_ErrorEnvelope_SetsIsError()
        {
            var found = MCPRequestHandler.TryParseEnvelope(
                "{\"success\":false,\"code\":\"BOOM\",\"error\":\"BOOM\"}", out _, out var isError);

            Assert.IsTrue(found);
            Assert.IsTrue(isError);
        }

        [Test]
        public void TryParseEnvelope_RejectsNonEnvelopeInputs()
        {
            Assert.IsFalse(MCPRequestHandler.TryParseEnvelope("plain text result", out _, out _));
            Assert.IsFalse(MCPRequestHandler.TryParseEnvelope("{\"foo\":1}", out _, out _), "json without success field");
            Assert.IsFalse(MCPRequestHandler.TryParseEnvelope("{\"success\":\"yes\"}", out _, out _), "success must be boolean");
            Assert.IsFalse(MCPRequestHandler.TryParseEnvelope(null, out _, out _));
            Assert.IsFalse(MCPRequestHandler.TryParseEnvelope("{not json", out _, out _));
        }

        [Test]
        public async Task WaitForHotReloadOutcome_CompilationAlreadyStarted_ReturnsTrueImmediately()
        {
            var result = await CompilationFunctions.WaitForHotReloadOutcomeAsync(
                () => true, TimeSpan.FromSeconds(10));

            Assert.IsTrue(result);
        }

        [Test]
        public async Task WaitForHotReloadOutcome_NeverCompiles_ReturnsFalse()
        {
            var result = await CompilationFunctions.WaitForHotReloadOutcomeAsync(
                () => false, TimeSpan.Zero);

            Assert.IsFalse(result);
        }

        [Test]
        public async Task WaitForHotReloadOutcome_CompilationStartsMidWait_ReturnsTrue()
        {
            int calls = 0;
            var result = await CompilationFunctions.WaitForHotReloadOutcomeAsync(
                () => ++calls >= 2, TimeSpan.FromSeconds(5));

            Assert.IsTrue(result);
        }

        private sealed class LoopbackPair : IDisposable
        {
            public TcpClient ServerSide;
            public TcpClient ClientSide;
            public int ServerPort;
            private TcpListener _listener;

            public static async Task<LoopbackPair> CreateAsync()
            {
                var pair = new LoopbackPair();
                pair._listener = new TcpListener(IPAddress.Loopback, 0);
                pair._listener.Start();
                pair.ServerPort = ((IPEndPoint)pair._listener.LocalEndpoint).Port;

                var acceptTask = pair._listener.AcceptTcpClientAsync();
                pair.ClientSide = new TcpClient();
                await pair.ClientSide.ConnectAsync(IPAddress.Loopback, pair.ServerPort);
                pair.ServerSide = await acceptTask;
                return pair;
            }

            public void Dispose()
            {
                ServerSide?.Dispose();
                ClientSide?.Dispose();
                _listener?.Stop();
            }
        }
    }
}
