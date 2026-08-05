// Copyright (C) GameWright. Licensed under MIT.

namespace GameWright.Editor.MCP.Server
{
    internal static class MCPBrokerProtocol
    {
        // v2: pull responses carry AcceptSseHeader (client's Accept: text/event-stream),
        //     push requests may carry ContentTypeHeader to override the client-facing
        //     response content type (used for SSE-piggybacked notifications).
        public const int Version = 2;
        public const string Name = "gamewright-unity-mcp-broker";
        public const string HealthPath = "/_gamewright/broker/health";
        public const string AttachPath = "/_gamewright/broker/attach";
        public const string PullPath = "/_gamewright/broker/pull";
        public const string PushPath = "/_gamewright/broker/push";
        public const string DetachPath = "/_gamewright/broker/detach";
        public const string ShutdownPath = "/_gamewright/broker/shutdown";
        public const string TokenHeader = "X-GameWright-Broker-Token";
        public const string SessionHeader = "X-GameWright-Broker-Session";
        public const string ReqIdHeader = "X-GameWright-Broker-ReqId";
        public const string RedeliveryHeader = "X-GameWright-Broker-Redelivery";
        public const string BrokerHeader = "X-GameWright-Broker";
        public const string AcceptSseHeader = "X-GameWright-Broker-Accept-SSE";
        public const string ContentTypeHeader = "X-GameWright-Broker-Content-Type";
    }
}
