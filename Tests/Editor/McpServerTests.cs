// Copyright (C) KitWright. Licensed under MIT.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using KitWright.Editor.MCP.Server;
using KitWright.Editor.Settings;
using KitWright.Editor.State;
using KitWright.Editor.Threading;
using KitWright.Editor.Tools;
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
            _tempRoot = Path.Combine(Path.GetTempPath(), "kitwright-mcpserver-" + Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_tempRoot))
                Directory.Delete(_tempRoot, recursive: true);
        }

        [Test]
        public void NegotiateProtocolVersion_EchoesSupportedRequestElseServerLatest()
        {
            Assert.AreEqual("2025-06-18", MCPRequestHandler.ProtocolVersion);
            Assert.AreEqual("2024-11-05", MCPRequestHandler.NegotiateProtocolVersion("2024-11-05"));
            Assert.AreEqual("2025-03-26", MCPRequestHandler.NegotiateProtocolVersion("2025-03-26"));
            Assert.AreEqual("2025-06-18", MCPRequestHandler.NegotiateProtocolVersion(null));
            Assert.AreEqual("2025-06-18", MCPRequestHandler.NegotiateProtocolVersion("1999-01-01"));
        }

        [Test]
        public async Task Ping_IsAnsweredWithAnEmptyResult()
        {
            var settings = new SettingsController(_tempRoot);
            using (var threadHelper = new EditorThreadHelper())
            using (var resourceProvider = new MCPResourceProvider(null, null))
            {
                var handler = new MCPRequestHandler(
                    new MCPToolExporter(settings),
                    new MCPExecutionBridge(threadHelper, settings, new StateController(), new FunctionInvoker(), null),
                    resourceProvider,
                    new MCPPromptProvider("Test", _tempRoot),
                    "KitWright MCP Server",
                    "0.0.0",
                    "pin");

                var response = await handler.HandleRequestAsync(
                    new MCPRequest { JsonRpc = "2.0", Id = 7, Method = "ping" }, CancellationToken.None);

                Assert.IsNotNull(response, "ping must be answered");
                Assert.IsNull(response.Error, "ping must not come back as an error");
                Assert.IsNotNull(response.Result);
                Assert.AreEqual(7, response.Id);
            }
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

    }
}
