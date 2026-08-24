// Copyright (C) KitWright. Licensed under MIT.

using System.Collections.Generic;
using KitWright.Editor.MCP.Server;
using NUnit.Framework;
using UnityEngine;

namespace KitWright.Editor.Tests
{
    public sealed class JsonCodecTests
    {
        [Test]
        public void Serialize_WritesAnonymousTypesAsObjectsNotAsQuotedBlobs()
        {
            var json = JsonCodec.Serialize(new { jsonrpc = "2.0", id = 7, done = true });

            Assert.AreEqual("{\"jsonrpc\":\"2.0\",\"id\":7,\"done\":true}", json);
            Assert.That(json, Does.Not.Contain("="),
                "An anonymous type that reaches ToString() arrives as \"{ jsonrpc = 2.0 }\".");
        }

        [Test]
        public void Serialize_RecursesThroughNestedAnonymousTypesAndCollections()
        {
            var json = JsonCodec.Serialize(new
            {
                method = "notifications/message",
                @params = new { level = "error", tags = new List<string> { "a", "b" } }
            });

            Assert.AreEqual(
                "{\"method\":\"notifications/message\"," +
                "\"params\":{\"level\":\"error\",\"tags\":[\"a\",\"b\"]}}",
                json);
        }

        [Test]
        public void Serialize_KeepsTheStringFallbackForTypesUnityFormatsItself()
        {
            Assert.AreEqual("\"Warning\"", JsonCodec.Serialize(LogType.Warning));

            var vector = JsonCodec.Serialize(new Vector3(1f, 2f, 3f));
            Assert.That(vector, Does.StartWith("\"(").And.EndWith(")\""));
            Assert.That(vector, Does.Not.Contain("normalized"),
                "Reflecting over a Vector3 would recurse forever through its normalized property.");
        }

        [Test]
        public void Serialize_HandlesDictionariesListsAndScalars()
        {
            Assert.AreEqual("null", JsonCodec.Serialize(null));
            Assert.AreEqual("\"a\\\"b\"", JsonCodec.Serialize("a\"b"));
            Assert.AreEqual("[1,2]", JsonCodec.Serialize(new List<object> { 1, 2 }));
            Assert.AreEqual("{\"k\":\"v\"}",
                JsonCodec.Serialize(new Dictionary<string, object> { ["k"] = "v" }));
        }

        [Test]
        public void Serialize_EscapesControlCharsSoAReflectedNullByteStaysValidJson()
        {
            Assert.AreEqual("\"a\\u0000b\"", JsonCodec.Serialize("a\0b"));
            Assert.AreEqual("\"\\u001f\"", JsonCodec.Serialize("\u001f"));
            Assert.AreEqual("\"\\n\\t\"", JsonCodec.Serialize("\n\t"),
                "Named escapes must not regress to \\u form.");
        }

        [Test]
        public void Serialize_DropsNonFiniteFloatsToNullSoTheResponseStaysValidJson()
        {
            Assert.AreEqual("null", JsonCodec.Serialize(double.PositiveInfinity));
            Assert.AreEqual("null", JsonCodec.Serialize(double.NegativeInfinity));
            Assert.AreEqual("null", JsonCodec.Serialize(double.NaN));
            Assert.AreEqual("null", JsonCodec.Serialize(float.PositiveInfinity));
            Assert.AreEqual("1.5", JsonCodec.Serialize(1.5), "Finite floats must still serialize.");
        }
    }
}
